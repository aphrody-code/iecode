// Compression Helper - Auto-detection and decompression
// Supports: LZ10 (Nintendo/Level-5), LZ4 (modern standard)
// AOT-Compatible: Native AOT ready

using System;
using System.IO;

namespace IECODE.Core.Compression;

/// <summary>
/// Compression format enumeration
/// </summary>
public enum CompressionFormat
{
    None,
    Lz10,
    Lz4,
    Unknown
}

/// <summary>
/// Unified compression detection and decompression
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    /// Detect compression format from data
    /// </summary>
    public static CompressionFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return CompressionFormat.None;
        
        // Check LZ10 (Nintendo/Level-5)
        if (Lz10Decoder.IsLz10Compressed(data))
            return CompressionFormat.Lz10;
        
        // Check LZ4
        if (Lz4Decoder.IsLz4Compressed(data))
            return CompressionFormat.Lz4;
        
        return CompressionFormat.None;
    }
    
    /// <summary>
    /// Auto-decompress data based on detected format
    /// </summary>
    /// <param name="data">Input data (may be compressed or uncompressed)</param>
    /// <param name="expectedSize">Expected decompressed size (optional, for LZ4)</param>
    /// <returns>Decompressed data or original data if not compressed</returns>
    public static byte[] Decompress(ReadOnlySpan<byte> data, int expectedSize = -1)
    {
        var format = DetectFormat(data);
        
        return format switch
        {
            CompressionFormat.Lz10 => Lz10Decoder.Decompress(data),
            CompressionFormat.Lz4 => expectedSize > 0 
                ? Lz4Decoder.Decompress(data, expectedSize)
                : throw new InvalidDataException("LZ4 decompression requires expectedSize parameter"),
            CompressionFormat.None => data.ToArray(),
            _ => throw new NotSupportedException($"Unsupported compression format: {format}")
        };
    }
    
    /// <summary>
    /// Try to decompress data, return original if fails
    /// </summary>
    public static bool TryDecompress(ReadOnlySpan<byte> data, out byte[] result, int expectedSize = -1)
    {
        try
        {
            result = Decompress(data, expectedSize);
            return true;
        }
        catch
        {
            result = data.ToArray();
            return false;
        }
    }
    
    /// <summary>
    /// Check if data is compressed (any supported format)
    /// </summary>
    public static bool IsCompressed(ReadOnlySpan<byte> data)
    {
        return DetectFormat(data) != CompressionFormat.None;
    }
    
    /// <summary>
    /// Get decompressed size if available from header
    /// </summary>
    /// <returns>Decompressed size or -1 if unknown</returns>
    public static int GetDecompressedSize(ReadOnlySpan<byte> data)
    {
        var format = DetectFormat(data);
        
        return format switch
        {
            CompressionFormat.Lz10 => Lz10Decoder.GetDecompressedSize(data),
            CompressionFormat.Lz4 => -1, // LZ4 block format has no size header
            _ => -1
        };
    }
    
    /// <summary>
    /// Decompress file from disk
    /// </summary>
    public static byte[] DecompressFile(string path, int expectedSize = -1)
    {
        var data = File.ReadAllBytes(path);
        return Decompress(data, expectedSize);
    }
}
