// Texture Converter - BC1-BC7 to PNG/DDS
// Uses BCnEncoder.Net for decompression
// AOT-Compatible: Native AOT ready

using System;
using System.Buffers.Binary;
using System.IO;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace IECODE.Core.Graphics;

/// <summary>
/// Texture format enumeration (matches NXTCH formats)
/// </summary>
public enum TextureFormat
{
    Unknown = 0,
    BC1_UNORM = 1,   // DXT1
    BC2_UNORM = 2,   // DXT3
    BC3_UNORM = 3,   // DXT5
    BC4_UNORM = 4,   // ATI1/LATC1
    BC5_UNORM = 5,   // ATI2/LATC2
    BC6H_UFLOAT = 6, // HDR
    BC7_UNORM = 7,   // High quality
    RGBA8_UNORM = 15 // Uncompressed
}

/// <summary>
/// AOT-Compatible texture format converter
/// </summary>
public static class TextureConverter
{
    /// <summary>
    /// Maps raw G4TX format integer to TextureFormat enum
    /// </summary>
    public static TextureFormat MapG4txFormat(int rawFormat)
    {
        return rawFormat switch
        {
            0x18 => TextureFormat.RGBA8_UNORM,
            0x19 => TextureFormat.RGBA8_UNORM,
            0x1C => TextureFormat.BC1_UNORM,
            0x1D => TextureFormat.BC2_UNORM,
            0x1E => TextureFormat.BC3_UNORM,
            0x1F => TextureFormat.BC4_UNORM,
            0x20 => TextureFormat.BC5_UNORM,
            0x21 => TextureFormat.BC6H_UFLOAT,
            0x22 => TextureFormat.BC7_UNORM,
            // Fallback for values that might match the enum directly
            1 => TextureFormat.BC1_UNORM,
            2 => TextureFormat.BC2_UNORM,
            3 => TextureFormat.BC3_UNORM,
            4 => TextureFormat.BC4_UNORM,
            5 => TextureFormat.BC5_UNORM,
            6 => TextureFormat.BC6H_UFLOAT,
            7 => TextureFormat.BC7_UNORM,
            15 => TextureFormat.RGBA8_UNORM,
            _ => TextureFormat.Unknown
        };
    }

    /// <summary>
    /// Load DDS file to ImageSharp Image using BCnEncoder with manual header parsing
    /// </summary>
    public static Image<Rgba32> LoadDdsToImage(string path)
    {
        using var fs = File.OpenRead(path);
        return LoadDdsToImage(fs);
    }

    /// <summary>
    /// Load DDS data to ImageSharp Image using BCnEncoder with manual header parsing
    /// </summary>
    public static Image<Rgba32> LoadDdsToImage(ReadOnlySpan<byte> ddsData)
    {
        using var ms = new MemoryStream(ddsData.ToArray());
        return LoadDdsToImage(ms);
    }

    /// <summary>
    /// Load DDS stream to ImageSharp Image using BCnEncoder with manual header parsing
    /// </summary>
    public static Image<Rgba32> LoadDdsToImage(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
        
        // Read Magic
        uint magic = reader.ReadUInt32();
        if (magic != 0x20534444) // "DDS "
            throw new InvalidDataException("Invalid DDS magic");
            
        // Read Header
        int size = reader.ReadInt32(); // 124
        int flags = reader.ReadInt32();
        int height = reader.ReadInt32();
        int width = reader.ReadInt32();
        int pitchOrLinearSize = reader.ReadInt32();
        int depth = reader.ReadInt32();
        int mipMapCount = reader.ReadInt32();
        
        // Skip reserved1[11]
        stream.Seek(11 * 4, SeekOrigin.Current);
        
        // PixelFormat
        int pfSize = reader.ReadInt32(); // 32
        int pfFlags = reader.ReadInt32();
        uint fourCC = reader.ReadUInt32();
        int rgbBitCount = reader.ReadInt32();
        uint rBitMask = reader.ReadUInt32();
        uint gBitMask = reader.ReadUInt32();
        uint bBitMask = reader.ReadUInt32();
        uint aBitMask = reader.ReadUInt32();
        
        // Skip caps
        stream.Seek(4 * 4 + 4, SeekOrigin.Current); // caps1, caps2, caps3, caps4, reserved2
        
        CompressionFormat format = CompressionFormat.Unknown;
        
        if (fourCC == 0x30315844) // "DX10"
        {
            // Read DX10 header
            uint dxgiFormat = reader.ReadUInt32();
            int resourceDimension = reader.ReadInt32();
            int miscFlag = reader.ReadInt32();
            int arraySize = reader.ReadInt32();
            int miscFlags2 = reader.ReadInt32();
            
            format = MapDxgiFormatToCompressionFormat(dxgiFormat);
        }
        else
        {
            format = MapFourCCToCompressionFormat(fourCC);
        }
        
        // Read data
        long headerSize = stream.Position;
        long dataSize = stream.Length - headerSize;
        byte[] data = reader.ReadBytes((int)dataSize);
        
        var decoder = new BcDecoder();
        var pixels = decoder.DecodeRaw(data, width, height, format);
        
        // Convert to ImageSharp
        var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    if (idx < pixels.Length)
                    {
                        var p = pixels[idx];
                        row[x] = new Rgba32(p.r, p.g, p.b, p.a);
                    }
                }
            }
        });
        
        return image;
    }

    private static CompressionFormat MapFourCCToCompressionFormat(uint fourCC)
    {
        return fourCC switch
        {
            0x31545844 => CompressionFormat.Bc1, // DXT1
            0x33545844 => CompressionFormat.Bc2, // DXT3
            0x35545844 => CompressionFormat.Bc3, // DXT5
            0x31495441 => CompressionFormat.Bc4, // ATI1
            0x32495441 => CompressionFormat.Bc5, // ATI2
            _ => CompressionFormat.Unknown // Handle uncompressed or others?
        };
    }

    private static CompressionFormat MapDxgiFormatToCompressionFormat(uint dxgiFormat)
    {
        return dxgiFormat switch
        {
            71 => CompressionFormat.Bc1, // BC1_UNORM
            74 => CompressionFormat.Bc2, // BC2_UNORM
            77 => CompressionFormat.Bc3, // BC3_UNORM
            80 => CompressionFormat.Bc4, // BC4_UNORM
            83 => CompressionFormat.Bc5, // BC5_UNORM
            95 => CompressionFormat.Bc6U, // BC6H_UF16
            98 => CompressionFormat.Bc7, // BC7_UNORM
            _ => CompressionFormat.Unknown
        };
    }

    /// <summary>
    /// Convert BC compressed texture to RGBA32
    /// </summary>
    /// <param name="data">Compressed texture data</param>
    /// <param name="width">Texture width</param>
    /// <param name="height">Texture height</param>
    /// <param name="format">Texture format</param>
    /// <returns>RGBA32 pixel data</returns>
    public static Rgba32[] DecompressToRgba32(
        ReadOnlySpan<byte> data,
        int width,
        int height,
        TextureFormat format)
    {
        var decoder = new BcDecoder();
        var bcFormat = ConvertToBcFormat(format);
        
        // Decode BC compressed data
        var pixels = decoder.DecodeRaw(
            data.ToArray(),
            width,
            height,
            bcFormat);
        
        // Convert ColorRgba32 to ImageSharp Rgba32
        var result = new Rgba32[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            result[i] = new Rgba32(
                pixels[i].r,
                pixels[i].g,
                pixels[i].b,
                pixels[i].a);
        }
        
        return result;
    }
    
    /// <summary>
    /// Convert BC compressed texture directly to PNG
    /// </summary>
    public static byte[] ConvertToPng(
        ReadOnlySpan<byte> data,
        int width,
        int height,
        TextureFormat format)
    {
        // Decompress to RGBA32
        var pixels = DecompressToRgba32(data, width, height, format);
        
        // Create ImageSharp image
        using var image = Image.LoadPixelData<Rgba32>(pixels, width, height);
        
        // Encode to PNG
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
    
    /// <summary>
    /// Convert BC compressed texture to DDS file
    /// </summary>
    public static byte[] ConvertToDds(
        ReadOnlySpan<byte> data,
        int width,
        int height,
        TextureFormat format,
        int mipCount = 1)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        // Write DDS header
        WriteDdsHeader(writer, width, height, format, mipCount);
        
        // Write pixel data
        writer.Write(data);
        
        return ms.ToArray();
    }
    
    /// <summary>
    /// Convert TextureFormat to BCnEncoder CompressionFormat
    /// </summary>
    private static CompressionFormat ConvertToBcFormat(TextureFormat format)
    {
        return format switch
        {
            TextureFormat.BC1_UNORM => CompressionFormat.Bc1,
            TextureFormat.BC2_UNORM => CompressionFormat.Bc2,
            TextureFormat.BC3_UNORM => CompressionFormat.Bc3,
            TextureFormat.BC4_UNORM => CompressionFormat.Bc4,
            TextureFormat.BC5_UNORM => CompressionFormat.Bc5,
            TextureFormat.BC6H_UFLOAT => CompressionFormat.Bc6U,
            TextureFormat.BC7_UNORM => CompressionFormat.Bc7,
            _ => throw new NotSupportedException($"Unsupported format: {format}")
        };
    }
    
    /// <summary>
    /// Write DDS file header
    /// </summary>
    private static void WriteDdsHeader(
        BinaryWriter writer,
        int width,
        int height,
        TextureFormat format,
        int mipCount)
    {
        // Magic number "DDS "
        writer.Write(0x20534444);
        
        // Check if we need DX10 header
        bool isDx10 = format == TextureFormat.BC6H_UFLOAT || format == TextureFormat.BC7_UNORM;
        
        // DDS_HEADER
        writer.Write(124);                    // dwSize
        writer.Write(0x1 | 0x2 | 0x4 | 0x1000 | 0x20000); // dwFlags (CAPS | HEIGHT | WIDTH | PIXELFORMAT | MIPMAPCOUNT)
        writer.Write(height);                 // dwHeight
        writer.Write(width);                  // dwWidth
        writer.Write(0);                      // dwPitchOrLinearSize
        writer.Write(0);                      // dwDepth
        writer.Write(mipCount);               // dwMipMapCount
        
        // dwReserved1[11]
        for (int i = 0; i < 11; i++)
            writer.Write(0);
        
        // DDS_PIXELFORMAT
        writer.Write(32);                     // dwSize
        writer.Write(0x4);                    // dwFlags (DDPF_FOURCC)
        WriteFourCC(writer, format);          // dwFourCC
        writer.Write(0);                      // dwRGBBitCount
        writer.Write(0);                      // dwRBitMask
        writer.Write(0);                      // dwGBitMask
        writer.Write(0);                      // dwBBitMask
        writer.Write(0);                      // dwABitMask
        
        // dwCaps
        writer.Write(0x1000 | 0x8 | 0x400000); // DDSCAPS_TEXTURE | DDSCAPS_COMPLEX | DDSCAPS_MIPMAP
        writer.Write(0);                      // dwCaps2
        writer.Write(0);                      // dwCaps3
        writer.Write(0);                      // dwCaps4
        writer.Write(0);                      // dwReserved2

        // If DX10, write the extended header
        if (isDx10)
        {
            WriteDdsDx10Header(writer, format);
        }
    }

    private static void WriteDdsDx10Header(BinaryWriter writer, TextureFormat format)
    {
        // DXGI_FORMAT
        uint dxgiFormat = format switch
        {
            TextureFormat.BC6H_UFLOAT => 95, // DXGI_FORMAT_BC6H_UF16
            TextureFormat.BC7_UNORM => 98,   // DXGI_FORMAT_BC7_UNORM
            _ => 0
        };
        
        writer.Write(dxgiFormat);
        writer.Write(3); // D3D10_RESOURCE_DIMENSION_TEXTURE2D
        writer.Write(0); // miscFlag
        writer.Write(1); // arraySize
        writer.Write(0); // miscFlags2
    }
    
    /// <summary>
    /// Write DDS FourCC for texture format
    /// </summary>
    private static void WriteFourCC(BinaryWriter writer, TextureFormat format)
    {
        uint fourcc = format switch
        {
            TextureFormat.BC1_UNORM => 0x31545844,   // "DXT1"
            TextureFormat.BC2_UNORM => 0x33545844,   // "DXT3"
            TextureFormat.BC3_UNORM => 0x35545844,   // "DXT5"
            TextureFormat.BC4_UNORM => 0x31495441,   // "ATI1"
            TextureFormat.BC5_UNORM => 0x32495441,   // "ATI2"
            TextureFormat.BC6H_UFLOAT => 0x30315844, // "DX10"
            TextureFormat.BC7_UNORM => 0x30315844,   // "DX10"
            _ => throw new NotSupportedException($"No FourCC for format: {format}")
        };
        
        writer.Write(fourcc);
    }
    
    /// <summary>
    /// Calculate texture data size including mipmaps
    /// </summary>
    public static int CalculateTextureDataSize(
        int width,
        int height,
        TextureFormat format,
        int mipCount = 1)
    {
        int blockSize = GetBlockSize(format);
        int totalSize = 0;
        
        for (int i = 0; i < mipCount; i++)
        {
            int mipWidth = Math.Max(1, width >> i);
            int mipHeight = Math.Max(1, height >> i);
            
            if (format == TextureFormat.RGBA8_UNORM)
            {
                // Uncompressed: 4 bytes per pixel
                totalSize += mipWidth * mipHeight * 4;
            }
            else
            {
                // Block compressed: 4x4 blocks
                int blocksX = (mipWidth + 3) / 4;
                int blocksY = (mipHeight + 3) / 4;
                totalSize += blocksX * blocksY * blockSize;
            }
        }
        
        return totalSize;
    }
    
    /// <summary>
    /// Get block size in bytes for format
    /// </summary>
    private static int GetBlockSize(TextureFormat format)
    {
        return format switch
        {
            TextureFormat.BC1_UNORM => 8,    // 64 bits per 4x4 block
            TextureFormat.BC2_UNORM => 16,   // 128 bits per 4x4 block
            TextureFormat.BC3_UNORM => 16,   // 128 bits per 4x4 block
            TextureFormat.BC4_UNORM => 8,    // 64 bits per 4x4 block
            TextureFormat.BC5_UNORM => 16,   // 128 bits per 4x4 block
            TextureFormat.BC6H_UFLOAT => 16, // 128 bits per 4x4 block
            TextureFormat.BC7_UNORM => 16,   // 128 bits per 4x4 block
            TextureFormat.RGBA8_UNORM => 4,  // 4 bytes per pixel
            _ => throw new NotSupportedException($"Unknown format: {format}")
        };
    }
    
    /// <summary>
    /// Check if format is block-compressed
    /// </summary>
    public static bool IsBlockCompressed(TextureFormat format)
    {
        return format != TextureFormat.RGBA8_UNORM;
    }
}
