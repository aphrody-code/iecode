using System.CommandLine;
using IECODE.Core.Converters;

namespace IECODE.CLI.Commands;

public static class ConvertCommand
{
    public static Command Create()
    {
        var command = new Command("convert", "Convert game assets to standard formats (PNG, GLB, MP4)");

        var inputArgument = new Argument<string>("input", "Input file or directory path");
        command.AddArgument(inputArgument);

        var outputOption = new Option<string?>(
            aliases: ["--output", "-o"],
            description: "Output directory (default: same as input)")
        {
            IsRequired = false
        };
        command.AddOption(outputOption);

        var recursiveOption = new Option<bool>(
            aliases: ["--recursive", "-r"],
            description: "Process directories recursively");
        command.AddOption(recursiveOption);

        var formatOption = new Option<string?>(
            aliases: ["--format", "-f"],
            description: "Target format (png, webp, glb, mp4, webm)");
        command.AddOption(formatOption);

        command.SetHandler(async (string input, string? output, bool recursive, string? format) =>
        {
            var converter = new AssetConverterFacade();
            
            // Determine output path if not specified
            if (string.IsNullOrEmpty(output))
            {
                // If input is a directory, output to same directory (in-place)
                // If input is a file, output to parent directory
                output = Directory.Exists(input) ? input : (Path.GetDirectoryName(input) ?? ".");
            }

            if (Directory.Exists(input))
            {
                Console.WriteLine($"Converting directory: {input} -> {output}");
                var results = await converter.ConvertDirectoryAsync(input, output, recursive, format);
                Console.WriteLine($"Converted {results.Count} files.");
                foreach (var result in results)
                {
                    Console.WriteLine($"  {result}");
                }
            }
            else if (File.Exists(input))
            {
                Console.WriteLine($"Converting file: {input} -> {output}");
                var result = await converter.ConvertFileAsync(input, output);
                if (result != null)
                {
                    Console.WriteLine($"  Success: {result}");
                }
                else
                {
                    Console.WriteLine($"  Skipped or failed: {input}");
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: Input path not found: {input}");
            }

        }, inputArgument, outputOption, recursiveOption, formatOption);

        return command;
    }
}
