using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IECODE.Core.Formats.Level5;

/// <summary>
/// Parser pour le format G4MG (Level-5 Mesh Geometry) d'Inazuma Eleven Victory Road.
/// Format basé sur l'analyse de Kuriimu2 et des plugins Noesis.
/// </summary>
public static class G4mgParser
{
    // Magic "G4MG"
    public const uint MAGIC_LE = 0x474D3447; // Little-Endian (x86)
    public const uint MAGIC_BE = 0x47344D47; // Big-Endian
    
    // Structs de base pour vecteurs
    public readonly record struct Vector2(float X, float Y);
    public readonly record struct Vector3(float X, float Y, float Z);
    
    /// <summary>
    /// Structure du header G4MG (basée sur G4PK pattern)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct G4mgHeader
    {
        public uint Magic;           // "G4MG"
        public short HeaderSize;     // 0x40 (64 bytes)
        public short FileType;       // 0x65 ou similaire
        public int Version;          // 0x00100000
        public int ContentSize;      // Taille des données
        
        // Padding 16 bytes
        public ulong Reserved1;
        public ulong Reserved2;
        
        public int MeshCount;        // Nombre de meshes
        public short SubMeshCount;   // Nombre de sub-meshes
        public short VertexFormatVersion; // Version du format vertex
        public short Unknown1;
        public short Unknown2;
        
        // Padding 20 bytes
        public ulong Reserved3;
        public ulong Reserved4;
        public uint Reserved5;
        
        public bool IsValid => (Magic == MAGIC_LE || Magic == MAGIC_BE) && HeaderSize == 0x40;
        public bool IsBigEndian => Magic == MAGIC_BE;
    }
    
    /// <summary>
    /// Entrée de mesh (structure inférée depuis Noesis parsers)
    /// </summary>
    public sealed class MeshEntry
    {
        public required string Name { get; init; }
        public required int VertexCount { get; init; }
        public required int IndexCount { get; init; }
        public required int VertexBufferOffset { get; init; }
        public required int IndexBufferOffset { get; init; }
        public required int MaterialIndex { get; init; }
        public required VertexFormat Format { get; init; }
        public uint RawFormat { get; init; }
        
        // Bounding box
        public Vector3 BBoxMin { get; init; }
        public Vector3 BBoxMax { get; init; }
    }
    
    /// <summary>
    /// Format de vertex (basé sur les patterns Level-5 observés)
    /// </summary>
    [Flags]
    public enum VertexFormat : uint
    {
        None = 0,
        Position = 0x00000001,      // float3 (12 bytes)
        Normal = 0x00000008,        // float3 (12 bytes)
        UV0 = 0x00000010,           // float2 (8 bytes)
        UV1 = 0x00000020,           // float2 (8 bytes) - Lightmap UV
        Color = 0x00000040,         // byte4 (4 bytes)
        BoneWeights = 0x00000002,   // byte4 (4 bytes)
        BoneIndices = 0x00000004,   // byte4 (4 bytes)
        Tangent = 0x00000080,       // float3 (12 bytes)
        Binormal = 0x00000100,      // float3 (12 bytes)
        
        // Formats courants
        Static = Position | Normal | UV0,                           // 32 bytes
        Skinned = Position | Normal | UV0 | BoneWeights | BoneIndices, // 40 bytes
        Full = Position | Normal | UV0 | UV1 | Tangent | Binormal,  // 68 bytes
        Level5_24Byte = 0x10000000, // Custom: Pos(12)+BoneIdx(4)+BoneWgt(4)+UV(4)
    }
    
    /// <summary>
    /// Vertex structure (format le plus commun : Position + Normal + UV)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct Vertex
    {
        // Position (12 bytes)
        public readonly float PositionX;
        public readonly float PositionY;
        public readonly float PositionZ;
        
        // Normal (12 bytes)
        public readonly float NormalX;
        public readonly float NormalY;
        public readonly float NormalZ;
        
        // UV (8 bytes)
        public readonly float U;
        public readonly float V;
        
        public Vertex(float px, float py, float pz, float nx, float ny, float nz, float u, float v)
        {
            PositionX = px; PositionY = py; PositionZ = pz;
            NormalX = nx; NormalY = ny; NormalZ = nz;
            U = u; V = v;
        }
        
        // Total: 32 bytes
        
        public Vector3 Position => new(PositionX, PositionY, PositionZ);
        public Vector3 Normal => new(NormalX, NormalY, NormalZ);
        public Vector2 UV => new(U, V);
    }
    
    /// <summary>
    /// Vertex avec skinning (pour personnages animés)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct SkinnedVertex
    {
        public readonly Vertex BaseVertex;  // 32 bytes
        
        // Bone weights (4 bytes)
        public readonly byte Weight0;
        public readonly byte Weight1;
        public readonly byte Weight2;
        public readonly byte Weight3;
        
        // Bone indices (4 bytes)
        public readonly byte BoneIndex0;
        public readonly byte BoneIndex1;
        public readonly byte BoneIndex2;
        public readonly byte BoneIndex3;
        
        // Total: 40 bytes
    }
    
    /// <summary>
    /// Parse le header d'un fichier G4MG
    /// </summary>
    public static G4mgHeader ParseHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < Marshal.SizeOf<G4mgHeader>())
            throw new InvalidDataException($"Data too short for G4MG header (need {Marshal.SizeOf<G4mgHeader>()} bytes)");
        
        // Read magic to detect endianness
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        bool isBigEndian = magic == MAGIC_BE;
        
        if (magic != MAGIC_LE && magic != MAGIC_BE)
            throw new InvalidDataException($"Invalid G4MG magic: 0x{magic:X8}");

        var header = new G4mgHeader();
        header.Magic = magic;
        
        // Read fields with correct endianness
        if (isBigEndian)
        {
            header.HeaderSize = BinaryPrimitives.ReadInt16BigEndian(data.Slice(4));
            header.FileType = BinaryPrimitives.ReadInt16BigEndian(data.Slice(6));
            header.Version = BinaryPrimitives.ReadInt32BigEndian(data.Slice(8));
            header.ContentSize = BinaryPrimitives.ReadInt32BigEndian(data.Slice(12));
            // Reserved1 (16)
            // Reserved2 (24)
            header.MeshCount = BinaryPrimitives.ReadInt32BigEndian(data.Slice(32));
            header.SubMeshCount = BinaryPrimitives.ReadInt16BigEndian(data.Slice(36));
            header.VertexFormatVersion = BinaryPrimitives.ReadInt16BigEndian(data.Slice(38));
            header.Unknown1 = BinaryPrimitives.ReadInt16BigEndian(data.Slice(40));
            header.Unknown2 = BinaryPrimitives.ReadInt16BigEndian(data.Slice(42));
            // Reserved3 (44)
            // Reserved4 (52)
            // Reserved5 (60)
        }
        else
        {
            header.HeaderSize = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(4));
            header.FileType = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(6));
            header.Version = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8));
            header.ContentSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(12));
            // Reserved1 (16)
            // Reserved2 (24)
            header.MeshCount = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(32));
            header.SubMeshCount = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(36));
            header.VertexFormatVersion = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(38));
            header.Unknown1 = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(40));
            header.Unknown2 = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(42));
            // Reserved3 (44)
            // Reserved4 (52)
            // Reserved5 (60)
        }
        
        return header;
    }
    
    /// <summary>
    /// Parse le header depuis un fichier
    /// </summary>
    public static G4mgHeader ParseHeaderFromFile(string filePath)
    {
        Span<byte> headerBuffer = stackalloc byte[Marshal.SizeOf<G4mgHeader>()];
        
        using var fs = File.OpenRead(filePath);
        var bytesRead = fs.Read(headerBuffer);
        
        if (bytesRead < headerBuffer.Length)
            throw new InvalidDataException($"File too short: {filePath}");
        
        return ParseHeader(headerBuffer);
    }
    
    /// <summary>
    /// Parse tous les meshes d'un fichier G4MG
    /// </summary>
    public static List<MeshEntry> ParseMeshes(string filePath)
    {
        var data = File.ReadAllBytes(filePath);
        return ParseMeshes(data);
    }
    
    /// <summary>
    /// Parse tous les meshes depuis un buffer
    /// </summary>
    public static List<MeshEntry> ParseMeshes(ReadOnlySpan<byte> data)
    {
        var header = ParseHeader(data);
        
        if (!header.IsValid)
            throw new InvalidDataException($"Invalid G4MG magic: 0x{header.Magic:X8}");

        var meshes = new List<MeshEntry>(header.MeshCount);
        
        // Offset après le header
        int offset = Marshal.SizeOf<G4mgHeader>();
        
        for (int i = 0; i < header.MeshCount; i++)
        {
            // Lire la table des meshes (format simplifié pour POC)
            // Dans le vrai format, il y a une table d'offsets puis les données
            
            // TODO: Implémenter le parsing complet basé sur le reverse engineering
            // Pour l'instant, structure de base pour le viewer Vulkan
            
            var mesh = new MeshEntry
            {
                Name = $"mesh_{i:D3}",
                VertexCount = 0,     // À parser
                IndexCount = 0,      // À parser
                VertexBufferOffset = 0,
                IndexBufferOffset = 0,
                MaterialIndex = 0,
                Format = VertexFormat.Static,
                BBoxMin = new Vector3(0, 0, 0),
                BBoxMax = new Vector3(0, 0, 0)
            };
            
            meshes.Add(mesh);
        }
        
        return meshes;
    }

    /// <summary>
    /// Parse raw geometry data using metadata from G4MD
    /// </summary>
    public static List<MeshEntry> ParseRawGeometry(ReadOnlySpan<byte> g4mgData, List<G4mdParser.Submesh> submeshes)
    {
        var meshes = new List<MeshEntry>();
        
        foreach (var submesh in submeshes)
        {
            // Skip dummy meshes (VertexCount <= 1)
            if (submesh.VertexCount <= 1)
                continue;

            // Map G4MD format to G4MG format
            // 0x00440105 -> Skinned?
            // 0x00000001 -> Static?
            
            var format = VertexFormat.Static;
            if ((submesh.VertexFormat & 0x4) != 0) // Has BoneIndices
            {
                format = VertexFormat.Skinned;
            }
            
            // Check for 24-byte format (0x00440105)
            // Also check if it has BoneIndices but NOT Normal (0x8)
            if ((submesh.VertexFormat & 0x4) != 0 && (submesh.VertexFormat & 0x8) == 0)
            {
                 format = VertexFormat.Level5_24Byte;
            }

            var mesh = new MeshEntry
            {
                Name = submesh.Name,
                VertexCount = submesh.VertexCount,
                IndexCount = submesh.IndexBufferSize / 2, // Assume ushort indices
                VertexBufferOffset = submesh.VertexBufferOffset,
                IndexBufferOffset = submesh.IndexBufferOffset,
                MaterialIndex = submesh.MaterialIndex,
                Format = format,
                RawFormat = submesh.VertexFormat,
                BBoxMin = new Vector3(0, 0, 0),
                BBoxMax = new Vector3(0, 0, 0)
            };
            
            meshes.Add(mesh);
        }
        
        return meshes;
    }
    
    /// <summary>
    /// Lit les vertices d'un mesh (format static standard)
    /// </summary>
    public static Vertex[] ReadStaticVertices(ReadOnlySpan<byte> data, int offset, int count)
    {
        var vertexSize = Marshal.SizeOf<Vertex>();
        var vertexData = data.Slice(offset, count * vertexSize);
        
        return MemoryMarshal.Cast<byte, Vertex>(vertexData).ToArray();
    }
    
    /// <summary>
    /// Lit les vertices avec skinning
    /// </summary>
    public static SkinnedVertex[] ReadSkinnedVertices(ReadOnlySpan<byte> data, int offset, int count)
    {
        var vertexSize = Marshal.SizeOf<SkinnedVertex>();
        var vertexData = data.Slice(offset, count * vertexSize);
        
        return MemoryMarshal.Cast<byte, SkinnedVertex>(vertexData).ToArray();
    }
    
    /// <summary>
    /// Lit les indices (uint16)
    /// </summary>
    public static ushort[] ReadIndices(ReadOnlySpan<byte> data, int offset, int count)
    {
        var indexData = data.Slice(offset, count * sizeof(ushort));
        return MemoryMarshal.Cast<byte, ushort>(indexData).ToArray();
    }
    
    /// <summary>
    /// Exporte un mesh au format OBJ (pour debug/visualisation)
    /// </summary>
    public static void ExportToObj(MeshEntry mesh, Vertex[] vertices, ushort[] indices, string outputPath)
    {
        using var writer = new StreamWriter(outputPath);
        
        writer.WriteLine($"# G4MG Mesh: {mesh.Name}");
        writer.WriteLine($"# Vertices: {vertices.Length}");
        writer.WriteLine($"# Faces: {indices.Length / 3}");
        writer.WriteLine();
        
        // Vertices
        foreach (var v in vertices)
        {
            writer.WriteLine($"v {v.PositionX} {v.PositionY} {v.PositionZ}");
        }
        
        writer.WriteLine();
        
        // Normals
        foreach (var v in vertices)
        {
            writer.WriteLine($"vn {v.NormalX} {v.NormalY} {v.NormalZ}");
        }
        
        writer.WriteLine();
        
        // UVs
        foreach (var v in vertices)
        {
            writer.WriteLine($"vt {v.U} {v.V}");
        }
        
        writer.WriteLine();
        
        // Faces (OBJ is 1-indexed)
        for (int i = 0; i < indices.Length; i += 3)
        {
            var i0 = indices[i] + 1;
            var i1 = indices[i + 1] + 1;
            var i2 = indices[i + 2] + 1;
            writer.WriteLine($"f {i0}/{i0}/{i0} {i1}/{i1}/{i1} {i2}/{i2}/{i2}");
        }
    }
    
    /// <summary>
    /// Calcule le stride du vertex buffer selon le format
    /// </summary>
    public static int GetVertexStride(VertexFormat format)
    {
        int stride = 0;
        
        if (format.HasFlag(VertexFormat.Position)) stride += 12;
        if (format.HasFlag(VertexFormat.Normal)) stride += 12;
        if (format.HasFlag(VertexFormat.UV0)) stride += 8;
        if (format.HasFlag(VertexFormat.UV1)) stride += 8;
        if (format.HasFlag(VertexFormat.Color)) stride += 4;
        if (format.HasFlag(VertexFormat.BoneWeights)) stride += 4;
        if (format.HasFlag(VertexFormat.BoneIndices)) stride += 4;
        if (format.HasFlag(VertexFormat.Tangent)) stride += 12;
        if (format.HasFlag(VertexFormat.Binormal)) stride += 12;
        
        return stride;
    }
}
