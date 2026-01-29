using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using IECODE.Core.Formats.Level5;

namespace IECODE.Core.Formats.Level5;

/// <summary>
/// High-performance reader for G4MG geometry data.
/// Handles reading Vertex and Index buffers using AOT-safe techniques.
/// </summary>
public static class G4mgReader
{
    /// <summary>
    /// Reads the raw vertex buffer for a specific mesh.
    /// </summary>
    public static byte[] ReadVertexBuffer(string filePath, G4mgParser.MeshEntry mesh)
    {
        using var fs = File.OpenRead(filePath);
        return ReadVertexBuffer(fs, mesh);
    }

    /// <summary>
    /// Reads the raw vertex buffer from a stream.
    /// </summary>
    public static byte[] ReadVertexBuffer(Stream stream, G4mgParser.MeshEntry mesh)
    {
        int stride = G4mgParser.GetVertexStride(mesh.Format);
        int bufferSize = mesh.VertexCount * stride;
        
        Console.WriteLine($"Reading Vertex Buffer: Offset={mesh.VertexBufferOffset}, Count={mesh.VertexCount}, Stride={stride}, Size={bufferSize}, StreamLength={stream.Length}");

        if (mesh.VertexBufferOffset + bufferSize > stream.Length)
        {
            Console.WriteLine($"Error: Vertex buffer out of bounds! End={mesh.VertexBufferOffset + bufferSize}, StreamLength={stream.Length}");
            // Return empty or throw?
        }

        byte[] buffer = new byte[bufferSize];
        
        stream.Seek(mesh.VertexBufferOffset, SeekOrigin.Begin);
        int bytesRead = stream.Read(buffer, 0, bufferSize);
        
        if (bytesRead != bufferSize)
        {
            // It's possible the file is truncated or our offset logic is slightly off
            // For now, we warn but return what we have
            Console.WriteLine($"Warning: Expected {bufferSize} bytes for vertex buffer, got {bytesRead}");
        }
        
        return buffer;
    }

    /// <summary>
    /// Reads the raw index buffer for a specific mesh.
    /// </summary>
    public static byte[] ReadIndexBuffer(string filePath, G4mgParser.MeshEntry mesh)
    {
        using var fs = File.OpenRead(filePath);
        return ReadIndexBuffer(fs, mesh);
    }

    /// <summary>
    /// Reads the raw index buffer from a stream.
    /// </summary>
    public static byte[] ReadIndexBuffer(Stream stream, G4mgParser.MeshEntry mesh)
    {
        // Indices are usually ushort (2 bytes)
        int bufferSize = mesh.IndexCount * 2;
        
        Console.WriteLine($"Reading Index Buffer: Offset={mesh.IndexBufferOffset}, Count={mesh.IndexCount}, Size={bufferSize}, StreamLength={stream.Length}");

        if (mesh.IndexBufferOffset + bufferSize > stream.Length)
        {
             Console.WriteLine($"Error: Index buffer out of bounds! End={mesh.IndexBufferOffset + bufferSize}, StreamLength={stream.Length}");
        }

        byte[] buffer = new byte[bufferSize];
        
        stream.Seek(mesh.IndexBufferOffset, SeekOrigin.Begin);
        int bytesRead = stream.Read(buffer, 0, bufferSize);
        
        if (bytesRead != bufferSize)
        {
            Console.WriteLine($"Warning: Expected {bufferSize} bytes for index buffer, got {bytesRead}");
        }
        
        return buffer;
    }

    /// <summary>
    /// Parses vertices into structured data.
    /// </summary>
    public static G4mgParser.Vertex[] ParseVertices(ReadOnlySpan<byte> vertexData, G4mgParser.VertexFormat format)
    {
        int stride = G4mgParser.GetVertexStride(format);
        int count = vertexData.Length / stride;
        
        var vertices = new G4mgParser.Vertex[count];
        
        for (int i = 0; i < count; i++)
        {
            var slice = vertexData.Slice(i * stride, stride);
            
            // Read Position (always present)
            float px = BinaryPrimitives.ReadSingleLittleEndian(slice);
            float py = BinaryPrimitives.ReadSingleLittleEndian(slice.Slice(4));
            float pz = BinaryPrimitives.ReadSingleLittleEndian(slice.Slice(8));
            
            // Sanitize Position
            if (!float.IsFinite(px)) px = 0;
            if (!float.IsFinite(py)) py = 0;
            if (!float.IsFinite(pz)) pz = 0;
            
            // Read Normal (if present)
            float nx = 0, ny = 0, nz = 1; // Default to valid normal
            int offset = 12;
            
            if (format.HasFlag(G4mgParser.VertexFormat.Normal))
            {
                nx = BinaryPrimitives.ReadSingleLittleEndian(slice.Slice(offset));
                ny = BinaryPrimitives.ReadSingleLittleEndian(slice.Slice(offset + 4));
                nz = BinaryPrimitives.ReadSingleLittleEndian(slice.Slice(offset + 8));
                
                // Sanitize Normal
                if (!float.IsFinite(nx)) nx = 0;
                if (!float.IsFinite(ny)) ny = 0;
                if (!float.IsFinite(nz)) nz = 1;
                
                // Normalize
                float lenSq = nx * nx + ny * ny + nz * nz;
                if (lenSq < 1e-6f)
                {
                    nx = 0; ny = 0; nz = 1;
                }
                else
                {
                    float len = MathF.Sqrt(lenSq);
                    nx /= len;
                    ny /= len;
                    nz /= len;
                }
                
                offset += 12;
            }
            
            // Read UV0 (if present)
            float u = 0, v = 0;
            if (format.HasFlag(G4mgParser.VertexFormat.UV0))
            {
                u = BinaryPrimitives.ReadSingleLittleEndian(slice.Slice(offset));
                v = BinaryPrimitives.ReadSingleLittleEndian(slice.Slice(offset + 4));
                
                // Sanitize
                if (!float.IsFinite(u)) u = 0;
                if (!float.IsFinite(v)) v = 0;
                
                offset += 8;
            }
            
            // Construct vertex
            vertices[i] = new G4mgParser.Vertex(px, py, pz, nx, ny, nz, u, v);
        }
        
        return vertices;
    }
    
    /// <summary>
    /// Parses indices into ushort array.
    /// </summary>
    public static ushort[] ParseIndices(ReadOnlySpan<byte> indexData)
    {
        var indices = MemoryMarshal.Cast<byte, ushort>(indexData).ToArray();
        
        // Debug: check max index
        if (indices.Length > 0)
        {
            ushort max = 0;
            ushort min = 65535;
            foreach (var idx in indices)
            {
                if (idx > max) max = idx;
                if (idx < min) min = idx;
            }
            Console.WriteLine($"Parsed {indices.Length} indices. Min={min}, Max={max}");
        }
        
        return indices;
    }
}
