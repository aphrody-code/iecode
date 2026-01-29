using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;
using IECODE.Core.Formats.Criware.CriFs;
using IECODE.Core.Formats.Criware.CriFs.Definitions.Structs;
using IECODE.Core.Formats.Criware.CriFs.Encryption;

namespace IECODE.Core.Archives;

/// <summary>
/// Service d'extraction et de manipulation des archives CPK.
/// Wrapper autour de IECODE.Core.Formats.Criware.CriFs optimisé pour IEVR.
/// </summary>
public sealed class CpkService
{
    private readonly IEVRGame _game;
    
    // Pool de buffers pour éviter les allocations
    private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;
    private const int OptimalBufferSize = 256 * 1024; // 256 KB buffer optimal pour SSD

    public CpkService(IEVRGame game)
    {
        _game = game;
    }

    /// <summary>
    /// Liste tous les fichiers CPK dans le dossier packs/.
    /// </summary>
    public IEnumerable<string> GetAllCpkFiles()
    {
        if (!Directory.Exists(_game.PacksPath))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(_game.PacksPath, "*.cpk", SearchOption.AllDirectories))
        {
            yield return file;
        }
    }

    /// <summary>
    /// Ouvre un CPK et retourne la liste des fichiers qu'il contient.
    /// </summary>
    /// <param name="cpkPath">Chemin vers le fichier CPK</param>
    /// <param name="decrypt">Décrypter automatiquement si nécessaire</param>
    public CpkFileInfo[] GetFilesInCpk(string cpkPath, bool decrypt = true)
    {
        using var stream = OpenCpkStream(cpkPath, decrypt);
        using var reader = CriFsLib.Instance.CreateCpkReader(stream, ownsStream: true);

        return reader.GetFiles()
            .Select(f => new CpkFileInfo
            {
                FileName = f.FileName,
                Directory = f.Directory ?? string.Empty,
                FullPath = string.IsNullOrEmpty(f.Directory) 
                    ? f.FileName 
                    : $"{f.Directory}/{f.FileName}",
                FileSize = (long)f.FileSize,
                ExtractSize = (long)f.ExtractSize,
                IsCompressed = f.FileSize != f.ExtractSize
            })
            .ToArray();
    }

    /// <summary>
    /// Extrait un fichier spécifique d'un CPK.
    /// </summary>
    /// <param name="cpkPath">Chemin vers le CPK</param>
    /// <param name="filePath">Chemin du fichier dans le CPK</param>
    /// <param name="outputPath">Chemin de sortie</param>
    /// <param name="ct">Token d'annulation</param>
    public async Task ExtractFileAsync(string cpkPath, string filePath, string outputPath, CancellationToken ct = default)
    {
        using var stream = OpenCpkStream(cpkPath, true);
        using var reader = CriFsLib.Instance.CreateCpkReader(stream, ownsStream: true);

        var files = reader.GetFiles();
        var targetFile = files.FirstOrDefault(f =>
        {
            string fullPath = string.IsNullOrEmpty(f.Directory) ? f.FileName : $"{f.Directory}/{f.FileName}";
            return fullPath.Equals(filePath, StringComparison.OrdinalIgnoreCase);
        });

        if (targetFile.FileName == null)
        {
            throw new FileNotFoundException($"File not found in CPK: {filePath}");
        }

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var extracted = reader.ExtractFile(targetFile);
        await File.WriteAllBytesAsync(outputPath, extracted.Span.ToArray(), ct);
    }

    /// <summary>
    /// Extrait tous les fichiers d'un CPK vers un dossier.
    /// </summary>
    /// <param name="cpkPath">Chemin vers le CPK</param>
    /// <param name="outputDirectory">Dossier de sortie</param>
    /// <param name="progress">Callback de progression (fichiers extraits, total)</param>
    /// <param name="ct">Token d'annulation</param>
    public async Task ExtractAllAsync(
        string cpkPath,
        string outputDirectory,
        IProgress<(int current, int total, string fileName)>? progress = null,
        CancellationToken ct = default)
    {
        await ExtractAllOptimizedAsync(cpkPath, outputDirectory, Environment.ProcessorCount, progress, ct);
    }

    /// <summary>
    /// Extrait tous les fichiers d'un CPK avec pattern streaming Microsoft (Channel + Parallel.ForEachAsync).
    /// Architecture inspirée de ZipFile.ExtractToDirectory avec protection path traversal.
    /// </summary>
    public async Task ExtractAllOptimizedAsync(
        string cpkPath,
        string outputDirectory,
        int maxParallelism,
        IProgress<(int current, int total, string fileName)>? progress = null,
        CancellationToken ct = default)
    {
        // Normaliser le chemin de sortie (protection path traversal)
        outputDirectory = Path.GetFullPath(outputDirectory);
        if (!outputDirectory.EndsWith(Path.DirectorySeparatorChar))
            outputDirectory += Path.DirectorySeparatorChar;

        using var stream = OpenCpkStream(cpkPath, true);
        using var reader = CriFsLib.Instance.CreateCpkReader(stream, ownsStream: true);

        var files = reader.GetFiles().ToArray();
        int total = files.Length;
        int current = 0;

        // Pré-créer tous les répertoires en une passe
        var directories = files
            .Select(f => string.IsNullOrEmpty(f.Directory) ? null : Path.Combine(outputDirectory, f.Directory.Replace('/', Path.DirectorySeparatorChar)))
            .Where(d => d != null)
            .Distinct()
            .ToArray();
        
        foreach (var dir in directories)
        {
            Directory.CreateDirectory(dir!);
        }

        // Channel bounded pour backpressure (pattern Microsoft Docs)
        var channel = Channel.CreateBounded<(CpkFile file, string fullPath, string outputPath)>(
            new BoundedChannelOptions(maxParallelism * 2)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

        // Producer: Extraire les fichiers et alimenter le channel
        var producerTask = Task.Run(async () =>
        {
            try
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();

                    string fullPath = string.IsNullOrEmpty(file.Directory)
                        ? file.FileName
                        : $"{file.Directory}/{file.FileName}";

                    string relativePath = fullPath.Replace('/', Path.DirectorySeparatorChar);
                    string outputPath = Path.GetFullPath(Path.Combine(outputDirectory, relativePath));

                    // Protection path traversal (pattern Microsoft ZipFile)
                    if (!outputPath.StartsWith(outputDirectory, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Path traversal detected: {fullPath}");
                    }

                    await channel.Writer.WriteAsync((file, fullPath, outputPath), ct);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, ct);

        // Consumers: Extraire + écrire en parallèle (pattern Parallel.ForEachAsync)
        await Parallel.ForEachAsync(
            channel.Reader.ReadAllAsync(ct),
            new ParallelOptions 
            { 
                MaxDegreeOfParallelism = maxParallelism,
                CancellationToken = ct
            },
            async (item, token) =>
            {
                try
                {
                    // Extraction depuis CPK (IECODE.Core.Formats.Criware.CriFs)
                    using var extracted = reader.ExtractFile(item.file);
                    byte[] data = extracted.Span.ToArray();

                    // Écriture async avec FileStream optimisé
                    await WriteFileOptimizedAsync(item.outputPath, data, token);

                    // Progress reporting thread-safe
                    int done = Interlocked.Increment(ref current);
                    progress?.Report((done, total, item.fullPath));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Log mais continue (ne pas bloquer l'extraction globale)
                    Console.Error.WriteLine($"Error extracting {item.fullPath}: {ex.Message}");
                }
            });

        await producerTask;
    }

    /// <summary>
    /// Écrit un fichier de manière optimisée avec buffer poolé.
    /// </summary>
    private static async Task WriteFileOptimizedAsync(string path, byte[] data, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var fs = new FileStream(
            path, 
            FileMode.Create, 
            FileAccess.Write, 
            FileShare.None,
            bufferSize: OptimalBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        
        await fs.WriteAsync(data.AsMemory(), ct);
    }

    /// <summary>
    /// Ouvre un stream CPK avec décryptage automatique via IECODE.Core.Formats.Criware.CriFs.
    /// </summary>
    private static Stream OpenCpkStream(string cpkPath, bool decrypt)
    {
        if (!decrypt)
        {
            return new FileStream(cpkPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 128 * 1024, FileOptions.SequentialScan);
        }

        // Check if encrypted by reading magic
        byte[] magic = new byte[4];
        using (var fs = new FileStream(cpkPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            byte[] buffer = new byte[2048];
            fs.ReadExactly(buffer);
        }

        // CPK magic = "CPK "
        if (magic[0] == 'C' && magic[1] == 'P' && magic[2] == 'K' && magic[3] == ' ')
        {
            // Not encrypted - return plain FileStream
            return new FileStream(cpkPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 128 * 1024, FileOptions.SequentialScan);
        }

        // Use IECODE.Core.Formats.Criware.CriFs's CpkDecryptionStream which properly decrypts ALL data
        return CpkDecryptionStream.FromFile(cpkPath);
    }
}

/// <summary>
/// Informations sur un fichier dans un CPK.
/// </summary>
public readonly struct CpkFileInfo
{
    public required string FileName { get; init; }
    public required string Directory { get; init; }
    public required string FullPath { get; init; }
    public required long FileSize { get; init; }
    public required long ExtractSize { get; init; }
    public required bool IsCompressed { get; init; }
}

