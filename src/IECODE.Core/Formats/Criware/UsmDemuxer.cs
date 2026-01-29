using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IECODE.Core.Formats.Criware;

public class UsmDemuxer
{
    private const uint CRID_MAGIC = 0x43524944; // CRID
    private const uint SFV_MAGIC = 0x40534656;  // @SFV
    private const uint SFA_MAGIC = 0x40534641;  // @SFA
    private const uint ALP_MAGIC = 0x40414C50;  // @ALP
    private const uint SBT_MAGIC = 0x40534254;  // @SBT
    private const uint CUE_MAGIC = 0x40435545;  // @CUE
    private const uint UTF_MAGIC = 0x40555446;  // @UTF

    public static void Demux(string inputPath, string outputDir)
    {
        using var fs = File.OpenRead(inputPath);
        using var reader = new BinaryReader(fs);

        if (fs.Length < 8)
            throw new InvalidDataException("File too small");

        uint magic = BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4));
        if (magic != CRID_MAGIC)
            throw new InvalidDataException("Invalid USM file (CRID signature missing)");

        // Read CRID size
        int cridSize = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
        
        // Skip CRID payload
        fs.Seek(cridSize, SeekOrigin.Current);

        // Prepare output streams
        var streams = new Dictionary<uint, FileStream>();
        
        try
        {
            while (fs.Position < fs.Length)
            {
                if (fs.Length - fs.Position < 8) break;

                byte[] header = reader.ReadBytes(8);
                uint signature = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
                int size = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4, 4));

                if (size < 0 || fs.Position + size > fs.Length)
                {
                    Console.WriteLine($"Warning: Invalid chunk size {size} at {fs.Position - 8}");
                    break;
                }

                byte[] payload = reader.ReadBytes(size);

                // Check for @UTF metadata
                bool isMetadata = false;
                if (payload.Length >= 24 + 4)
                {
                    uint potentialUtf = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(24, 4));
                    if (potentialUtf == UTF_MAGIC)
                    {
                        isMetadata = true;
                    }
                }
                
                // Also check for #HEADER END
                if (payload.Length >= 0x10)
                {
                     string start = Encoding.ASCII.GetString(payload.AsSpan(0, Math.Min(payload.Length, 16)));
                     if (start.Contains("#HEADER")) isMetadata = true;
                }

                if (!isMetadata)
                {
                    if (!streams.ContainsKey(signature))
                    {
                        string ext = signature switch
                        {
                            SFV_MAGIC => "h264", // H.264 detected
                            SFA_MAGIC => "hca",  // HCA detected
                            ALP_MAGIC => "alp",
                            SBT_MAGIC => "sbt",
                            CUE_MAGIC => "cue",
                            _ => "bin"
                        };
                        
                        string filename = $"{Path.GetFileNameWithoutExtension(inputPath)}_{signature:X}.{ext}";
                        string path = Path.Combine(outputDir, filename);
                        streams[signature] = File.Create(path);
                    }

                    // Strip 0x18 bytes header from video and audio payloads
                    if ((signature == SFV_MAGIC || signature == SFA_MAGIC) && payload.Length > 0x18)
                    {
                        streams[signature].Write(payload.AsSpan(0x18));
                    }
                    else
                    {
                        streams[signature].Write(payload);
                    }
                }
            }
        }
        finally
        {
            foreach (var stream in streams.Values)
            {
                stream.Dispose();
            }
        }
    }
}
