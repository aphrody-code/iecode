// Upgraded G4TX Parser based on Kuriimu2 plugin_level5
// Format: .g4tx (Level-5 Graphics 4 Texture Container)
// References: lib/Kuriimu2/plugins/Level5/plugin_level5/Switch/Archive/G4tx.cs
// AOT-Compatible: Native AOT ready, no reflection

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IECODE.Core.Formats.Level5.CfgBin.Tools;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace IECODE.Core.Formats.Level5;

/// <summary>
/// G4TX Header Structure (0x60 bytes)
/// Magic: "G4TX" - Level-5 Graphics 4 Texture Container
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct G4txHeader
{
    public readonly uint Magic;              // "G4TX" (0x47345458)
    public readonly short HeaderSize;        // 0x60
    public readonly short FileType;          // 0x65
    public readonly int Unknown1;            // 0x00180000
    public readonly int TableSize;           // Size of entry tables
    
    public readonly ulong Zeroes1;           // 0x10 padding (1/2)
    public readonly ulong Zeroes2;           // 0x10 padding (2/2)
    
    public readonly short TextureCount;      // Number of main textures
    public readonly short TotalCount;        // textureCount + subTextureCount
    public readonly byte Unknown2;           
    public readonly byte SubTextureCount;    // Number of sub-textures (atlas regions)
    public readonly short Unknown3;          
    public readonly int Unknown4;            
    public readonly int TextureDataSize;     // Total size of NXTCH data
    public readonly long Unknown5;           
    
    public readonly ulong Unknown6_1;        // 0x28 padding (1/5)
    public readonly ulong Unknown6_2;        // 0x28 padding (2/5)
    public readonly ulong Unknown6_3;        // 0x28 padding (3/5)
    public readonly ulong Unknown6_4;        // 0x28 padding (4/5)
    public readonly ulong Unknown6_5;        // 0x28 padding (5/5)
    
    public bool IsValid => Magic == 0x58543447; // "G4TX" (read as LE)
}

/// <summary>
/// G4TX Main Texture Entry (0x30 bytes)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct G4txEntry
{
    public readonly int Unknown1;
    public readonly int NxtchOffset;         // Offset to NXTCH chunk
    public readonly int NxtchSize;           // Size of NXTCH chunk
    public readonly int Unknown2;
    public readonly int Unknown3;
    public readonly int Unknown4;
    public readonly short Width;             // Texture width
    public readonly short Height;            // Texture height
    public readonly int Const2;              // Always 1?
    
    public readonly ulong Unknown5_1;        // 0x10 padding (1/2)
    public readonly ulong Unknown5_2;        // 0x10 padding (2/2)
}

/// <summary>
/// G4TX Sub-Texture Entry (0x18 bytes) - Atlas regions
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct G4txSubEntry
{
    public readonly short EntryId;           // Parent texture index
    public readonly short Unknown1;
    public readonly short X;                 // Region X offset
    public readonly short Y;                 // Region Y offset
    public readonly short Width;             // Region width
    public readonly short Height;            // Region height
    public readonly int Unknown2;
    public readonly int Unknown3;
    public readonly int Unknown4;
}

/// <summary>
/// NXTCH Header (Nintendo Switch Texture Chunk)
/// Magic: "NXTCH000"
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct NxtchHeader
{
    public readonly ulong Magic;             // "NXTCH000"
    
    public readonly int TextureDataSize;
    public readonly int Unknown1;
    public readonly int Unknown2;
    public readonly int Width;
    public readonly int Height;
    public readonly int Unknown3;
    public readonly int Unknown4;
    public readonly int Format;              // Texture format (BC7, BC1, etc.)
    public readonly int MipMapCount;
    public readonly int TextureDataSize2;    // Duplicate size?
    
    public bool IsValid 
    {
        get 
        {
            // Check if starts with "NXTCH" (0x484354584E in little endian)
            // Only check first 5 bytes
            return (Magic & 0xFFFFFFFFFF) == 0x484354584E;
        }
    }
}

/// <summary>
/// Parsed G4TX texture with metadata
/// </summary>
public readonly record struct G4txTexture(
    byte Id,
    string Name,
    int Width,
    int Height,
    int Format,
    int MipMapCount,
    ReadOnlyMemory<byte> TextureData,
    bool IsDds,
    IReadOnlyList<G4txSubTexture> SubTextures
);

/// <summary>
/// Parsed G4TX sub-texture (atlas region)
/// </summary>
public readonly record struct G4txSubTexture(
    byte Id,
    string Name,
    short X,
    short Y,
    short Width,
    short Height
);

/// <summary>
/// AOT-Compatible G4TX Parser with full Kuriimu2 features
/// </summary>
public static class G4txParser
{
    public const uint MAGIC = 0x47345458; // "G4TX"
    private const int HEADER_SIZE = 0x60;
    private const int ENTRY_SIZE = 0x30;
    private const int SUB_ENTRY_SIZE = 0x18;
    
    /// <summary>
    /// Parse G4TX header from binary data
    /// </summary>
    public static G4txHeader ParseHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < HEADER_SIZE)
            throw new InvalidDataException($"Data too small for G4TX header: {data.Length} < {HEADER_SIZE}");
        
        ref readonly var header = ref MemoryMarshal.AsRef<G4txHeader>(data[..HEADER_SIZE]);
        
        if (!header.IsValid)
            throw new InvalidDataException($"Invalid G4TX magic: 0x{header.Magic:X8}");
        
        return header;
    }
    
    /// <summary>
    /// Parse all textures from G4TX file with sub-texture support
    /// </summary>
    public static List<G4txTexture> ParseTextures(ReadOnlySpan<byte> data)
    {
        var header = ParseHeader(data);
        var textures = new List<G4txTexture>();
        
        // Calculate offsets
        int entryOffset = HEADER_SIZE;
        int subEntryOffset = entryOffset + header.TextureCount * ENTRY_SIZE;
        int hashOffset = (subEntryOffset + header.SubTextureCount * SUB_ENTRY_SIZE + 0xF) & ~0xF;
        int idOffset = hashOffset + header.TotalCount * 4;
        int stringOffset = (idOffset + header.TotalCount + 0x3) & ~0x3;
        
        // Read entries
        var entries = ParseEntries(data[entryOffset..], header.TextureCount);
        var subEntries = ParseSubEntries(data[subEntryOffset..], header.SubTextureCount);
        
        // Read IDs
        var ids = data.Slice(idOffset, header.TotalCount);
        
        // Read string offsets
        var stringOffsets = new short[header.TotalCount];
        for (int i = 0; i < header.TotalCount; i++)
        {
            stringOffsets[i] = BinaryPrimitives.ReadInt16LittleEndian(
                data.Slice(stringOffset + i * 2, 2));
        }
        
        // Calculate NXTCH base offset
        int nxtchBase = (header.HeaderSize + header.TableSize + 0xF) & ~0xF;
        
        // Parse each main texture
        for (int i = 0; i < header.TextureCount; i++)
        {
            ref readonly var entry = ref entries[i];
            
            // Read texture name
            string name = ReadNullTerminatedString(data[(stringOffset + stringOffsets[i])..]);
            
            // Extract Texture data (NXTCH or DDS)
            int nxtchOffset = nxtchBase + entry.NxtchOffset;
            var textureData = data.Slice(nxtchOffset, entry.NxtchSize).ToArray();
            
            // Check for DDS Magic (DX11/PC)
            bool isDds = false;
            int width = 0;
            int height = 0;
            int format = 0;
            int mipMapCount = 0;

            if (textureData.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(textureData) == DdsParser.DDS_MAGIC)
            {
                isDds = true;
                var ddsHeader = DdsParser.ParseHeader(textureData);
                width = (int)ddsHeader.Width;
                height = (int)ddsHeader.Height;
                format = (int)ddsHeader.PfFourCC;
                mipMapCount = (int)ddsHeader.MipMapCount;
            }
            else
            {
                // Parse NXTCH header for format info (Switch)
                var nxtchHeader = ParseNxtchHeader(textureData);
                width = nxtchHeader.Width;
                height = nxtchHeader.Height;
                format = nxtchHeader.Format;
                mipMapCount = nxtchHeader.MipMapCount;
            }
            
            // Find sub-textures for this entry
            var subTextures = new List<G4txSubTexture>();
            int subEntryId = header.TextureCount;
            
            foreach (ref readonly var subEntry in subEntries)
            {
                if (subEntry.EntryId == i)
                {
                    string subName = ReadNullTerminatedString(
                        data[(stringOffset + stringOffsets[subEntryId])..]);
                    
                    subTextures.Add(new G4txSubTexture(
                        Id: ids[subEntryId],
                        Name: subName,
                        X: subEntry.X,
                        Y: subEntry.Y,
                        Width: subEntry.Width,
                        Height: subEntry.Height
                    ));
                    
                    subEntryId++;
                }
            }
            
            textures.Add(new G4txTexture(
                Id: ids[i],
                Name: name,
                Width: width,
                Height: height,
                Format: format,
                MipMapCount: mipMapCount,
                TextureData: textureData,
                IsDds: isDds,
                SubTextures: subTextures
            ));
        }
        
        return textures;
    }
    
    /// <summary>
    /// Parse G4TX entries from binary data
    /// </summary>
    private static ReadOnlySpan<G4txEntry> ParseEntries(ReadOnlySpan<byte> data, int count)
    {
        if (data.Length < count * ENTRY_SIZE)
            throw new InvalidDataException($"Insufficient data for {count} G4TX entries");
        
        return MemoryMarshal.Cast<byte, G4txEntry>(data[..(count * ENTRY_SIZE)]);
    }
    
    /// <summary>
    /// Parse G4TX sub-entries from binary data
    /// </summary>
    private static ReadOnlySpan<G4txSubEntry> ParseSubEntries(ReadOnlySpan<byte> data, int count)
    {
        if (data.Length < count * SUB_ENTRY_SIZE)
            throw new InvalidDataException($"Insufficient data for {count} G4TX sub-entries");
        
        return MemoryMarshal.Cast<byte, G4txSubEntry>(data[..(count * SUB_ENTRY_SIZE)]);
    }
    
    /// <summary>
    /// Parse NXTCH header from texture chunk data
    /// </summary>
    public static NxtchHeader ParseNxtchHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < Marshal.SizeOf<NxtchHeader>())
            // throw new InvalidDataException("Data too small for NXTCH header");
            return new NxtchHeader(); // Return empty/invalid
        
        ref readonly var header = ref MemoryMarshal.AsRef<NxtchHeader>(data);
        
        // if (!header.IsValid)
        //    throw new InvalidDataException("Invalid NXTCH magic");
        
        return header;
    }
    
    /// <summary>
    /// Read null-terminated string from binary data
    /// </summary>
    private static string ReadNullTerminatedString(ReadOnlySpan<byte> data)
    {
        int nullIndex = data.IndexOf((byte)0);
        if (nullIndex == -1)
            nullIndex = data.Length;
        
        return Encoding.UTF8.GetString(data[..nullIndex]);
    }
    
    /// <summary>
    /// Extract NXTCH texture data (after header)
    /// </summary>
    public static ReadOnlyMemory<byte> ExtractNxtchTextureData(ReadOnlySpan<byte> nxtchData)
    {
        var header = ParseNxtchHeader(nxtchData);
        int headerSize = Marshal.SizeOf<NxtchHeader>();
        
        return nxtchData[headerSize..].ToArray();
    }
    
    /// <summary>
    /// Calculate CRC32 hash for texture name (used by Kuriimu2)
    /// </summary>
    public static uint CalculateNameHash(string name)
    {
        return Crc32.Compute(Encoding.UTF8.GetBytes(name));
    }
    
    /// <summary>
    /// Parse G4TX file from disk
    /// </summary>
    public static List<G4txTexture> ParseFile(string path)
    {
        var data = File.ReadAllBytes(path);
        return ParseTextures(data);
    }
    
    /// <summary>
    /// Extract all NXTCH chunks to a directory
    /// </summary>
    public static void ExtractTextureFiles(string g4txPath, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        
        var textures = ParseFile(g4txPath);
        
        foreach (var texture in textures)
        {
            string extension = texture.IsDds ? "dds" : "nxtch";
            var outputPath = Path.Combine(outputDir, $"{texture.Name}.{extension}");
            File.WriteAllBytes(outputPath, texture.TextureData.ToArray());
        }
    }
}



