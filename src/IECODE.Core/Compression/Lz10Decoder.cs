// LZ10 Decoder - Nintendo Compression Algorithm
// Used in: Level-5 games (Inazuma Eleven, Professor Layton)
// Format: 0x10 + 24-bit decompressed size + compressed data
// AOT-Compatible: Native AOT ready, no Stream API

using System;
using System.Buffers.Binary;
using System.IO;

namespace IECODE.Core.Compression;

/// <summary>
/// AOT-Compatible LZ10 Decoder for Nintendo/Level-5 compression
/// Based on Kuriimu2's Lz10Decoder (ported for Native AOT)
/// </summary>
public static class Lz10Decoder
{
    public const byte MAGIC = 0x10;
    
    /// <summary>
    /// Decompress LZ10 data from span
    /// </summary>
    /// <param name="input">Compressed data (with 0x10 header)</param>
    /// <returns>Decompressed data</returns>
    /// <exception cref="InvalidDataException">Invalid LZ10 header or corrupted data</exception>
    public static byte[] Decompress(ReadOnlySpan<byte> input)
    {
        if (input.Length < 4)
            throw new InvalidDataException("Input too small for LZ10 header");
        
        // Read header
        byte magic = input[0];
        if (magic != MAGIC)
            throw new InvalidDataException($"Invalid LZ10 magic: 0x{magic:X2} (expected 0x10)");
        
        // Decompressed size is 24-bit little-endian
        int decompressedSize = input[1] | (input[2] << 8) | (input[3] << 16);
        
        if (decompressedSize <= 0)
            throw new InvalidDataException($"Invalid decompressed size: {decompressedSize}");
        
        // Decompress payload
        return DecompressHeaderless(input[4..], decompressedSize);
    }
    
    /// <summary>
    /// Decompress LZ10 data without header (raw compressed stream)
    /// </summary>
    public static byte[] DecompressHeaderless(ReadOnlySpan<byte> input, int decompressedSize)
    {
        var output = new byte[decompressedSize];
        var circularBuffer = new CircularBuffer(0x1000);
        
        int inputPos = 0;
        int outputPos = 0;
        
        int flags = 0;
        int mask = 1;
        
        while (outputPos < decompressedSize && inputPos < input.Length)
        {
            // Read new flag byte every 8 blocks
            if (mask == 1)
            {
                if (inputPos >= input.Length)
                    throw new InvalidDataException("Unexpected end of compressed data");
                
                flags = input[inputPos++];
                mask = 0x80;
            }
            else
            {
                mask >>= 1;
            }
            
            // Check flag bit: 1 = compressed, 0 = uncompressed
            if ((flags & mask) != 0)
            {
                // Compressed block
                if (inputPos + 2 > input.Length)
                    throw new InvalidDataException("Unexpected end of compressed block");
                
                byte byte1 = input[inputPos++];
                byte byte2 = input[inputPos++];
                
                // Length: high 4 bits of byte1 + 3 (range: 3-18)
                int length = (byte1 >> 4) + 3;
                
                // Displacement: low 4 bits of byte1 + byte2 + 1 (range: 1-4096)
                int displacement = (((byte1 & 0x0F) << 8) | byte2) + 1;
                
                // Copy from circular buffer
                for (int i = 0; i < length; i++)
                {
                    byte value = circularBuffer.ReadFromDisplacement(displacement);
                    
                    if (outputPos < decompressedSize)
                    {
                        output[outputPos++] = value;
                        circularBuffer.WriteByte(value);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                // Uncompressed byte
                if (inputPos >= input.Length)
                    throw new InvalidDataException("Unexpected end of uncompressed data");
                
                byte value = input[inputPos++];
                
                if (outputPos < decompressedSize)
                {
                    output[outputPos++] = value;
                    circularBuffer.WriteByte(value);
                }
            }
        }
        
        return output;
    }
    
    /// <summary>
    /// Check if data starts with LZ10 header
    /// </summary>
    public static bool IsLz10Compressed(ReadOnlySpan<byte> data)
    {
        return data.Length >= 4 && data[0] == MAGIC;
    }
    
    /// <summary>
    /// Get decompressed size from LZ10 header
    /// </summary>
    public static int GetDecompressedSize(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return -1;
        
        if (data[0] != MAGIC)
            return -1;
        
        return data[1] | (data[2] << 8) | (data[3] << 16);
    }
    
    /// <summary>
    /// Circular buffer for LZ10 decompression
    /// Implements sliding window for back-references
    /// </summary>
    private ref struct CircularBuffer
    {
        private Span<byte> _buffer;
        private int _position;
        
        public CircularBuffer(int size)
        {
            _buffer = new byte[size];
            _position = 0;
        }
        
        public void WriteByte(byte value)
        {
            _buffer[_position % _buffer.Length] = value;
            _position++;
        }
        
        public byte ReadFromDisplacement(int displacement)
        {
            if (displacement <= 0 || displacement > _position)
                throw new InvalidDataException($"Invalid displacement: {displacement}");
            
            int readPos = (_position - displacement) % _buffer.Length;
            return _buffer[readPos];
        }
    }
}
