using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IECODE.Core.Formats.Level5;

/// <summary>
/// Parser for G4MD (Level-5 Model Data).
/// Contains geometry data (vertices, indices, submeshes).
/// 
/// Reverse Engineered from nie.exe FUN_14056d530:
/// - Format is stored as Big-Endian, converted to Little-Endian at load
/// - Header contains section offsets for vertex/face/bone data
/// - Multiple data sections with different strides
/// 
/// AOT-Compatible: No reflection.
/// </summary>
public class G4mdParser
{
    /// <summary>Magic "G4MD" in Little-Endian (as read on x86)</summary>
    public const uint MAGIC_LE = 0x444D3447;
    
    /// <summary>Magic "G4MD" in Big-Endian (native format)</summary>
    public const uint MAGIC_BE = 0x47344D44;
    
    // Legacy alias
    public const uint MAGIC = MAGIC_LE;

    /// <summary>
    /// G4MD Header structure (0x44+ bytes)
    /// Stored as Big-Endian, requires byte-swapping on x86.
    /// Based on FUN_14056d530 reverse engineering.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct G4mdHeader
    {
        public uint Magic;              // 0x00: "G4MD"
        public ushort Field04;          // 0x04: byte-swapped
        public ushort Field06;          // 0x06: byte-swapped
        public byte Field08;            // 0x08
        public byte Field09;            // 0x09
        public ushort SectionBase;      // 0x0A: Base for section offset calculations
        public uint Field0C;            // 0x0C: byte-swapped (32-bit)
        
        // Reserved: 0x10-0x1F (16 bytes)
        
        public ushort VertexCount;      // 0x20: Number of vertices (formerly SubmeshCount)
        public ushort FaceCount;        // 0x22: Number of faces (formerly TotalCount)
        public byte BoneCount;          // 0x24: Number of bone references
        public byte Reserved25;         // 0x25
        public ushort Reserved26;       // 0x26
        
        public uint Field28;            // 0x28: byte-swapped
        public uint Field2C;            // 0x2C: byte-swapped
        
        // Section offset table (all ushort, byte-swapped)
        public ushort VertexDataOffset; // 0x30: Vertex section offset
        public ushort BoneRefOffset;    // 0x32: Bone reference offset
        public ushort Section34Offset;  // 0x34
        public ushort Section36Offset;  // 0x36
        public ushort Section38Offset;  // 0x38
        public ushort Section3AOffset;  // 0x3A
        public ushort IndexOffset;      // 0x3C: Index buffer offset
        public ushort Section3EOffset;  // 0x3E
        public ushort Section40Offset;  // 0x40
        public ushort Section42Offset;  // 0x42
        
        // Legacy accessors
        public readonly ushort HeaderSize => (ushort)(SectionBase * 4);
        public readonly ushort TypeId => Field06;
        public readonly uint Unk1 => (uint)(Field08 | (Field09 << 8));
        public readonly uint Unk2 => Field0C;
        public readonly ushort SubmeshCount => VertexCount;
        public readonly ushort TotalCount => FaceCount;
    }

    /// <summary>
    /// Submesh/geometry section data
    /// </summary>
    public struct Submesh
    {
        public string Name;
        public int IndexCount;
        public int MaterialIndex;
        public uint VertexFormat;
        public int IndexBufferOffset;
        public int IndexBufferSize;
        public int VertexCount;
        public int VertexBufferOffset;
    }
    
    /// <summary>
    /// Bone reference entry
    /// </summary>
    public readonly record struct BoneRef(
        int BoneIndex,
        float Weight
    );

    public G4mdHeader Header { get; private set; }
    public List<Submesh> Submeshes { get; private set; } = [];
    public List<BoneRef> BoneRefs { get; private set; } = [];
    public bool IsBigEndian { get; private set; }

    public void Parse(string filePath)
    {
        var data = File.ReadAllBytes(filePath);
        Parse(data);
    }

    public void Parse(ReadOnlySpan<byte> data)
    {
        // Detect endianness
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data[..4]);
        IsBigEndian = magic == MAGIC_BE;
        
        Header = ParseHeader(data, IsBigEndian);
        Submeshes.Clear();
        BoneRefs.Clear();
        
        // Parse submeshes using section offset table
        ParseSubmeshes(data);
        
        // Parse bone references if present
        if (Header.BoneCount > 0)
        {
            ParseBoneRefs(data);
        }
    }
    
    /// <summary>
    /// Calculate absolute offset for a section.
    /// Formula from FUN_14056d530: base_ptr + (SectionBase + offset) * 4
    /// </summary>
    public int GetSectionOffset(ushort relativeOffset)
    {
        return (Header.SectionBase + relativeOffset) * 4;
    }
    
    private void ParseSubmeshes(ReadOnlySpan<byte> data)
    {
        // Submesh table starts at calculated offset
        int meshTableOffset = GetSectionOffset(Header.VertexDataOffset);
        int meshStride = 0x10; // 12-byte entries (3 x uint32) from FUN_14056d530
        
        // Fallback to legacy parsing if offset seems wrong
        if (meshTableOffset <= 0 || meshTableOffset >= data.Length)
        {
            meshTableOffset = 0xD0;
            meshStride = 0x50;
        }
        
        int count = Math.Min(Header.VertexCount, (data.Length - meshTableOffset) / meshStride);
        
        for (int i = 0; i < count; i++)
        {
            int offset = meshTableOffset + (i * meshStride);
            if (offset + meshStride > data.Length)
                break;
                
            var meshData = data.Slice(offset, Math.Min(meshStride, data.Length - offset));
            
            var mesh = new Submesh
            {
                Name = $"Mesh_{i}",
                IndexCount = ReadInt32(meshData, 0x00),
                MaterialIndex = meshData.Length >= 0x0C ? ReadInt32(meshData, 0x08) : 0,
                VertexFormat = meshData.Length >= 0x14 ? ReadUInt32(meshData, 0x10) : 0,
                IndexBufferOffset = meshData.Length >= 0x24 ? ReadInt32(meshData, 0x20) : 0,
                IndexBufferSize = meshData.Length >= 0x28 ? ReadInt32(meshData, 0x24) : 0,
                VertexCount = meshData.Length >= 0x2C ? ReadInt32(meshData, 0x28) : 0,
                VertexBufferOffset = meshData.Length >= 0x30 ? ReadInt32(meshData, 0x2C) : 0
            };
            
            Submeshes.Add(mesh);
        }
    }
    
    private void ParseBoneRefs(ReadOnlySpan<byte> data)
    {
        int boneOffset = GetSectionOffset(Header.BoneRefOffset);
        if (boneOffset <= 0 || boneOffset >= data.Length)
            return;
        
        // Bone entries are 8 bytes each (from FUN_14056d530 inner loop)
        int stride = 8;
        
        for (int i = 0; i < Header.BoneCount; i++)
        {
            int offset = boneOffset + (i * stride);
            if (offset + stride > data.Length)
                break;
            
            var entry = data.Slice(offset, stride);
            BoneRefs.Add(new BoneRef(
                BoneIndex: ReadInt32(entry, 0),
                Weight: BitConverter.Int32BitsToSingle(ReadInt32(entry, 4))
            ));
        }
    }
    
    private int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        if (offset + 4 > data.Length) return 0;
        return IsBigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(data[offset..])
            : BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }
    
    private uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
    {
        if (offset + 4 > data.Length) return 0;
        return IsBigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..])
            : BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    }
    
    private ushort ReadUInt16(ReadOnlySpan<byte> data, int offset)
    {
        if (offset + 2 > data.Length) return 0;
        return IsBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
    }

    private static G4mdHeader ParseHeader(ReadOnlySpan<byte> span, bool isBigEndian)
    {
        var header = new G4mdHeader();
        
        header.Magic = isBigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(span[..4])
            : BinaryPrimitives.ReadUInt32LittleEndian(span[..4]);
        
        if (header.Magic != MAGIC_LE && header.Magic != MAGIC_BE)
            throw new InvalidDataException($"Invalid G4MD magic: 0x{header.Magic:X8}");

        // Read with appropriate endianness
        header.Field04 = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[4..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[4..]);
        header.Field06 = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[6..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[6..]);
        header.Field08 = span[8];
        header.Field09 = span[9];
        header.SectionBase = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x0A..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x0A..]);
        header.Field0C = isBigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(span[0x0C..])
            : BinaryPrimitives.ReadUInt32LittleEndian(span[0x0C..]);
        
        // Counts at 0x20+
        header.VertexCount = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x20..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x20..]);
        header.FaceCount = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x22..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x22..]);
        header.BoneCount = span[0x24];
        
        // Section offsets (0x30-0x42)
        header.VertexDataOffset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x30..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x30..]);
        header.BoneRefOffset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x32..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x32..]);
        header.Section34Offset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x34..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x34..]);
        header.Section36Offset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x36..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x36..]);
        header.Section38Offset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x38..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x38..]);
        header.Section3AOffset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x3A..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x3A..]);
        header.IndexOffset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x3C..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x3C..]);
        header.Section3EOffset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x3E..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x3E..]);
        header.Section40Offset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x40..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x40..]);
        header.Section42Offset = isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span[0x42..])
            : BinaryPrimitives.ReadUInt16LittleEndian(span[0x42..]);
        
        return header;
    }
    
    /// <summary>
    /// Get summary information about a G4MD file.
    /// </summary>
    public string GetSummary()
    {
        return $"""
            G4MD Model Data
            ---------------
            Endianness: {(IsBigEndian ? "Big-Endian" : "Little-Endian")}
            Vertices: {Header.VertexCount}
            Faces: {Header.FaceCount}
            Bones: {Header.BoneCount}
            Submeshes: {Submeshes.Count}
            Section Base: 0x{Header.SectionBase:X4}
            Data Sections:
              - Vertex Data: 0x{GetSectionOffset(Header.VertexDataOffset):X}
              - Bone Refs:   0x{GetSectionOffset(Header.BoneRefOffset):X}
              - Index Data:  0x{GetSectionOffset(Header.IndexOffset):X}
            """;
    }
    
    /// <summary>
    /// Check if data is a valid G4MD file.
    /// </summary>
    public static bool IsG4md(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) return false;
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        return magic == MAGIC_LE || magic == MAGIC_BE;
    }
}
