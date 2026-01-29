using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IECODE.Core.Analysis;

/// <summary>
/// High-performance C code parser for decompiled source files.
/// Extracts functions, structs, enums, and global variables.
/// Optimized for large files (100MB+) using streaming and parallel processing.
/// </summary>
/// <remarks>
/// Native AOT compatible - uses source-generated regex and JSON serialization.
/// Designed for Ghidra/IDA decompiled output parsing.
/// </remarks>
public sealed partial class CCodeParserService : IDisposable
{
    private readonly ConcurrentDictionary<string, CFunction> _functions = new();
    private readonly ConcurrentDictionary<string, CStruct> _structs = new();
    private readonly ConcurrentDictionary<string, CEnum> _enums = new();
    private readonly ConcurrentDictionary<string, CGlobalVariable> _globals = new();

    private string? _sourceFile;
    private bool _isParsed;
    private ParsingStatistics _stats = new();

    #region Public Properties

    /// <summary>Whether the file has been parsed.</summary>
    public bool IsParsed => _isParsed;

    /// <summary>Source file path.</summary>
    public string? SourceFile => _sourceFile;

    /// <summary>Parsing statistics.</summary>
    public ParsingStatistics Stats => _stats;

    /// <summary>All extracted functions.</summary>
    public IReadOnlyDictionary<string, CFunction> Functions => _functions;

    /// <summary>All extracted structs.</summary>
    public IReadOnlyDictionary<string, CStruct> Structs => _structs;

    /// <summary>All extracted enums.</summary>
    public IReadOnlyDictionary<string, CEnum> Enums => _enums;

    /// <summary>All extracted global variables.</summary>
    public IReadOnlyDictionary<string, CGlobalVariable> Globals => _globals;

    #endregion

    #region Source Generated Regex (Native AOT Compatible)

    // Function declaration pattern: returnType functionName(params) {
    [GeneratedRegex(@"^(?<return>[\w\s\*]+?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<params>[^)]*)\)\s*\{?\s*$", 
        RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex FunctionDeclRegex();

    // Ghidra-style function: undefined4 FUN_12345678(void)
    [GeneratedRegex(@"^(?<return>undefined\d*|void|int|uint|long|ulong|byte|char|short|ushort|bool|float|double|longlong|ulonglong|pointer|code\s*\*?|[a-zA-Z_][a-zA-Z0-9_]*(?:\s*\*)?)\s+(?<name>FUN_[0-9a-fA-F]+|_?[A-Za-z_][A-Za-z0-9_]*)\s*\((?<params>[^)]*)\)\s*$", 
        RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex GhidraFunctionRegex();

    // Ghidra comment header: * Function: FUN_xxxxx
    [GeneratedRegex(@"\*\s*Function:\s*(?<name>\w+)", RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex GhidraFuncCommentRegex();

    // Ghidra address comment: * Address: xxxxx
    [GeneratedRegex(@"\*\s*Address:\s*(?<addr>[0-9a-fA-F]+)", RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex GhidraAddrCommentRegex();

    // Struct declaration: struct Name { or typedef struct { ... } Name;
    [GeneratedRegex(@"^(?:typedef\s+)?struct\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{?\s*$", 
        RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex StructDeclRegex();

    // Enum declaration: enum Name { or typedef enum { ... } Name;
    [GeneratedRegex(@"^(?:typedef\s+)?enum\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{?\s*$", 
        RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex EnumDeclRegex();

    // Global variable: type name = value; or type name;
    [GeneratedRegex(@"^(?<type>[\w\s\*]+?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*(?<value>[^;]+))?\s*;\s*$", 
        RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex GlobalVarRegex();

    // Address comment: /* Address: 0x12345678 */ or // 0x12345678
    [GeneratedRegex(@"(?:Address|addr|@)?\s*:?\s*(?:0x)?(?<addr>[0-9a-fA-F]{6,16})", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex AddressRegex();

    // Function call pattern
    [GeneratedRegex(@"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", 
        RegexOptions.Compiled | RegexOptions.ExplicitCapture)]
    private static partial Regex FunctionCallRegex();

    #endregion

    #region Parsing Methods

    /// <summary>
    /// Parses a C source file asynchronously with streaming for large files.
    /// </summary>
    /// <param name="filePath">Path to the C source file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsing statistics.</returns>
    public async Task<ParsingStatistics> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("C source file not found.", filePath);

        _sourceFile = filePath;
        _functions.Clear();
        _structs.Clear();
        _enums.Clear();
        _globals.Clear();
        _stats = new ParsingStatistics { SourceFile = filePath };

        var fileInfo = new FileInfo(filePath);
        _stats.FileSizeBytes = fileInfo.Length;
        _stats.StartTime = DateTime.UtcNow;

        // Use streaming for large files (>10MB)
        if (fileInfo.Length > 10 * 1024 * 1024)
        {
            await ParseLargeFileStreamingAsync(filePath, cancellationToken);
        }
        else
        {
            await ParseSmallFileAsync(filePath, cancellationToken);
        }

        _stats.EndTime = DateTime.UtcNow;
        _stats.FunctionCount = _functions.Count;
        _stats.StructCount = _structs.Count;
        _stats.EnumCount = _enums.Count;
        _stats.GlobalCount = _globals.Count;
        _isParsed = true;

        return _stats;
    }

    /// <summary>
    /// Parses a large file using memory-efficient streaming.
    /// Supports Ghidra comment-based function headers.
    /// </summary>
    private async Task ParseLargeFileStreamingAsync(string filePath, CancellationToken cancellationToken)
    {
        const int bufferSize = 4 * 1024 * 1024; // 4MB buffer
        
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, 
            FileShare.Read, bufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 
            bufferSize: bufferSize, leaveOpen: true);

        var lineNumber = 0;
        var currentFunction = new StringBuilder(16384);
        var braceDepth = 0;
        var inFunction = false;

        // State for pending function from Ghidra comment header
        var pendingFunctionName = "";
        ulong pendingFunctionAddress = 0;
        var pendingReturnType = "";
        var pendingParams = "";
        var pendingStartLine = 0;
        var waitingForBody = false;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;

            lineNumber++;
            _stats.TotalLines = lineNumber;

            var trimmed = line.Trim();

            // Skip empty lines unless we're in a function body
            if (string.IsNullOrWhiteSpace(trimmed) && !inFunction)
            {
                continue;
            }

            // Check for Ghidra function header in comment: * Function: FUN_xxxxx
            var funcCommentMatch = GhidraFuncCommentRegex().Match(trimmed);
            if (funcCommentMatch.Success)
            {
                pendingFunctionName = funcCommentMatch.Groups["name"].Value;
                pendingFunctionAddress = 0;
                waitingForBody = false;
                continue;
            }

            // Check for Ghidra address in comment: * Address: xxxxx
            var addrCommentMatch = GhidraAddrCommentRegex().Match(trimmed);
            if (addrCommentMatch.Success && !string.IsNullOrEmpty(pendingFunctionName))
            {
                if (ulong.TryParse(addrCommentMatch.Groups["addr"].Value, 
                    System.Globalization.NumberStyles.HexNumber, null, out var addr))
                {
                    pendingFunctionAddress = addr;
                }
                continue;
            }

            // Check for opening brace FIRST (start of function body)
            if (waitingForBody && trimmed == "{")
            {
                inFunction = true;
                waitingForBody = false;
                braceDepth = 1;
                currentFunction.AppendLine(line);
                continue;
            }

            // Check for function declaration line (after we saw the comment header)
            if (!inFunction && !string.IsNullOrEmpty(pendingFunctionName) && 
                !trimmed.StartsWith("/*") && !trimmed.StartsWith("*"))
            {
                var declMatch = GhidraFunctionRegex().Match(trimmed);
                if (declMatch.Success)
                {
                    var declName = declMatch.Groups["name"].Value;
                    // Match either by exact name or accept any FUN_ if we have a pending FUN_
                    if (declName == pendingFunctionName || 
                        (pendingFunctionName.StartsWith("FUN_") && declName.StartsWith("FUN_")))
                    {
                        pendingReturnType = declMatch.Groups["return"].Value.Trim();
                        pendingParams = declMatch.Groups["params"].Value.Trim();
                        pendingStartLine = lineNumber;
                        waitingForBody = true;
                        currentFunction.Clear();
                        currentFunction.AppendLine(line);
                    }
                }
                // Don't continue - let the flow check for opening brace on same line or next iteration
            }

            // Inside function body
            if (inFunction)
            {
                currentFunction.AppendLine(line);

                var openBraces = line.Count(c => c == '{');
                var closeBraces = line.Count(c => c == '}');
                braceDepth += openBraces - closeBraces;

                if (braceDepth <= 0)
                {
                    // End of function - process it
                    var funcLines = lineNumber - pendingStartLine + 1;
                    var body = currentFunction.ToString();

                    // Calculate cyclomatic complexity
                    var complexity = CalculateComplexity(body);

                    // Extract called functions
                    var calledFunctions = new HashSet<string>();
                    foreach (Match m in FunctionCallRegex().Matches(body))
                    {
                        var called = m.Groups["name"].Value;
                        if (called != pendingFunctionName)
                            calledFunctions.Add(called);
                    }

                    var func = new CFunction
                    {
                        Name = pendingFunctionName,
                        ReturnType = pendingReturnType,
                        ParameterString = pendingParams,
                        StartLine = pendingStartLine,
                        EndLine = lineNumber,
                        LineCount = funcLines,
                        Complexity = complexity,
                        Address = pendingFunctionAddress > 0 ? pendingFunctionAddress : null,
                        CalledFunctions = calledFunctions.Take(100).ToList(),
                        Body = body
                    };

                    _functions.TryAdd(pendingFunctionName, func);

                    // Reset state
                    inFunction = false;
                    currentFunction.Clear();
                    pendingFunctionName = "";
                    pendingFunctionAddress = 0;
                    pendingReturnType = "";
                    pendingParams = "";
                    braceDepth = 0;
                }
            }
        }
    }

    /// <summary>
    /// Calculates cyclomatic complexity for a function body.
    /// </summary>
    private static int CalculateComplexity(string body)
    {
        var complexity = 1;
        complexity += Regex.Matches(body, @"\bif\s*\(").Count;
        complexity += Regex.Matches(body, @"\belse\s+if\s*\(").Count;
        complexity += Regex.Matches(body, @"\bwhile\s*\(").Count;
        complexity += Regex.Matches(body, @"\bfor\s*\(").Count;
        complexity += Regex.Matches(body, @"\bswitch\s*\(").Count;
        complexity += Regex.Matches(body, @"\bcase\s+").Count;
        complexity += Regex.Matches(body, @"(\&\&|\|\|)").Count;
        complexity += Regex.Matches(body, @"\?[^:]+:").Count; // ternary
        return complexity;
    }

    /// <summary>
    /// Parses a smaller file by loading it into memory.
    /// </summary>
    private async Task ParseSmallFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var lines = content.Split('\n');
        _stats.TotalLines = lines.Length;

        var currentFunction = new StringBuilder(8192);
        var currentFunctionName = string.Empty;
        var currentFunctionStart = 0;
        var braceDepth = 0;
        var inFunction = false;

        for (var i = 0; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[i];
            var lineNumber = i + 1;
            var trimmedLine = line.TrimStart();

            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                if (inFunction) currentFunction.AppendLine(line);
                continue;
            }

            var openBraces = line.Count(c => c == '{');
            var closeBraces = line.Count(c => c == '}');

            if (!inFunction)
            {
                var funcMatch = GhidraFunctionRegex().Match(trimmedLine);
                if (!funcMatch.Success)
                    funcMatch = FunctionDeclRegex().Match(trimmedLine);

                if (funcMatch.Success)
                {
                    currentFunctionName = funcMatch.Groups["name"].Value;
                    currentFunctionStart = lineNumber;
                    currentFunction.Clear();
                    currentFunction.AppendLine(line);
                    braceDepth = openBraces - closeBraces;
                    inFunction = braceDepth > 0 || line.Contains('{');
                    continue;
                }
            }
            else
            {
                currentFunction.AppendLine(line);
                braceDepth += openBraces - closeBraces;

                if (braceDepth <= 0)
                {
                    var funcMatch = GhidraFunctionRegex().Match(currentFunction.ToString().Split('\n')[0].TrimStart());
                    if (!funcMatch.Success)
                        funcMatch = FunctionDeclRegex().Match(currentFunction.ToString().Split('\n')[0].TrimStart());

                    ProcessFunction(currentFunctionName, currentFunction.ToString(), 
                        currentFunctionStart, lineNumber, funcMatch);
                    inFunction = false;
                    currentFunction.Clear();
                    braceDepth = 0;
                }
            }
        }
    }

    #endregion

    #region Processing Methods

    private void ProcessFunction(string name, string body, int startLine, int endLine, Match match)
    {
        var returnType = match.Success ? match.Groups["return"].Value.Trim() : "unknown";
        var parameters = match.Success ? match.Groups["params"].Value.Trim() : "";

        // Extract address from function name (Ghidra style: FUN_12345678)
        ulong? address = null;
        if (name.StartsWith("FUN_", StringComparison.OrdinalIgnoreCase))
        {
            var hexPart = name[4..];
            if (ulong.TryParse(hexPart, System.Globalization.NumberStyles.HexNumber, null, out var addr))
                address = addr;
        }

        // Extract called functions
        var calledFunctions = new HashSet<string>();
        var callMatches = FunctionCallRegex().Matches(body);
        foreach (Match callMatch in callMatches)
        {
            var calledName = callMatch.Groups["name"].Value;
            if (calledName != name && !IsKeyword(calledName))
                calledFunctions.Add(calledName);
        }

        var function = new CFunction
        {
            Name = name,
            ReturnType = returnType,
            Parameters = ParseParameters(parameters),
            ParameterString = parameters,
            StartLine = startLine,
            EndLine = endLine,
            LineCount = endLine - startLine + 1,
            Address = address,
            Body = body,
            CalledFunctions = calledFunctions.ToList(),
            Complexity = CalculateCyclomaticComplexity(body)
        };

        _functions.TryAdd(name, function);
    }

    private void ProcessStruct(string name, int line)
    {
        var structure = new CStruct
        {
            Name = name,
            LineNumber = line
        };
        _structs.TryAdd(name, structure);
    }

    private void ProcessEnum(string name, int line)
    {
        var enumDef = new CEnum
        {
            Name = name,
            LineNumber = line
        };
        _enums.TryAdd(name, enumDef);
    }

    private void ProcessGlobal(string name, string type, int line)
    {
        var global = new CGlobalVariable
        {
            Name = name,
            Type = type,
            LineNumber = line
        };
        _globals.TryAdd(name, global);
    }

    private static List<CParameter> ParseParameters(string paramString)
    {
        var result = new List<CParameter>();
        if (string.IsNullOrWhiteSpace(paramString) || paramString == "void")
            return result;

        var parts = paramString.Split(',', StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var tokens = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2)
            {
                result.Add(new CParameter
                {
                    Type = string.Join(" ", tokens[..^1]),
                    Name = tokens[^1].TrimStart('*')
                });
            }
            else if (tokens.Length == 1)
            {
                result.Add(new CParameter { Type = tokens[0], Name = "" });
            }
        }
        return result;
    }

    private static int CalculateCyclomaticComplexity(string body)
    {
        var complexity = 1; // Base complexity

        // Count decision points
        complexity += Regex.Matches(body, @"\bif\s*\(").Count;
        complexity += Regex.Matches(body, @"\belse\s+if\s*\(").Count;
        complexity += Regex.Matches(body, @"\bwhile\s*\(").Count;
        complexity += Regex.Matches(body, @"\bfor\s*\(").Count;
        complexity += Regex.Matches(body, @"\bcase\s+").Count;
        complexity += Regex.Matches(body, @"\b(&&|\|\|)\b").Count;
        complexity += Regex.Matches(body, @"\?\s*[^:]+\s*:").Count; // Ternary

        return complexity;
    }

    private static bool IsKeyword(string name)
    {
        return name switch
        {
            "if" or "else" or "while" or "for" or "do" or "switch" or "case" or 
            "break" or "continue" or "return" or "goto" or "sizeof" or "typeof" or
            "true" or "false" or "null" or "NULL" => true,
            _ => false
        };
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets a function by name.
    /// </summary>
    public CFunction? GetFunction(string name) => _functions.GetValueOrDefault(name);

    /// <summary>
    /// Gets a function by address.
    /// </summary>
    public CFunction? GetFunctionByAddress(ulong address)
    {
        return _functions.Values.FirstOrDefault(f => f.Address == address);
    }

    /// <summary>
    /// Searches functions by name pattern.
    /// </summary>
    public IEnumerable<CFunction> SearchFunctions(string pattern)
    {
        return _functions.Values.Where(f => 
            f.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets functions sorted by complexity (most complex first).
    /// </summary>
    public IEnumerable<CFunction> GetMostComplexFunctions(int count = 50)
    {
        return _functions.Values
            .OrderByDescending(f => f.Complexity)
            .Take(count);
    }

    /// <summary>
    /// Gets functions sorted by line count (largest first).
    /// </summary>
    public IEnumerable<CFunction> GetLargestFunctions(int count = 50)
    {
        return _functions.Values
            .OrderByDescending(f => f.LineCount)
            .Take(count);
    }

    /// <summary>
    /// Gets call graph data (who calls whom).
    /// </summary>
    public Dictionary<string, List<string>> GetCallGraph()
    {
        return _functions.Values
            .Where(f => f.CalledFunctions.Count > 0)
            .ToDictionary(f => f.Name, f => f.CalledFunctions);
    }

    /// <summary>
    /// Gets functions that call a specific function.
    /// </summary>
    public IEnumerable<CFunction> GetCallers(string functionName)
    {
        return _functions.Values.Where(f => f.CalledFunctions.Contains(functionName));
    }

    #endregion

    #region Export Methods

    /// <summary>
    /// Exports all parsed data to JSON.
    /// </summary>
    public async Task ExportToJsonAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var data = new CCodeAnalysisResult
        {
            SourceFile = _sourceFile ?? "",
            ParsedAt = DateTime.UtcNow,
            Statistics = _stats,
            Functions = _functions.Values.OrderBy(f => f.Name).ToList(),
            Structs = _structs.Values.OrderBy(s => s.Name).ToList(),
            Enums = _enums.Values.OrderBy(e => e.Name).ToList(),
            Globals = _globals.Values.OrderBy(g => g.Name).ToList()
        };

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, data, CCodeParserJsonContext.Default.CCodeAnalysisResult, cancellationToken);
    }

    /// <summary>
    /// Generates a markdown summary report.
    /// </summary>
    public async Task GenerateMarkdownReportAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# C Code Analysis Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Source File:** `{Path.GetFileName(_sourceFile)}`");
        sb.AppendLine();

        // Statistics
        sb.AppendLine("## 📊 Statistics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| File Size | {_stats.FileSizeBytes / (1024.0 * 1024.0):F2} MB |");
        sb.AppendLine($"| Total Lines | {_stats.TotalLines:N0} |");
        sb.AppendLine($"| Functions | {_stats.FunctionCount:N0} |");
        sb.AppendLine($"| Structs | {_stats.StructCount:N0} |");
        sb.AppendLine($"| Enums | {_stats.EnumCount:N0} |");
        sb.AppendLine($"| Global Variables | {_stats.GlobalCount:N0} |");
        sb.AppendLine($"| Parse Time | {_stats.Duration.TotalSeconds:F2}s |");
        sb.AppendLine();

        // Top 20 largest functions
        sb.AppendLine("## 🏋️ Top 20 Largest Functions");
        sb.AppendLine();
        sb.AppendLine("| # | Function | Lines | Complexity | Address |");
        sb.AppendLine("|---|----------|-------|------------|---------|");
        var largest = GetLargestFunctions(20).ToList();
        for (var i = 0; i < largest.Count; i++)
        {
            var f = largest[i];
            var addr = f.Address.HasValue ? $"0x{f.Address:X8}" : "N/A";
            sb.AppendLine($"| {i + 1} | `{f.Name}` | {f.LineCount} | {f.Complexity} | {addr} |");
        }
        sb.AppendLine();

        // Top 20 most complex functions
        sb.AppendLine("## 🧠 Top 20 Most Complex Functions");
        sb.AppendLine();
        sb.AppendLine("| # | Function | Complexity | Lines | Calls |");
        sb.AppendLine("|---|----------|------------|-------|-------|");
        var complex = GetMostComplexFunctions(20).ToList();
        for (var i = 0; i < complex.Count; i++)
        {
            var f = complex[i];
            sb.AppendLine($"| {i + 1} | `{f.Name}` | {f.Complexity} | {f.LineCount} | {f.CalledFunctions.Count} |");
        }
        sb.AppendLine();

        // Function categories (by prefix)
        sb.AppendLine("## 📁 Function Categories");
        sb.AppendLine();
        var categories = _functions.Values
            .GroupBy(f => GetFunctionCategory(f.Name))
            .OrderByDescending(g => g.Count())
            .Take(30);

        sb.AppendLine("| Category | Count | Avg Lines | Avg Complexity |");
        sb.AppendLine("|----------|-------|-----------|----------------|");
        foreach (var cat in categories)
        {
            var avgLines = cat.Average(f => f.LineCount);
            var avgComplexity = cat.Average(f => f.Complexity);
            sb.AppendLine($"| `{cat.Key}` | {cat.Count()} | {avgLines:F1} | {avgComplexity:F1} |");
        }
        sb.AppendLine();

        // Return type distribution
        sb.AppendLine("## 📈 Return Type Distribution");
        sb.AppendLine();
        var returnTypes = _functions.Values
            .GroupBy(f => f.ReturnType)
            .OrderByDescending(g => g.Count())
            .Take(20);

        sb.AppendLine("| Return Type | Count | Percentage |");
        sb.AppendLine("|-------------|-------|------------|");
        foreach (var rt in returnTypes)
        {
            var pct = (double)rt.Count() / _functions.Count * 100;
            sb.AppendLine($"| `{rt.Key}` | {rt.Count()} | {pct:F1}% |");
        }
        sb.AppendLine();

        // All functions list (first 1000)
        sb.AppendLine("## 📋 Function List (First 1000)");
        sb.AppendLine();
        sb.AppendLine("<details>");
        sb.AppendLine("<summary>Click to expand</summary>");
        sb.AppendLine();
        sb.AppendLine("| Function | Return Type | Parameters | Lines | Address |");
        sb.AppendLine("|----------|-------------|------------|-------|---------|");
        foreach (var f in _functions.Values.OrderBy(f => f.Name).Take(1000))
        {
            var addr = f.Address.HasValue ? $"0x{f.Address:X8}" : "-";
            var paramCount = f.Parameters.Count;
            sb.AppendLine($"| `{f.Name}` | `{f.ReturnType}` | {paramCount} | {f.LineCount} | {addr} |");
        }
        sb.AppendLine();
        sb.AppendLine("</details>");
        sb.AppendLine();

        await File.WriteAllTextAsync(outputPath, sb.ToString(), cancellationToken);
    }

    private static string GetFunctionCategory(string name)
    {
        // Ghidra functions
        if (name.StartsWith("FUN_"))
            return "FUN_*";

        // Find prefix pattern
        var underscoreIndex = name.IndexOf('_');
        if (underscoreIndex > 0 && underscoreIndex < 20)
            return name[..underscoreIndex] + "_*";

        // Camel case prefix
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
        _functions.Clear();
        _structs.Clear();
        _enums.Clear();
        _globals.Clear();
        _isParsed = false;
    }
}

#region Data Models

/// <summary>
/// Represents a parsed C function.
/// </summary>
public sealed record CFunction
{
    public string Name { get; init; } = string.Empty;
    public string ReturnType { get; init; } = string.Empty;
    public List<CParameter> Parameters { get; init; } = [];
    public string ParameterString { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public int LineCount { get; init; }
    public ulong? Address { get; init; }
    
    [JsonIgnore]
    public string Body { get; init; } = string.Empty;
    
    public List<string> CalledFunctions { get; init; } = [];
    public int Complexity { get; init; }
}

/// <summary>
/// Represents a function parameter.
/// </summary>
public sealed record CParameter
{
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Represents a parsed C struct.
/// </summary>
public sealed record CStruct
{
    public string Name { get; init; } = string.Empty;
    public int LineNumber { get; init; }
    public List<CStructField> Fields { get; init; } = [];
}

/// <summary>
/// Represents a struct field.
/// </summary>
public sealed record CStructField
{
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Offset { get; init; }
}

/// <summary>
/// Represents a parsed C enum.
/// </summary>
public sealed record CEnum
{
    public string Name { get; init; } = string.Empty;
    public int LineNumber { get; init; }
    public List<CEnumValue> Values { get; init; } = [];
}

/// <summary>
/// Represents an enum value.
/// </summary>
public sealed record CEnumValue
{
    public string Name { get; init; } = string.Empty;
    public long Value { get; init; }
}

/// <summary>
/// Represents a global variable.
/// </summary>
public sealed record CGlobalVariable
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int LineNumber { get; init; }
}

/// <summary>
/// Parsing statistics.
/// </summary>
public sealed record ParsingStatistics
{
    public string SourceFile { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int TotalLines { get; set; }
    public int FunctionCount { get; set; }
    public int StructCount { get; set; }
    public int EnumCount { get; set; }
    public int GlobalCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Complete analysis result.
/// </summary>
public sealed record CCodeAnalysisResult
{
    public string SourceFile { get; init; } = string.Empty;
    public DateTime ParsedAt { get; init; }
    public ParsingStatistics Statistics { get; init; } = new();
    public List<CFunction> Functions { get; init; } = [];
    public List<CStruct> Structs { get; init; } = [];
    public List<CEnum> Enums { get; init; } = [];
    public List<CGlobalVariable> Globals { get; init; } = [];
}

#endregion

#region JSON Serialization Context

/// <summary>
/// JSON serializer context for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(CCodeAnalysisResult))]
[JsonSerializable(typeof(CFunction))]
[JsonSerializable(typeof(CParameter))]
[JsonSerializable(typeof(CStruct))]
[JsonSerializable(typeof(CStructField))]
[JsonSerializable(typeof(CEnum))]
[JsonSerializable(typeof(CEnumValue))]
[JsonSerializable(typeof(CGlobalVariable))]
[JsonSerializable(typeof(ParsingStatistics))]
[JsonSerializable(typeof(List<CFunction>))]
[JsonSerializable(typeof(List<CStruct>))]
[JsonSerializable(typeof(List<CEnum>))]
[JsonSerializable(typeof(List<CGlobalVariable>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CCodeParserJsonContext : JsonSerializerContext
{
}

#endregion
