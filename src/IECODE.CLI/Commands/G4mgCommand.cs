using System.Runtime.InteropServices;
using System.Buffers.Binary;
using System.CommandLine;
using System.Numerics;
using IECODE.Core.Formats.Level5;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace IECODE.CLI.Commands;

/// <summary>
/// Command to parse and convert G4MG (Level-5 Mesh Geometry) files.
/// </summary>
public static class G4mgCommand
{
    public static Command Create()
    {
        var command = new Command("g4mg", "Parse and convert G4MG geometry files");

        // Subcommand: to-glb
        var toGlbCommand = new Command("to-glb", "Convert G4MG to GLB");
        var fileArg = new Argument<string>("file", "Path to G4MG file");
        toGlbCommand.AddArgument(fileArg);
        var outputOption = new Option<string?>(["--output", "-o"], "Output GLB file");
        toGlbCommand.AddOption(outputOption);

        toGlbCommand.SetHandler((string file, string? output) =>
        {
            ConvertToGlb(file, output);
        }, fileArg, outputOption);

        command.AddCommand(toGlbCommand);

        return command;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Vertex24
    {
        public float PositionX, PositionY, PositionZ;
        public byte BoneIndex0, BoneIndex1, BoneIndex2, BoneIndex3;
        public byte Weight0, Weight1, Weight2, Weight3;
        public ushort U, V;
    }

    private static void ConvertToGlb(string filePath, string? outputPath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ File not found: {filePath}");
                return;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.ChangeExtension(filePath, ".glb");
            }

            Console.WriteLine($"📦 Converting: {Path.GetFileName(filePath)} -> {Path.GetFileName(outputPath)}");

            byte[] fileData = File.ReadAllBytes(filePath);

            // Check for encryption (Magic: 0x0CA3D70A)
            if (fileData.Length > 4)
            {
                uint magic = BinaryPrimitives.ReadUInt32LittleEndian(fileData);
                if (magic == 0x0CA3D70A)
                {
                    Console.WriteLine("🔒 Detected encrypted G4MG file (Magic: 0x0CA3D70A). Decrypting...");
                    // Key derived from 0x0CA3D70A ^ 0x474D3447 (G4MG)
                    uint key = 0x4BEEE34D;
                    var keyBytes = BitConverter.GetBytes(key);
                    
                    // Decrypt header only? Or check if body is encrypted?
                    // Analysis shows body at 0x600 is 00 00 00 00 02 00 00 00 (valid data).
                    // If we decrypt it, it becomes garbage (Key).
                    // So likely only the header is encrypted.
                    // Let's decrypt first 0x40 bytes (Header Size).
                    
                    int decryptSize = 0x40;
                    for (int i = 0; i < decryptSize; i++)
                    {
                        fileData[i] ^= keyBytes[i % 4];
                    }
                    Console.WriteLine("🔓 Decryption complete (Header only). Magic is now: 0x" + BinaryPrimitives.ReadUInt32LittleEndian(fileData).ToString("X8"));
                }
            }
            
            Console.WriteLine("DEBUG: Starting parsing...");

            List<G4mgParser.MeshEntry> meshes;

            // Check for companion G4MD file
            string g4mdPath = Path.ChangeExtension(filePath, ".g4md");
            if (File.Exists(g4mdPath))
            {
                Console.WriteLine($"ℹ️ Found companion G4MD file: {Path.GetFileName(g4mdPath)}");
                var g4md = new G4mdParser();
                g4md.Parse(g4mdPath);
                Console.WriteLine(g4md.GetSummary());
                
                // Use G4MD to parse G4MG structure
                Console.WriteLine("DEBUG: Parsing raw geometry...");
                meshes = G4mgParser.ParseRawGeometry(fileData, g4md.Submeshes);
                Console.WriteLine($"DEBUG: Parsed {meshes.Count} meshes.");

                // Fixup formats based on heuristics
                var fixedMeshes = new List<G4mgParser.MeshEntry>();
                foreach (var m in meshes)
                {
                    var format = m.Format;
                    // Force 24-byte format if RawFormat is 0 and BoneCount > 0
                    if (m.RawFormat == 0 && g4md.Header.BoneCount > 0)
                    {
                        format = G4mgParser.VertexFormat.Level5_24Byte;
                    }
                    // Also force 24-byte for 0x02020000 (Face mesh?)
                    if (m.RawFormat == 0x02020000)
                    {
                        format = G4mgParser.VertexFormat.Level5_24Byte;
                    }
                    
                    fixedMeshes.Add(new G4mgParser.MeshEntry
                    {
                        Name = m.Name,
                        VertexCount = m.VertexCount,
                        IndexCount = m.IndexCount,
                        VertexBufferOffset = m.VertexBufferOffset,
                        IndexBufferOffset = m.IndexBufferOffset,
                        MaterialIndex = m.MaterialIndex,
                        Format = format,
                        RawFormat = m.RawFormat,
                        BBoxMin = m.BBoxMin,
                        BBoxMax = m.BBoxMax
                    });
                }
                meshes = fixedMeshes;

                // Robust Scan for Index Buffer
                // Find a block of size (IndexCount * 2) where all ushorts are < VertexCount
                int ibFoundOffset = -1;
                if (meshes.Count > 0)
                {
                    var m0 = meshes[0];
                    int requiredSize = m0.IndexCount * 2;
                    int maxVertexIndex = m0.VertexCount;
                    
                    Console.WriteLine($"DEBUG: Scanning for IB (Size: {requiredSize}, MaxVal: {maxVertexIndex})...");

                    // Scan with stride 4 (alignment)
                    for (int i = 0x600; i < fileData.Length - requiredSize; i += 4)
                    {
                        bool valid = true;
                        // Check first 10 indices to fail fast
                        for (int j = 0; j < 20; j += 2)
                        {
                            ushort val = BitConverter.ToUInt16(fileData, i + j);
                            if (val >= maxVertexIndex)
                            {
                                valid = false;
                                break;
                            }
                        }
                        
                        if (valid)
                        {
                            // Check ALL indices
                            for (int j = 0; j < requiredSize; j += 2)
                            {
                                ushort val = BitConverter.ToUInt16(fileData, i + j);
                                if (val >= maxVertexIndex)
                                {
                                    valid = false;
                                    break;
                                }
                            }
                        }

                        if (valid)
                        {
                            ibFoundOffset = i;
                            break;
                        }
                    }
                }
                
                if (ibFoundOffset != -1)
                {
                    Console.WriteLine($"✅ Found potential IB at 0x{ibFoundOffset:X}");
                    
                    // Apply IB offset
                    var newMeshes = new List<G4mgParser.MeshEntry>();
                    foreach (var m in meshes)
                    {
                        int newIbOffset = m.IndexBufferOffset;
                        if (m == meshes[0])
                        {
                            newIbOffset = ibFoundOffset;
                        }
                        else if (m == meshes[1])
                        {
                            // Guess Mesh 1 IB is after Mesh 0 IB
                            // Align to 4 bytes
                            int m0Size = meshes[0].IndexCount * 2;
                            int nextOffset = ibFoundOffset + m0Size;
                            if (nextOffset % 4 != 0) nextOffset += 2;
                            newIbOffset = nextOffset;
                        }

                        newMeshes.Add(new G4mgParser.MeshEntry
                        {
                            Name = m.Name,
                            VertexCount = m.VertexCount,
                            IndexCount = m.IndexCount,
                            VertexBufferOffset = m.VertexBufferOffset,
                            IndexBufferOffset = newIbOffset,
                            MaterialIndex = m.MaterialIndex,
                            Format = m.Format,
                            RawFormat = m.RawFormat,
                            BBoxMin = m.BBoxMin,
                            BBoxMax = m.BBoxMax
                        });
                    }
                    meshes = newMeshes;
                }

                // Heuristic scan removed (replaced by global IB scan and format fixup)
            }
            else
            {
                // Fallback to standalone G4MG parsing
                // We need to pass the decrypted data, not read from file again
                meshes = G4mgParser.ParseMeshes(fileData);
            }

            // 2. Create Scene
            var scene = new SceneBuilder();
            
            // Default material
            var material = new MaterialBuilder("Default")
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader();

            foreach (var meshEntry in meshes)
            {
                Console.WriteLine($"  - Processing mesh: {meshEntry.Name} (V: {meshEntry.VertexCount}, I: {meshEntry.IndexCount})");
                Console.WriteLine($"    Format: {meshEntry.Format}");
                Console.WriteLine($"    RawFormat: 0x{meshEntry.RawFormat:X8}");
                Console.WriteLine($"    Offsets: VB 0x{meshEntry.VertexBufferOffset:X}, IB 0x{meshEntry.IndexBufferOffset:X}");
                Console.WriteLine($"    FileData Length: {fileData.Length}");

                if (meshEntry.VertexCount == 0) continue;

                // Read buffers based on format
                if (meshEntry.Format == G4mgParser.VertexFormat.Skinned)
                {
                    G4mgParser.SkinnedVertex[] vertices = [];
                    ushort[] indices = [];
                    
                    // Manual read vertices
                    int vSize = 40;
                    int vCount = meshEntry.VertexCount;
                    int vOffset = meshEntry.VertexBufferOffset;
                    if (vOffset + vCount * vSize > fileData.Length)
                    {
                        Console.WriteLine($"    Vertex Buffer overflow! Need {vOffset + vCount * vSize}, have {fileData.Length}");
                        continue;
                    }
                    var vSpan = fileData.AsSpan(vOffset, vCount * vSize);
                    vertices = MemoryMarshal.Cast<byte, G4mgParser.SkinnedVertex>(vSpan).ToArray();

                    // Manual read indices
                    int iSize = 2;
                    int iCount = meshEntry.IndexCount;
                    int iOffset = meshEntry.IndexBufferOffset;
                    if (iOffset + iCount * iSize > fileData.Length)
                    {
                        Console.WriteLine($"    Index Buffer overflow! Need {iOffset + iCount * iSize}, have {fileData.Length}");
                        continue;
                    }
                    var iSpan = new ReadOnlySpan<byte>(fileData, iOffset, iCount * iSize);
                    indices = MemoryMarshal.Cast<byte, ushort>(iSpan).ToArray();

                    
                    var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>(meshEntry.Name);
                    var primitive = meshBuilder.UsePrimitive(material);
                    
                    var gltfVertices = new (VertexPositionNormal, VertexTexture1, VertexJoints4)[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var v = vertices[i];
                        gltfVertices[i] = (
                            new VertexPositionNormal(v.BaseVertex.PositionX, v.BaseVertex.PositionY, v.BaseVertex.PositionZ, 
                                                   v.BaseVertex.NormalX, v.BaseVertex.NormalY, v.BaseVertex.NormalZ),
                            new VertexTexture1(new Vector2(v.BaseVertex.U, v.BaseVertex.V)),
                            new VertexJoints4(
                                (v.BoneIndex0, v.Weight0 / 255f),
                                (v.BoneIndex1, v.Weight1 / 255f),
                                (v.BoneIndex2, v.Weight2 / 255f),
                                (v.BoneIndex3, v.Weight3 / 255f)
                            )
                        );
                    }
                    
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        if (i + 2 >= indices.Length) break;
                        primitive.AddTriangle(gltfVertices[indices[i]], gltfVertices[indices[i+1]], gltfVertices[indices[i+2]]);
                    }
                    
                    scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
                }
                else if (meshEntry.Format == G4mgParser.VertexFormat.Level5_24Byte)
                {
                    // Manual read 24-byte vertices
                    int vSize = 24;
                    int vCount = meshEntry.VertexCount;
                    int vOffset = meshEntry.VertexBufferOffset;
                    if (vOffset + vCount * vSize > fileData.Length)
                    {
                        Console.WriteLine($"    Vertex Buffer overflow! Need {vOffset + vCount * vSize}, have {fileData.Length}");
                        continue;
                    }
                    var vSpan = fileData.AsSpan(vOffset, vCount * vSize);
                    var vertices = MemoryMarshal.Cast<byte, Vertex24>(vSpan).ToArray();

                    // Manual read indices
                    int iSize = 2;
                    int iCount = meshEntry.IndexCount;
                    int iOffset = meshEntry.IndexBufferOffset;
                    if (iOffset + iCount * iSize > fileData.Length)
                    {
                        Console.WriteLine($"    Index Buffer overflow! Need {iOffset + iCount * iSize}, have {fileData.Length}");
                        continue;
                    }
                    var iSpan = fileData.AsSpan(iOffset, iCount * iSize);
                    var indices = MemoryMarshal.Cast<byte, ushort>(iSpan).ToArray();

                    // Debug indices
                    bool indicesValid = true;
                    if (indices.Length > 0)
                    {
                        ushort maxIdx = 0;
                        foreach(var idx in indices) if(idx > maxIdx) maxIdx = idx;
                        Console.WriteLine($"    Indices: {indices.Length}, Max Index: {maxIdx}, Vertex Count: {vertices.Length}");
                        Console.WriteLine($"    First 10 indices: {string.Join(", ", indices.Take(10))}");
                        
                        if (maxIdx >= vertices.Length)
                        {
                            Console.WriteLine("⚠️ Indices are invalid (Max Index >= Vertex Count). Skipping faces.");
                            indicesValid = false;
                        }
                    }

                    var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>(meshEntry.Name);
                    var primitive = meshBuilder.UsePrimitive(material);

                    var gltfVertices = new (VertexPositionNormal, VertexTexture1, VertexJoints4)[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var v = vertices[i];
                        // Assume Half Float UVs
                        float u = (float)BitConverter.UInt16BitsToHalf(v.U);
                        float v_ = (float)BitConverter.UInt16BitsToHalf(v.V);

                        // Normalize weights
                        float w0 = v.Weight0 / 255f;
                        float w1 = v.Weight1 / 255f;
                        float w2 = v.Weight2 / 255f;
                        float w3 = v.Weight3 / 255f;
                        float sum = w0 + w1 + w2 + w3;
                        if (sum > 0)
                        {
                            w0 /= sum; w1 /= sum; w2 /= sum; w3 /= sum;
                        }
                        else
                        {
                            w0 = 1.0f; w1 = 0; w2 = 0; w3 = 0;
                        }

                        gltfVertices[i] = (
                            new VertexPositionNormal(v.PositionX, v.PositionY, v.PositionZ, 0, 1, 0), // Dummy normal
                            new VertexTexture1(new Vector2(u, v_)),
                            new VertexJoints4(
                                (v.BoneIndex0, w0),
                                (v.BoneIndex1, w1),
                                (v.BoneIndex2, w2),
                                (v.BoneIndex3, w3)
                            )
                        );
                    }

                    if (indicesValid)
                    {
                        for (int i = 0; i < indices.Length; i += 3)
                        {
                            if (i + 2 >= indices.Length) break;
                            primitive.AddTriangle(gltfVertices[indices[i]], gltfVertices[indices[i+1]], gltfVertices[indices[i+2]]);
                        }
                    }
                    else
                    {
                        // Add points for debugging
                        var pointPrim = meshBuilder.UsePrimitive(material, 1); // 1 = POINTS
                        for (int i = 0; i < gltfVertices.Length; i++)
                        {
                            pointPrim.AddPoint(gltfVertices[i]);
                        }
                    }

                    scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
                }
                else
                {
                    // Manual read static
                    int vSize = 32;
                    int vCount = meshEntry.VertexCount;
                    int vOffset = meshEntry.VertexBufferOffset;
                    if (vOffset + vCount * vSize > fileData.Length)
                    {
                        Console.WriteLine($"    Vertex Buffer overflow! Need {vOffset + vCount * vSize}, have {fileData.Length}");
                        continue;
                    }
                    
                    var vSpan = fileData.AsSpan(vOffset, vCount * vSize);
                    var vertices = MemoryMarshal.Cast<byte, G4mgParser.Vertex>(vSpan).ToArray();
                    
                    // Manual read indices
                    int iSize = 2;
                    int iCount = meshEntry.IndexCount;
                    int iOffset = meshEntry.IndexBufferOffset;
                    if (iOffset + iCount * iSize > fileData.Length)
                    {
                        Console.WriteLine($"    Index Buffer overflow! Need {iOffset + iCount * iSize}, have {fileData.Length}");
                        continue;
                    }
                    
                    var iSpan = fileData.AsSpan(iOffset, iCount * iSize);
                    var indices = MemoryMarshal.Cast<byte, ushort>(iSpan).ToArray();

                    var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1>(meshEntry.Name);
                    var primitive = meshBuilder.UsePrimitive(material);

                    var gltfVertices = new (VertexPositionNormal, VertexTexture1)[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var v = vertices[i];
                        gltfVertices[i] = (
                            new VertexPositionNormal(v.PositionX, v.PositionY, v.PositionZ, v.NormalX, v.NormalY, v.NormalZ),
                            new VertexTexture1(new Vector2(v.U, v.V))
                        );
                    }

                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        if (i + 2 >= indices.Length) break;
                        
                        var i0 = indices[i];
                        var i1 = indices[i + 1];
                        var i2 = indices[i + 2];
                        
                        if (i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
                            continue;

                        primitive.AddTriangle(gltfVertices[i0], gltfVertices[i1], gltfVertices[i2]);
                    }
                    
                    scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
                }
            }

            // 3. Save GLB
            var model = scene.ToGltf2();
            model.SaveGLB(outputPath);
            
            Console.WriteLine($"✅ Saved GLB to: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
