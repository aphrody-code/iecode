using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using IECODE.Core.Formats.Level5;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;

namespace IECODE.Core.Converters;

public static class ModelExportExtensions
{
    /// <summary>
    /// Exports a G4MG mesh to a GLB file.
    /// </summary>
    public static void SaveAsGlb(this G4mgParser.MeshEntry mesh, string sourceFilePath, string outputPath)
    {
        // 1. Read Buffers
        var vertexBytes = G4mgReader.ReadVertexBuffer(sourceFilePath, mesh);
        var indexBytes = G4mgReader.ReadIndexBuffer(sourceFilePath, mesh);

        // 2. Parse Data
        var vertices = G4mgReader.ParseVertices(vertexBytes, mesh.Format);
        var indices = G4mgReader.ParseIndices(indexBytes);

        // 3. Create Material
        var material = new MaterialBuilder($"{mesh.Name}_Mat")
            .WithDoubleSide(true)
            .WithMetallicRoughnessShader();

        // 4. Create MeshBuilder
        // We use VertexPositionNormal and VertexTexture1 (UVs)
        var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1>(mesh.Name);
        var primitive = meshBuilder.UsePrimitive(material);

        // 5. Add Triangles
        for (int i = 0; i < indices.Length; i += 3)
        {
            var i0 = indices[i];
            var i1 = indices[i + 1];
            var i2 = indices[i + 2];

            if (i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
                continue;

            var v0 = vertices[i0];
            var v1 = vertices[i1];
            var v2 = vertices[i2];

            primitive.AddTriangle(
                (new VertexPositionNormal(ToNumerics(v0.Position), ToNumerics(v0.Normal)), new VertexTexture1(ToNumerics(v0.UV))),
                (new VertexPositionNormal(ToNumerics(v1.Position), ToNumerics(v1.Normal)), new VertexTexture1(ToNumerics(v1.UV))),
                (new VertexPositionNormal(ToNumerics(v2.Position), ToNumerics(v2.Normal)), new VertexTexture1(ToNumerics(v2.UV)))
            );
        }

        // 6. Create Scene & Save
        var model = ModelRoot.CreateModel();
        model.CreateMeshes(meshBuilder);
        model.SaveGLB(outputPath);
    }

    /// <summary>
    /// Exports multiple meshes to a single GLB file.
    /// </summary>
    public static void SaveAllAsGlb(this List<G4mgParser.MeshEntry> meshes, string sourceFilePath, string outputPath)
    {
        var model = ModelRoot.CreateModel();
        var scene = model.UseScene("Default");

        // Refined approach:
        // We can't easily modify the model *after* CreateMeshes in a loop if we want to use the Builder pattern cleanly.
        // Better to collect all MeshBuilders first.
        
        var meshBuilders = new List<MeshBuilder<VertexPositionNormal, VertexTexture1>>();
        
        foreach (var mesh in meshes)
        {
             var vertexBytes = G4mgReader.ReadVertexBuffer(sourceFilePath, mesh);
            var indexBytes = G4mgReader.ReadIndexBuffer(sourceFilePath, mesh);
            var vertices = G4mgReader.ParseVertices(vertexBytes, mesh.Format);
            var indices = G4mgReader.ParseIndices(indexBytes);

            var material = new MaterialBuilder($"{mesh.Name}_Mat")
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader();

            var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1>(mesh.Name);
            var primitive = meshBuilder.UsePrimitive(material);

            for (int i = 0; i < indices.Length; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                // Skip invalid indices or degenerate triangles
                if (i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
                {
                    // Console.WriteLine($"Skipping triangle with invalid indices: {i0}, {i1}, {i2} (VertexCount: {vertices.Length})");
                    continue;
                }

                var v0 = vertices[i0];
                var v1 = vertices[i1];
                var v2 = vertices[i2];

                primitive.AddTriangle(
                    (new VertexPositionNormal(ToNumerics(v0.Position), ToNumerics(v0.Normal)), new VertexTexture1(ToNumerics(v0.UV))),
                    (new VertexPositionNormal(ToNumerics(v1.Position), ToNumerics(v1.Normal)), new VertexTexture1(ToNumerics(v1.UV))),
                    (new VertexPositionNormal(ToNumerics(v2.Position), ToNumerics(v2.Normal)), new VertexTexture1(ToNumerics(v2.UV)))
                );
            }
            meshBuilders.Add(meshBuilder);
        }

        model.CreateMeshes(meshBuilders.ToArray());
        model.SaveGLB(outputPath);
    }

    private static System.Numerics.Vector3 ToNumerics(G4mgParser.Vector3 v) => new(v.X, v.Y, v.Z);
    private static System.Numerics.Vector2 ToNumerics(G4mgParser.Vector2 v) => new(v.X, v.Y);
}
