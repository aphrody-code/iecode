using System.CommandLine;
using IECODE.Core.Formats.Level5;

namespace IECODE.CLI.Commands;

public static class G4pkCommand
{
    public static Command Create()
    {
        var command = new Command("g4pk", "Extract G4PK package files");

        var extractCommand = new Command("extract", "Extract files from G4PK");
        var fileArg = new Argument<string>("file", "Path to G4PK file");
        extractCommand.AddArgument(fileArg);
        
        var outputOption = new Option<string>(
            aliases: ["--output", "-o"],
            description: "Output directory")
        {
            IsRequired = true
        };
        extractCommand.AddOption(outputOption);

        extractCommand.SetHandler((string file, string output) =>
        {
            Extract(file, output);
        }, fileArg, outputOption);

        command.AddCommand(extractCommand);
        
        return command;
    }

    private static void Extract(string filePath, string outputDir)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ File not found: {filePath}");
                return;
            }

            Console.WriteLine($"📦 Extracting: {Path.GetFileName(filePath)}");
            var entries = G4pkParser.ParseFile(filePath);
            
            Directory.CreateDirectory(outputDir);
            
            foreach (var entry in entries)
            {
                string outputPath = Path.Combine(outputDir, entry.Name);
                File.WriteAllBytes(outputPath, entry.Data.ToArray());
                Console.WriteLine($"  📄 Extracted: {entry.Name} ({entry.Size:N0} bytes)");
            }
            
            Console.WriteLine($"✅ Extracted {entries.Count} files.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}
