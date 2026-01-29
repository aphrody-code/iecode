using System.CommandLine;
using System.Text.Json;
using IECODE.CLI;
using IECODE.Core.Formats.Level5;

namespace IECODE.CLI.Commands;

/// <summary>
/// Command to parse G4MD (Level-5 Model Data) files.
/// </summary>
public static class G4mdCommand
{
    public static Command Create()
    {
        var command = new Command("g4md", "Parse and analyze G4MD model files");

        // Subcommand: info
        var infoCommand = new Command("info", "Display G4MD file information");
        var infoFileArg = new Argument<string>("file", "Path to G4MD file");
        infoCommand.AddArgument(infoFileArg);
        
        var jsonOption = new Option<string?>(["--output", "-o"], "Output JSON file");
        infoCommand.AddOption(jsonOption);

        infoCommand.SetHandler((string file, string? output) =>
        {
            Info(file, output);
        }, infoFileArg, jsonOption);

        command.AddCommand(infoCommand);

        return command;
    }

    private static void Info(string filePath, string? outputJson)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ File not found: {filePath}");
                return;
            }

            Console.WriteLine($"📄 Analyzing: {Path.GetFileName(filePath)}");
            
            var parser = new G4mdParser();
            parser.Parse(filePath);
            
            Console.WriteLine($"G4MD Header:");
            Console.WriteLine($"- Magic: {parser.Header.Magic:X8}");
            Console.WriteLine($"- HeaderSize: {parser.Header.HeaderSize}");
            Console.WriteLine($"- TypeId: {parser.Header.TypeId}");
            Console.WriteLine($"- Unk1: {parser.Header.Unk1}");
            Console.WriteLine($"- Unk2: {parser.Header.Unk2}");
            Console.WriteLine($"- SubmeshCount: {parser.Header.SubmeshCount}");
            Console.WriteLine($"- TotalCount: {parser.Header.TotalCount}");
            
            Console.WriteLine($"\nSubmeshes ({parser.Submeshes.Count}):");
            foreach (var mesh in parser.Submeshes)
            {
                Console.WriteLine($"- {mesh.Name}:");
                Console.WriteLine($"  - IndexCount: {mesh.IndexCount}");
                Console.WriteLine($"  - MaterialIndex: {mesh.MaterialIndex}");
                Console.WriteLine($"  - IndexBufferOffset: {mesh.IndexBufferOffset} (0x{mesh.IndexBufferOffset:X})");
                Console.WriteLine($"  - IndexBufferSize: {mesh.IndexBufferSize} (0x{mesh.IndexBufferSize:X})");
                Console.WriteLine($"  - VertexCount: {mesh.VertexCount}");
                Console.WriteLine($"  - VertexBufferOffset: {mesh.VertexBufferOffset} (0x{mesh.VertexBufferOffset:X})");
            }

            if (outputJson != null)
            {
                var json = JsonSerializer.Serialize(parser.Header, JsonContext.Default.G4mdHeader);
                File.WriteAllText(outputJson, json);
                Console.WriteLine($"✅ Exported header to: {outputJson}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}
