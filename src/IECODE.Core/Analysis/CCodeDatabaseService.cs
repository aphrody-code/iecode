using System.Security.Cryptography;
using IECODE.Core.Data;
using IECODE.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IECODE.Core.Analysis;

/// <summary>
/// Service for persisting parsed C code analysis to SQLite database.
/// Supports incremental updates based on file hash.
/// </summary>
public sealed class CCodeDatabaseService : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly bool _ownsContext;

    /// <summary>
    /// Creates a service with the default database path.
    /// </summary>
    public CCodeDatabaseService() : this(new AppDbContext(), true)
    {
    }

    /// <summary>
    /// Creates a service with a custom database path.
    /// </summary>
    public CCodeDatabaseService(string dbPath) : this(new AppDbContext(dbPath), true)
    {
    }

    /// <summary>
    /// Creates a service with an existing context.
    /// </summary>
    public CCodeDatabaseService(AppDbContext dbContext, bool ownsContext = false)
    {
        _dbContext = dbContext;
        _ownsContext = ownsContext;
    }

    /// <summary>
    /// Ensures the database and tables are created.
    /// </summary>
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if a file needs to be reparsed based on its hash.
    /// </summary>
    public async Task<bool> NeedsReparseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var currentHash = await ComputeFileHashAsync(filePath, cancellationToken);
        
        var existing = await _dbContext.CSourceFiles
            .FirstOrDefaultAsync(f => f.FilePath == filePath, cancellationToken);

        return existing == null || existing.FileHash != currentHash;
    }

    /// <summary>
    /// Saves parsed results to the database.
    /// </summary>
    public async Task SaveResultsAsync(CCodeParserService parser, bool storeBody = false, 
        CancellationToken cancellationToken = default)
    {
        if (!parser.IsParsed || parser.SourceFile == null)
            throw new InvalidOperationException("Parser has not parsed any file.");

        var filePath = parser.SourceFile;
        var fileName = Path.GetFileName(filePath);
        var fileHash = await ComputeFileHashAsync(filePath, cancellationToken);
        var parsedAt = DateTime.UtcNow;

        // Delete existing data for this file
        await DeleteFileDataAsync(filePath, cancellationToken);

        // Save source file record
        var sourceFile = new CSourceFileEntity
        {
            FilePath = filePath,
            FileName = fileName,
            FileSizeBytes = parser.Stats.FileSizeBytes,
            TotalLines = parser.Stats.TotalLines,
            FunctionCount = parser.Stats.FunctionCount,
            StructCount = parser.Stats.StructCount,
            EnumCount = parser.Stats.EnumCount,
            GlobalCount = parser.Stats.GlobalCount,
            ParseDurationSeconds = parser.Stats.Duration.TotalSeconds,
            ParsedAt = parsedAt,
            FileHash = fileHash
        };
        _dbContext.CSourceFiles.Add(sourceFile);

        // Batch insert functions
        var functionEntities = parser.Functions.Values.Select(f => new CFunctionEntity
        {
            Name = f.Name,
            ReturnType = f.ReturnType,
            Parameters = f.ParameterString,
            ParameterCount = f.Parameters.Count,
            StartLine = f.StartLine,
            EndLine = f.EndLine,
            LineCount = f.LineCount,
            Address = f.Address,
            Complexity = f.Complexity,
            CallCount = f.CalledFunctions.Count,
            CalledFunctions = string.Join(",", f.CalledFunctions.Take(100)), // Limit to prevent huge strings
            SourceFile = filePath,
            Category = GetFunctionCategory(f.Name),
            ParsedAt = parsedAt,
            Body = storeBody ? f.Body : null
        }).ToList();

        await _dbContext.CFunctions.AddRangeAsync(functionEntities, cancellationToken);

        // Batch insert structs
        var structEntities = parser.Structs.Values.Select(s => new CStructEntity
        {
            Name = s.Name,
            LineNumber = s.LineNumber,
            FieldCount = s.Fields.Count,
            FieldsJson = "[]", // TODO: Serialize fields
            SourceFile = filePath,
            ParsedAt = parsedAt
        }).ToList();

        await _dbContext.CStructs.AddRangeAsync(structEntities, cancellationToken);

        // Batch insert enums
        var enumEntities = parser.Enums.Values.Select(e => new CEnumEntity
        {
            Name = e.Name,
            LineNumber = e.LineNumber,
            ValueCount = e.Values.Count,
            ValuesJson = "[]", // TODO: Serialize values
            SourceFile = filePath,
            ParsedAt = parsedAt
        }).ToList();

        await _dbContext.CEnums.AddRangeAsync(enumEntities, cancellationToken);

        // Batch insert globals
        var globalEntities = parser.Globals.Values.Select(g => new CGlobalEntity
        {
            Name = g.Name,
            Type = g.Type,
            LineNumber = g.LineNumber,
            SourceFile = filePath,
            ParsedAt = parsedAt
        }).ToList();

        await _dbContext.CGlobals.AddRangeAsync(globalEntities, cancellationToken);

        // Save all changes
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes all data for a specific source file.
    /// </summary>
    public async Task DeleteFileDataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _dbContext.CFunctions.Where(f => f.SourceFile == filePath)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CStructs.Where(s => s.SourceFile == filePath)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CEnums.Where(e => e.SourceFile == filePath)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CGlobals.Where(g => g.SourceFile == filePath)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CSourceFiles.Where(s => s.FilePath == filePath)
            .ExecuteDeleteAsync(cancellationToken);
    }

    #region Query Methods

    /// <summary>
    /// Gets all tracked source files.
    /// </summary>
    public async Task<List<CSourceFileEntity>> GetSourceFilesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.CSourceFiles.OrderByDescending(f => f.ParsedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Searches functions by name pattern.
    /// </summary>
    public async Task<List<CFunctionEntity>> SearchFunctionsAsync(string pattern, int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CFunctions
            .Where(f => EF.Functions.Like(f.Name, $"%{pattern}%"))
            .OrderBy(f => f.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a function by name.
    /// </summary>
    public async Task<CFunctionEntity?> GetFunctionByNameAsync(string name, 
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CFunctions
            .FirstOrDefaultAsync(f => f.Name == name, cancellationToken);
    }

    /// <summary>
    /// Gets a function by address.
    /// </summary>
    public async Task<CFunctionEntity?> GetFunctionByAddressAsync(ulong address,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CFunctions
            .FirstOrDefaultAsync(f => f.Address == address, cancellationToken);
    }

    /// <summary>
    /// Gets the most complex functions.
    /// </summary>
    public async Task<List<CFunctionEntity>> GetMostComplexFunctionsAsync(int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CFunctions
            .OrderByDescending(f => f.Complexity)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the largest functions by line count.
    /// </summary>
    public async Task<List<CFunctionEntity>> GetLargestFunctionsAsync(int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CFunctions
            .OrderByDescending(f => f.LineCount)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets functions by category.
    /// </summary>
    public async Task<List<CFunctionEntity>> GetFunctionsByCategoryAsync(string category, int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CFunctions
            .Where(f => f.Category == category)
            .OrderBy(f => f.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets function category statistics.
    /// </summary>
    public async Task<List<CategoryStats>> GetCategoryStatsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.CFunctions
            .GroupBy(f => f.Category)
            .Select(g => new CategoryStats
            {
                Category = g.Key,
                Count = g.Count(),
                AvgLines = g.Average(f => f.LineCount),
                AvgComplexity = g.Average(f => f.Complexity),
                TotalLines = g.Sum(f => f.LineCount)
            })
            .OrderByDescending(c => c.Count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets return type statistics.
    /// </summary>
    public async Task<List<ReturnTypeStats>> GetReturnTypeStatsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.CFunctions
            .GroupBy(f => f.ReturnType)
            .Select(g => new ReturnTypeStats
            {
                ReturnType = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(r => r.Count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets functions that call a specific function.
    /// </summary>
    public async Task<List<CFunctionEntity>> GetCallersAsync(string functionName, int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CFunctions
            .Where(f => f.CalledFunctions.Contains(functionName))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets total statistics across all files.
    /// </summary>
    public async Task<TotalStats> GetTotalStatsAsync(CancellationToken cancellationToken = default)
    {
        var files = await _dbContext.CSourceFiles.CountAsync(cancellationToken);
        var functions = await _dbContext.CFunctions.CountAsync(cancellationToken);
        var structs = await _dbContext.CStructs.CountAsync(cancellationToken);
        var enums = await _dbContext.CEnums.CountAsync(cancellationToken);
        var globals = await _dbContext.CGlobals.CountAsync(cancellationToken);

        return new TotalStats
        {
            FileCount = files,
            FunctionCount = functions,
            StructCount = structs,
            EnumCount = enums,
            GlobalCount = globals
        };
    }

    #endregion

    #region Helpers

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes);
    }

    private static string GetFunctionCategory(string name)
    {
        if (name.StartsWith("FUN_"))
            return "FUN_*";

        var underscoreIndex = name.IndexOf('_');
        if (underscoreIndex > 0 && underscoreIndex < 20)
            return name[..underscoreIndex] + "_*";

        for (var i = 1; i < Math.Min(name.Length, 20); i++)
        {
            if (char.IsUpper(name[i]))
                return name[..i] + "*";
        }

        return name.Length > 10 ? name[..10] + "*" : name;
    }

    #endregion

    public void Dispose()
    {
        if (_ownsContext)
        {
            _dbContext.Dispose();
        }
    }
}

#region Stats DTOs

public sealed record CategoryStats
{
    public string Category { get; init; } = string.Empty;
    public int Count { get; init; }
    public double AvgLines { get; init; }
    public double AvgComplexity { get; init; }
    public int TotalLines { get; init; }
}

public sealed record ReturnTypeStats
{
    public string ReturnType { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed record TotalStats
{
    public int FileCount { get; init; }
    public int FunctionCount { get; init; }
    public int StructCount { get; init; }
    public int EnumCount { get; init; }
    public int GlobalCount { get; init; }
}

#endregion
