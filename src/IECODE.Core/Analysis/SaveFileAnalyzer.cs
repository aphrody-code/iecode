using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IECODE.Core.IO;

namespace IECODE.Core.Analysis;

/// <summary>
/// Tools for analyzing save files and identifying their structure/encryption.
/// </summary>
public static class SaveFileAnalyzer
{
    /// <summary>
    /// Analyzes save file structure looking for patterns.
    /// </summary>
    public static SaveAnalysis Analyze(byte[] data)
    {
        var analysis = new SaveAnalysis();
        
        // Basic entropy check (high entropy = likely encrypted/compressed)
        analysis.Entropy = BinaryUtils.CalculateEntropy(data);
        analysis.IsLikelyEncrypted = analysis.Entropy > 7.5; // Max is 8.0
        
        // Look for known magic bytes patterns
        analysis.DetectedPatterns = FindPatterns(data);
        
        // Check for repeated byte sequences (indicates XOR encryption)
        analysis.RepeatedByteAnalysis = AnalyzeRepeatedBytes(data);
        
        // First bytes analysis
        analysis.FirstBytes = data.Take(64).ToArray();
        
        // Size analysis
        analysis.FileSize = data.Length;
        analysis.FileSizeKB = data.Length / 1024.0;
        analysis.FileSizeMB = data.Length / (1024.0 * 1024.0);
        
        return analysis;
    }
    
    /// <summary>
    /// Finds known patterns in data.
    /// </summary>
    private static List<PatternMatch> FindPatterns(byte[] data)
    {
        var patterns = new List<PatternMatch>();
        
        // Known magic bytes to search for
        var knownMagics = new Dictionary<byte[], string>
        {
            { "CFGB"u8.ToArray(), "CfgBin (Level-5 config)" },
            { "G4TX"u8.ToArray(), "G4TX Texture" },
            { "CPK "u8.ToArray(), "CPK Archive" },
            { "RIFF"u8.ToArray(), "RIFF (Audio/Video)" },
            { new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "PNG Image" },
            { "PK\x03\x04"u8.ToArray(), "ZIP Archive" },
            { new byte[] { 0x1F, 0x8B }, "GZIP Compressed" },
            { "Zstd"u8.ToArray(), "Zstandard Compressed" },
            { new byte[] { 0x28, 0xB5, 0x2F, 0xFD }, "Zstandard Frame" },
        };
        
        foreach (var (magic, name) in knownMagics)
        {
            int offset = FindBytes(data, magic);
            if (offset >= 0)
            {
                patterns.Add(new PatternMatch(name, offset, magic));
            }
        }
        
        return patterns;
    }
    
    /// <summary>
    /// Analyzes byte repetition patterns (useful for detecting XOR keys).
    /// </summary>
    private static RepeatedByteInfo AnalyzeRepeatedBytes(byte[] data)
    {
        var info = new RepeatedByteInfo();
        
        if (data.Length < 256) return info;
        
        // Check for repeating patterns at common key lengths
        int[] keySizes = [4, 8, 16, 32, 64, 128, 256];
        
        foreach (int keySize in keySizes)
        {
            if (data.Length < keySize * 3) continue;
            
            int matches = 0;
            int checks = Math.Min(1000, data.Length / keySize - 1);
            
            for (int i = 0; i < checks; i++)
            {
                int pos1 = i * keySize;
                int pos2 = pos1 + keySize;
                
                if (pos2 + keySize > data.Length) break;
                
                // XOR the two blocks - if encrypted with repeating key, result has patterns
                bool hasPattern = true;
                for (int j = 0; j < keySize && hasPattern; j++)
                {
                    byte xorResult = (byte)(data[pos1 + j] ^ data[pos2 + j]);
                    // Look for zeros (same plaintext XOR'd)
                    if (xorResult != 0) hasPattern = false;
                }
                
                if (hasPattern) matches++;
            }
            
            double ratio = (double)matches / checks;
            if (ratio > 0.1)
            {
                info.PossibleKeySize = keySize;
                info.RepetitionRatio = ratio;
                break;
            }
        }
        
        // Most common byte
        var frequency = new int[256];
        foreach (byte b in data)
            frequency[b]++;
        
        info.MostCommonByte = (byte)Array.IndexOf(frequency, frequency.Max());
        info.MostCommonByteCount = frequency.Max();
        info.MostCommonByteRatio = (double)info.MostCommonByteCount / data.Length;
        
        return info;
    }
    
    private static int FindBytes(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }
    
    /// <summary>
    /// Attempts to decrypt data using XOR with a key.
    /// </summary>
    public static byte[] TryXorDecrypt(byte[] data, byte[] key)
    {
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ key[i % key.Length]);
        }
        return result;
    }
    
    /// <summary>
    /// Generates a hex dump string for display.
    /// </summary>
    public static string GenerateHexDump(byte[] data, int offset = 0, int length = -1, int bytesPerLine = 16)
    {
        return BinaryUtils.GenerateHexDump(data, offset, length < 0 ? data.Length : length, bytesPerLine);
    }
}

/// <summary>
/// Results of save file analysis.
/// </summary>
public class SaveAnalysis
{
    public double Entropy { get; set; }
    public bool IsLikelyEncrypted { get; set; }
    public List<PatternMatch> DetectedPatterns { get; set; } = [];
    public RepeatedByteInfo RepeatedByteAnalysis { get; set; } = new();
    public byte[] FirstBytes { get; set; } = [];
    public int FileSize { get; set; }
    public double FileSizeKB { get; set; }
    public double FileSizeMB { get; set; }
}

/// <summary>
/// Pattern found in save data.
/// </summary>
public record PatternMatch(string Name, int Offset, byte[] Pattern);

/// <summary>
/// Repeated byte analysis info.
/// </summary>
public class RepeatedByteInfo
{
    public int PossibleKeySize { get; set; }
    public double RepetitionRatio { get; set; }
    public byte MostCommonByte { get; set; }
    public int MostCommonByteCount { get; set; }
    public double MostCommonByteRatio { get; set; }
}
