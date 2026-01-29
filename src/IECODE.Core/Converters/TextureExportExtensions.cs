using System;
using System.IO;
using IECODE.Core.Formats.Level5;
using IECODE.Core.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace IECODE.Core.Converters;

public static class TextureExportExtensions
{
    /// <summary>
    /// Exports a G4TX texture to a PNG file.
    /// </summary>
    public static void SaveAsPng(this G4txTexture texture, string outputPath)
    {
        if (texture.IsDds)
        {
            using var image = TextureConverter.LoadDdsToImage(texture.TextureData.Span);
            image.SaveAsPng(outputPath);
            return;
        }

        // 1. Extract NXTCH data (skip header)
        var nxtchBody = G4txParser.ExtractNxtchTextureData(texture.TextureData.Span);

        // 2. Determine format
        var format = TextureConverter.MapG4txFormat(texture.Format);

        if (format == TextureFormat.Unknown)
        {
             throw new NotSupportedException($"Unsupported G4TX texture format: 0x{texture.Format:X}");
        }

        var pngBytes = TextureConverter.ConvertToPng(
            nxtchBody.Span,
            texture.Width,
            texture.Height,
            format
        );

        File.WriteAllBytes(outputPath, pngBytes);
    }

    /// <summary>
    /// Exports a G4TX texture to a WebP file.
    /// </summary>
    public static void SaveAsWebp(this G4txTexture texture, string outputPath)
    {
        if (texture.IsDds)
        {
            using var ddsImage = TextureConverter.LoadDdsToImage(texture.TextureData.Span);
            ddsImage.SaveAsWebp(outputPath);
            return;
        }

        var nxtchBody = G4txParser.ExtractNxtchTextureData(texture.TextureData.Span);
        var format = TextureConverter.MapG4txFormat(texture.Format);

        if (format == TextureFormat.Unknown)
        {
             throw new NotSupportedException($"Unsupported G4TX texture format: 0x{texture.Format:X}");
        }

        var pixels = TextureConverter.DecompressToRgba32(
            nxtchBody.Span,
            texture.Width,
            texture.Height,
            format
        );

        using var image = Image.LoadPixelData<Rgba32>(pixels, texture.Width, texture.Height);
        image.SaveAsWebp(outputPath);
    }
}
