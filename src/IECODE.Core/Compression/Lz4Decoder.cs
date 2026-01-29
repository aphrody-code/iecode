// LZ4 Decoder - High-Speed Compression Algorithm
// Used in: Modern games, Level-5 newer formats
// Format: LZ4 block format (no frame header)
// AOT-Compatible: Native AOT ready, no external dependencies

using System;
using System.Buffers.Binary;
using System.IO;

namespace IECODE.Core.Compression;

/// <summary>
/// AOT-Compatible LZ4 Decoder for block format
/// Simplified implementation for game asset decompression
/// </summary>
public static class Lz4Decoder
{
    private const int MIN_MATCH_LENGTH = 4;
    private const int LAST_LITERALS_SIZE = 5;
    private const int MATCH_LENGTH_MASK = 0xF;
    
    /// <summary>
    /// Decompress LZ4 block format (no frame header)
    /// </summary>
    /// <param name="input">Compressed data</param>
    /// <param name="decompressedSize">Expected output size</param>
    /// <returns>Decompressed data</returns>
    public static byte[] Decompress(ReadOnlySpan<byte> input, int decompressedSize)
    {
        if (decompressedSize <= 0)
            throw new ArgumentException("Decompressed size must be positive", nameof(decompressedSize));
        
        var output = new byte[decompressedSize];
        int inputPos = 0;
        int outputPos = 0;
        
        while (inputPos < input.Length)
        {
            // Read token
            byte token = input[inputPos++];
            
            // Literal length (high 4 bits)
            int literalLength = token >> 4;
            
            // Extended literal length
            if (literalLength == 15)
            {
                while (inputPos < input.Length)
                {
                    byte extend = input[inputPos++];
                    literalLength += extend;
                    if (extend != 0xFF)
                        break;
                }
            }
            
            // Copy literals
            if (literalLength > 0)
            {
                if (inputPos + literalLength > input.Length)
                    throw new InvalidDataException("Literal length exceeds input size");
                
                if (outputPos + literalLength > output.Length)
                    throw new InvalidDataException("Literal copy exceeds output size");
                
                input.Slice(inputPos, literalLength).CopyTo(output.AsSpan(outputPos));
                inputPos += literalLength;
                outputPos += literalLength;
            }
            
            // Check if we're at the end
            if (inputPos >= input.Length)
                break;
            
            // Read match offset (16-bit little-endian)
            if (inputPos + 2 > input.Length)
                throw new InvalidDataException("Incomplete match offset");
            
            ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(input[inputPos..]);
            inputPos += 2;
            
            if (offset == 0 || offset > outputPos)
                throw new InvalidDataException($"Invalid match offset: {offset}");
            
            // Match length (low 4 bits + 4)
            int matchLength = (token & MATCH_LENGTH_MASK) + MIN_MATCH_LENGTH;
            
            // Extended match length
            if ((token & MATCH_LENGTH_MASK) == 15)
            {
                while (inputPos < input.Length)
                {
                    byte extend = input[inputPos++];
                    matchLength += extend;
                    if (extend != 0xFF)
                        break;
                }
            }
            
            // Copy match
            if (outputPos + matchLength > output.Length)
            {
                // Truncate to remaining space
                matchLength = output.Length - outputPos;
            }
            
            int matchPos = outputPos - offset;
            
            // Handle overlapping copies (RLE pattern)
            if (offset < matchLength)
            {
                // Pattern repeats
                for (int i = 0; i < matchLength; i++)
                {
                    output[outputPos++] = output[matchPos++];
                }
            }
            else
            {
                // Normal copy
                output.AsSpan(matchPos, matchLength).CopyTo(output.AsSpan(outputPos));
                outputPos += matchLength;
            }
            
            // Stop if output is complete
            if (outputPos >= decompressedSize)
                break;
        }
        
        return output;
    }
    
    /// <summary>
    /// Check if data looks like LZ4 compressed
    /// (Heuristic: no magic number in block format)
    /// </summary>
    public static bool IsLz4Compressed(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
            return false;
        
        // Check for LZ4 frame magic (optional)
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (magic == 0x184D2204) // LZ4 frame magic
            return true;
        
        // Heuristic: First token should be reasonable
        byte token = data[0];
        int literalLength = token >> 4;
        int matchLength = token & 0xF;
        
        // Both should be < 15 for most blocks
        return literalLength <= 15 && matchLength <= 15;
    }
}
