using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using IECODE.CLI.Commands;

namespace IECODE.CLI;

/// <summary>
/// Point d'entrée CLI pour IECODE.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("IECODE - Inazuma Eleven Victory Road Reverse Engineering Toolkit")
        {
            Name = "iecode"
        };

        // Global options
        var gamePathOption = new Option<string?>(
            aliases: ["--game", "-g"],
            description: "Path to IEVR game directory (default: Steam install path)")
        {
            IsRequired = false
        };
        rootCommand.AddGlobalOption(gamePathOption);

        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Enable verbose output");
        rootCommand.AddGlobalOption(verboseOption);

        // Add commands
        rootCommand.AddCommand(CreateInfoCommand(gamePathOption, verboseOption));
        rootCommand.AddCommand(CreateDumpCommand(gamePathOption, verboseOption));
        rootCommand.AddCommand(CreateExtractCommand(gamePathOption, verboseOption));
        rootCommand.AddCommand(CreatePackCommand(gamePathOption, verboseOption));
        rootCommand.AddCommand(CreateConfigCommand(gamePathOption, verboseOption));
        rootCommand.AddCommand(CreateCryptoCommand(verboseOption));
        
        // Memory command is Windows-only (P/Invoke for process memory access)
        if (OperatingSystem.IsWindows())
        {
            rootCommand.AddCommand(CreateMemoryCommand(verboseOption));
            rootCommand.AddCommand(CreateTechniqueCommand(verboseOption));
            rootCommand.AddCommand(CreateTraceCommand(verboseOption));
        }
        
        rootCommand.AddCommand(CreateAnalyzeCommand(gamePathOption, verboseOption));
        rootCommand.AddCommand(CreatePipelineCommand(gamePathOption, verboseOption));
        rootCommand.AddCommand(BenchmarkCommand.Create());
        rootCommand.AddCommand(G4txCommand.Create());
        rootCommand.AddCommand(G4pkCommand.Create());
        rootCommand.AddCommand(G4mdCommand.Create());
        rootCommand.AddCommand(G4mgCommand.Create());
        rootCommand.AddCommand(UtfCommand.Create());
        rootCommand.AddCommand(SearchCommand.Create());
        rootCommand.AddCommand(DumpGameDataCommand.Create());
        rootCommand.AddCommand(GenerateGameDataClassesCommand.Create());
        rootCommand.AddCommand(ConvertCommand.Create());
        rootCommand.AddCommand(FormatCommand.Create());
        rootCommand.AddCommand(G4raCommand.Create());
        rootCommand.AddCommand(AgiCommand.Create());
        rootCommand.AddCommand(CreatePassiveSkillCommand(gamePathOption, verboseOption));
        rootCommand.AddCommand(CreateLaunchCommand(verboseOption));

        // Temporary test command
        var testG4skCommand = new Command("test-g4sk", "Test G4SK parser");
        var fileArg = new Argument<string>("file", "Path to G4SK file");
        testG4skCommand.AddArgument(fileArg);
        testG4skCommand.SetHandler((string file) => TestG4sk.Run(file), fileArg);
        rootCommand.AddCommand(testG4skCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateInfoCommand(Option<string?> gamePathOption, Option<bool> verboseOption)
    {
        var command = new Command("info", "Display game information");

        var jsonOption = new Option<bool>("--json", "Output as JSON");
        command.AddOption(jsonOption);

        command.SetHandler(async (string? gamePath, bool verbose, bool json) =>
        {
            await InfoCommand.ExecuteAsync(gamePath, verbose, json);
        }, gamePathOption, verboseOption, jsonOption);

        return command;
    }

    private static Command CreateDumpCommand(Option<string?> gamePathOption, Option<bool> verboseOption)
    {
        var command = new Command("dump", "Dump game files (extract all CPKs with parallelization and smart resume)");

        var outputOption = new Option<string>(
            aliases: ["--output", "-o"],
            description: "Output directory")
        {
            IsRequired = true
        };
        command.AddOption(outputOption);

        var smartOption = new Option<bool>(
            aliases: ["--smart", "-s"],
            getDefaultValue: () => true,
            description: "Smart dump (skip existing files, resume interrupted dumps)");
        command.AddOption(smartOption);

        var threadsOption = new Option<int>(
            aliases: ["--threads", "-t"],
            getDefaultValue: () => Environment.ProcessorCount,
            description: "Number of parallel threads");
        command.AddOption(threadsOption);

        var noLooseOption = new Option<bool>(
            "--no-loose",
            description: "Don't copy loose files (cfg.bin, ini, etc.)");
        command.AddOption(noLooseOption);

        command.SetHandler(async (string? gamePath, bool verbose, string output, bool smart, int threads, bool noLoose) =>
        {
            await DumpCommand.ExecuteAsync(gamePath, output, smart, verbose, threads, !noLoose);
        }, gamePathOption, verboseOption, outputOption, smartOption, threadsOption, noLooseOption);

        return command;
    }

    private static Command CreateExtractCommand(Option<string?> gamePathOption, Option<bool> verboseOption)
    {
        var command = new Command("extract", "Extract files from a CPK archive");

        var cpkArgument = new Argument<string>("cpk", "Path to CPK file");
        command.AddArgument(cpkArgument);

        var outputOption = new Option<string>(
            aliases: ["--output", "-o"],
            description: "Output directory")
        {
            IsRequired = true
        };
        command.AddOption(outputOption);

        var filterOption = new Option<string?>("--filter", "File filter pattern (e.g., *.g4tx)");
        command.AddOption(filterOption);

        var listOption = new Option<bool>("--list", "List files only, don't extract");
        command.AddOption(listOption);

        command.SetHandler(async (string? gamePath, bool verbose, string cpk, string output, string? filter, bool list) =>
        {
            await ExtractCommand.ExecuteAsync(gamePath, cpk, output, filter, list, verbose);
        }, gamePathOption, verboseOption, cpkArgument, outputOption, filterOption, listOption);

        return command;
    }

    private static Command CreatePackCommand(Option<string?> gamePathOption, Option<bool> verboseOption)
    {
        var command = new Command("pack", "Pack a mod folder for IEVR");

        var inputArgument = new Argument<string>("input", "Mod folder to pack");
        command.AddArgument(inputArgument);

        var outputOption = new Option<string>(
            aliases: ["--output", "-o"],
            description: "Output directory")
        {
            IsRequired = true
        };
        command.AddOption(outputOption);

        var cpkListOption = new Option<string?>("--cpklist", "Path to cpk_list.cfg.bin (default: from game)");
        command.AddOption(cpkListOption);

        var platformOption = new Option<string>("--platform", () => "PC", "Target platform (PC or SWITCH)");
        command.AddOption(platformOption);

        command.SetHandler(async (string? gamePath, bool verbose, string input, string output, string? cpkList, string platform) =>
        {
            await PackCommand.ExecuteAsync(gamePath, input, output, cpkList, platform, verbose);
        }, gamePathOption, verboseOption, inputArgument, outputOption, cpkListOption, platformOption);

        return command;
    }

    private static Command CreateConfigCommand(Option<string?> gamePathOption, Option<bool> verboseOption)
    {
        var command = new Command("config", "Read/write cfg.bin configuration files");

        // Subcommand: read
        var readCommand = new Command("read", "Read a cfg.bin file and export to JSON");
        var readFileArg = new Argument<string>("file", "Path to cfg.bin file");
        readCommand.AddArgument(readFileArg);
        var readOutputOption = new Option<string?>(["--output", "-o"], "Output JSON file");
        readCommand.AddOption(readOutputOption);

        readCommand.SetHandler(async (string? gamePath, bool verbose, string file, string? output) =>
        {
            await ConfigCommand.ReadAsync(gamePath, file, output, verbose);
        }, gamePathOption, verboseOption, readFileArg, readOutputOption);

        command.AddCommand(readCommand);

        // Subcommand: info
        var infoCommand = new Command("info", "Show cfg.bin file information");
        var infoFileArg = new Argument<string>("file", "Path to cfg.bin file");
        infoCommand.AddArgument(infoFileArg);

        infoCommand.SetHandler(async (string? gamePath, bool verbose, string file) =>
        {
            await ConfigCommand.InfoAsync(gamePath, file, verbose);
        }, gamePathOption, verboseOption, infoFileArg);

        command.AddCommand(infoCommand);

        // Subcommand: cpklist
        var cpkListCommand = new Command("cpklist", "Read cpk_list.cfg.bin from game");
        var cpkListOutputOption = new Option<string?>(["--output", "-o"], "Output JSON file");
        cpkListCommand.AddOption(cpkListOutputOption);

        cpkListCommand.SetHandler(async (string? gamePath, bool verbose, string? output) =>
        {
            await ConfigCommand.ReadCpkListAsync(gamePath, output, verbose);
        }, gamePathOption, verboseOption, cpkListOutputOption);

        command.AddCommand(cpkListCommand);

        // Subcommand: list
        var listCommand = new Command("list", "List all cfg.bin files in game");

        listCommand.SetHandler(async (string? gamePath, bool verbose) =>
        {
            await ConfigCommand.ListAsync(gamePath, verbose);
        }, gamePathOption, verboseOption);

        command.AddCommand(listCommand);

        // Subcommand: search
        var searchCommand = new Command("search", "Search entries in a cfg.bin file");
        var searchFileArg = new Argument<string>("file", "Path to cfg.bin file");
        searchCommand.AddArgument(searchFileArg);
        var patternArg = new Argument<string>("pattern", "Search pattern");
        searchCommand.AddArgument(patternArg);

        searchCommand.SetHandler(async (string? gamePath, bool verbose, string file, string pattern) =>
        {
            await ConfigCommand.SearchAsync(gamePath, file, pattern, verbose);
        }, gamePathOption, verboseOption, searchFileArg, patternArg);

        command.AddCommand(searchCommand);

        // Subcommand: decrypt
        var decryptCommand = new Command("decrypt", "Decrypt a cfg.bin file");
        var decryptFileArg = new Argument<string>("file", "Path to encrypted cfg.bin file");
        decryptCommand.AddArgument(decryptFileArg);
        var decryptOutputOption = new Option<string?>(["--output", "-o"], "Output file (default: <file>.dec)");
        decryptCommand.AddOption(decryptOutputOption);

        decryptCommand.SetHandler(async (bool verbose, string file, string? output) =>
        {
            await ConfigCommand.DecryptAsync(file, output, verbose);
        }, verboseOption, decryptFileArg, decryptOutputOption);

        command.AddCommand(decryptCommand);

        // Subcommand: encrypt
        var encryptCommand = new Command("encrypt", "Encrypt a cfg.bin file");
        var encryptFileArg = new Argument<string>("file", "Path to cfg.bin file");
        encryptCommand.AddArgument(encryptFileArg);
        var keyNameOption = new Option<string?>(["--key", "-k"], "Key name for encryption (default: filename)");
        encryptCommand.AddOption(keyNameOption);
        var encryptOutputOption = new Option<string?>(["--output", "-o"], "Output file (default: <file>.enc)");
        encryptCommand.AddOption(encryptOutputOption);

        encryptCommand.SetHandler(async (bool verbose, string file, string? keyName, string? output) =>
        {
            await ConfigCommand.EncryptAsync(file, keyName, output, verbose);
        }, verboseOption, encryptFileArg, keyNameOption, encryptOutputOption);

        command.AddCommand(encryptCommand);

        // Subcommand: convert (batch conversion)
        var convertCommand = new Command("convert", "Convert cfg.bin files to JSON (recursive, smallest first)");
        var convertPathArg = new Argument<string>("path", "Path to cfg.bin file or directory");
        convertCommand.AddArgument(convertPathArg);
        var convertRecursiveOption = new Option<bool>(["--recursive", "-r"], "Process directories recursively");
        convertCommand.AddOption(convertRecursiveOption);

        convertCommand.SetHandler(async (string? gamePath, bool verbose, string path, bool recursive) =>
        {
            await ConfigCommand.ConvertAsync(gamePath, path, recursive, verbose);
        }, gamePathOption, verboseOption, convertPathArg, convertRecursiveOption);

        command.AddCommand(convertCommand);

        return command;
    }

    private static Command CreateCryptoCommand(Option<bool> verboseOption)
    {
        var command = new Command("crypto", "Encrypt/decrypt Criware files");

        // Subcommand: decrypt
        var decryptCommand = new Command("decrypt", "Decrypt a Criware file");
        var decryptFileArg = new Argument<string>("file", "File to decrypt");
        decryptCommand.AddArgument(decryptFileArg);
        var decryptOutputOption = new Option<string>(["--output", "-o"], "Output file") { IsRequired = true };
        decryptCommand.AddOption(decryptOutputOption);

        decryptCommand.SetHandler(async (bool verbose, string file, string output) =>
        {
            await CryptoCommand.DecryptAsync(file, output, verbose);
        }, verboseOption, decryptFileArg, decryptOutputOption);

        command.AddCommand(decryptCommand);

        // Subcommand: encrypt
        var encryptCommand = new Command("encrypt", "Encrypt a Criware file");
        var encryptFileArg = new Argument<string>("file", "File to encrypt");
        encryptCommand.AddArgument(encryptFileArg);
        var encryptOutputOption = new Option<string>(["--output", "-o"], "Output file") { IsRequired = true };
        encryptCommand.AddOption(encryptOutputOption);
        var keyOption = new Option<string>("--key", "Encryption key (hex, e.g., 0x1717E18E)") { IsRequired = true };
        encryptCommand.AddOption(keyOption);

        encryptCommand.SetHandler(async (bool verbose, string file, string output, string key) =>
        {
            await CryptoCommand.EncryptAsync(file, output, key, verbose);
        }, verboseOption, encryptFileArg, encryptOutputOption, keyOption);

        command.AddCommand(encryptCommand);

        return command;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static Command CreateMemoryCommand(Option<bool> verboseOption)
    {
        var command = new Command("memory", "Memory editing for running game (requires nie.exe)");

        // Subcommand: status
        var statusCommand = new Command("status", "Check game process status");
        statusCommand.SetHandler((bool verbose) =>
        {
            MemoryCommand.Status(verbose);
        }, verboseOption);
        command.AddCommand(statusCommand);

        // Subcommand: read
        var readCommand = new Command("read", "Read memory value");
        var addressArg = new Argument<string>("address", "Address (hex, e.g., 0x123456)");
        readCommand.AddArgument(addressArg);
        var typeOption = new Option<string>("--type", () => "int32", "Value type (int32, float, bytes)");
        readCommand.AddOption(typeOption);
        var lengthOption = new Option<int>("--length", () => 4, "Length for bytes type");
        readCommand.AddOption(lengthOption);

        readCommand.SetHandler((bool verbose, string address, string type, int length) =>
        {
            MemoryCommand.Read(address, type, length, verbose);
        }, verboseOption, addressArg, typeOption, lengthOption);

        command.AddCommand(readCommand);

        // Subcommand: write
        var writeCommand = new Command("write", "Write memory value");
        var writeAddressArg = new Argument<string>("address", "Address (hex)");
        writeCommand.AddArgument(writeAddressArg);
        var valueArg = new Argument<string>("value", "Value to write");
        writeCommand.AddArgument(valueArg);
        var writeTypeOption = new Option<string>("--type", () => "int32", "Value type");
        writeCommand.AddOption(writeTypeOption);

        writeCommand.SetHandler((bool verbose, string address, string value, string type) =>
        {
            MemoryCommand.Write(address, value, type, verbose);
        }, verboseOption, writeAddressArg, valueArg, writeTypeOption);

        command.AddCommand(writeCommand);

        return command;
    }

    private static Command CreateLaunchCommand(Option<bool> verboseOption)
    {
        var command = new Command("launch", "Launch the game bypassing EAC (intelligent launch)");

        command.SetHandler((bool verbose) =>
        {
            LaunchCommand.Execute(verbose);
        }, verboseOption);

        return command;
    }

    private static Command CreateAnalyzeCommand(Option<string?> gamePathOption, Option<bool> verboseOption)
    {
        var command = new Command("analyze", "Analyze game structure and generate report");

        var outputOption = new Option<string?>(["--output", "-o"], "Output JSON report file");
        command.AddOption(outputOption);

        var deepOption = new Option<bool>("--deep", "Deep analysis (includes file hashes, strings)");
        command.AddOption(deepOption);

        command.SetHandler(async (string? gamePath, bool verbose, string? output, bool deep) =>
        {
            await AnalyzeCommand.ExecuteAsync(gamePath, output, deep, verbose);
        }, gamePathOption, verboseOption, outputOption, deepOption);

        return command;
    }

    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
        Justification = "CFG.BIN types are preserved in IECODE.Core")]
    private static Command CreatePassiveSkillCommand(Option<string?> gamePathOption, Option<bool> verboseOption)
    {
        var command = new Command("passive", "Analyze passive skill data from CFG.BIN files");

        // Subcommand: analyze
        var analyzeCommand = new Command("analyze", "Parse passive skill configs and export data");

        var skillConfigOption = new Option<string?>(
            "--skill-config",
            "Path to passive_skill_config.cfg.bin");
        analyzeCommand.AddOption(skillConfigOption);

        var effectConfigOption = new Option<string?>(
            "--effect-config",
            "Path to passive_skill_effect_config.cfg.bin");
        analyzeCommand.AddOption(effectConfigOption);

        var outputOption = new Option<string?>(
            ["--output", "-o"],
            "Export results to JSON file");
        analyzeCommand.AddOption(outputOption);

        var buildTypeOption = new Option<string?>(
            "--build-type",
            "Filter by build type (Knockout, Tension, Counter, Kizuna, RoughPlay, Justice)");
        analyzeCommand.AddOption(buildTypeOption);

        analyzeCommand.SetHandler(async (string? gamePath, bool verbose, string? skillConfig, string? effectConfig, string? output, string? buildType) =>
        {
            await PassiveSkillCommand.AnalyzeAsync(gamePath, skillConfig, effectConfig, output, buildType, verbose);
        }, gamePathOption, verboseOption, skillConfigOption, effectConfigOption, outputOption, buildTypeOption);

        command.AddCommand(analyzeCommand);

        // Subcommand: buildtypes
        var buildTypesCommand = new Command("buildtypes", "List all build type categories");
        buildTypesCommand.SetHandler(() =>
        {
            PassiveSkillCommand.ListBuildTypes();
        });
        command.AddCommand(buildTypesCommand);

        // Subcommand: help
        var helpCommand = new Command("help", "Show help for passive skill commands");
        helpCommand.SetHandler(() =>
        {
            PassiveSkillCommand.ShowHelp();
        });
        command.AddCommand(helpCommand);

        return command;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static Command CreateTechniqueCommand(Option<bool> verboseOption)
    {
        var command = new Command("technique", "Technique (Hissatsu) editing for players (requires nie.exe)");

        // Subcommand: list
        var listCommand = new Command("list", "List all available hidden techniques");
        listCommand.SetHandler(() =>
        {
            TechniqueCommand.List();
        });
        command.AddCommand(listCommand);

        // Subcommand: read
        var readCommand = new Command("read", "Read techniques of a player");
        var playerArg = new Argument<int>("player", "Player index (0-15)");
        readCommand.AddArgument(playerArg);
        readCommand.SetHandler((bool verbose, int player) =>
        {
            TechniqueCommand.Read(player, verbose);
        }, verboseOption, playerArg);
        command.AddCommand(readCommand);

        // Subcommand: write
        var writeCommand = new Command("write", "Write a technique to a player's slot");
        var writePlayerArg = new Argument<int>("player", "Player index (0-15)");
        var writeSlotArg = new Argument<int>("slot", "Slot index (0-4)");
        var techArg = new Argument<string>("technique", "Technique name or hash (e.g., 'arashi_tatsumaki' or '0xA9EF1EA8')");
        writeCommand.AddArgument(writePlayerArg);
        writeCommand.AddArgument(writeSlotArg);
        writeCommand.AddArgument(techArg);
        var levelOption = new Option<int>("--level", () => 99, "Technique level (1-99)");
        writeCommand.AddOption(levelOption);
        writeCommand.SetHandler((bool verbose, int player, int slot, string tech, int level) =>
        {
            TechniqueCommand.Write(player, slot, tech, level, verbose);
        }, verboseOption, writePlayerArg, writeSlotArg, techArg, levelOption);
        command.AddCommand(writeCommand);

        // Subcommand: arashi (quick assign Ouragan Cyclonique)
        var arashiCommand = new Command("arashi", "Quick assign Ouragan Cyclonique (arashi_tatsumaki) to a player");
        var arashiPlayerArg = new Argument<int>("player", "Player index (0-15)");
        var arashiSlotOption = new Option<int>("--slot", () => 0, "Slot index (0-4, default: 0)");
        arashiCommand.AddArgument(arashiPlayerArg);
        arashiCommand.AddOption(arashiSlotOption);
        arashiCommand.SetHandler((bool verbose, int player, int slot) =>
        {
            TechniqueCommand.Arashi(player, slot, verbose);
        }, verboseOption, arashiPlayerArg, arashiSlotOption);
        command.AddCommand(arashiCommand);

        // Subcommand: clear
        var clearCommand = new Command("clear", "Clear a technique from a player's slot");
        var clearPlayerArg = new Argument<int>("player", "Player index (0-15)");
        var clearSlotArg = new Argument<int>("slot", "Slot index (0-4)");
        clearCommand.AddArgument(clearPlayerArg);
        clearCommand.AddArgument(clearSlotArg);
        clearCommand.SetHandler((bool verbose, int player, int slot) =>
        {
            TechniqueCommand.Clear(player, slot, verbose);
        }, verboseOption, clearPlayerArg, clearSlotArg);
        command.AddCommand(clearCommand);

        return command;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static Command CreateTraceCommand(Option<bool> verboseOption)
    {
        var command = new Command("trace", "Real-time process tracing via wtrace (ETW-based)");

        // Common options
        var handlersOption = new Option<string?>(
            ["--handlers", "-H"],
            "Event handlers to enable (comma-separated: file,tcp,udp,registry,rpc,image)");
        command.AddOption(handlersOption);

        var filterOption = new Option<string?>(
            ["--filter", "-f"],
            "Event filter (e.g., 'name >= FileIO' or 'path >= save'; use ; for multiple)");
        command.AddOption(filterOption);

        var jsonOption = new Option<bool>(
            "--json",
            "Output in JSON format");
        command.AddOption(jsonOption);

        var outputOption = new Option<string?>(
            ["--output", "-o"],
            "Save trace to file");
        command.AddOption(outputOption);

        var childrenOption = new Option<bool>(
            ["--children", "-c"],
            "Also trace child processes");
        command.AddOption(childrenOption);

        // Subcommand: status
        var statusCommand = new Command("status", "Check wtrace installation and game status");
        statusCommand.SetHandler((bool verbose) =>
        {
            TraceCommand.Status(verbose);
        }, verboseOption);
        command.AddCommand(statusCommand);

        // Subcommand: attach (default - attach to nie.exe)
        var attachCommand = new Command("attach", "Attach to running nie.exe (default)");
        attachCommand.SetHandler(async (bool verbose, string? handlers, string? filter, bool children, bool json, string? output) =>
        {
            await TraceCommand.AttachAsync(handlers, filter, children, json, output, verbose);
        }, verboseOption, handlersOption, filterOption, childrenOption, jsonOption, outputOption);
        command.AddCommand(attachCommand);

        // Subcommand: pid
        var pidCommand = new Command("pid", "Trace a process by PID");
        var pidArg = new Argument<int>("pid", "Process ID to trace");
        pidCommand.AddArgument(pidArg);
        pidCommand.SetHandler(async (bool verbose, int pid, string? handlers, string? filter, bool children, bool json, string? output) =>
        {
            await TraceCommand.TracePidAsync(pid, handlers, filter, children, json, output, verbose);
        }, verboseOption, pidArg, handlersOption, filterOption, childrenOption, jsonOption, outputOption);
        command.AddCommand(pidCommand);

        // Subcommand: name
        var nameCommand = new Command("name", "Trace a process by name");
        var nameArg = new Argument<string>("name", "Process name (e.g., notepad, chrome)");
        nameCommand.AddArgument(nameArg);
        nameCommand.SetHandler(async (bool verbose, string name, string? handlers, string? filter, bool children, bool json, string? output) =>
        {
            await TraceCommand.TraceNameAsync(name, handlers, filter, children, json, output, verbose);
        }, verboseOption, nameArg, handlersOption, filterOption, childrenOption, jsonOption, outputOption);
        command.AddCommand(nameCommand);

        // Subcommand: system
        var systemCommand = new Command("system", "System-wide trace (all processes)");
        systemCommand.SetHandler(async (bool verbose, string? handlers, string? filter, bool json, string? output) =>
        {
            await TraceCommand.SystemAsync(handlers, filter, json, output, verbose);
        }, verboseOption, handlersOption, filterOption, jsonOption, outputOption);
        command.AddCommand(systemCommand);

        // Default handler (no subcommand = attach to nie.exe)
        command.SetHandler(async (bool verbose, string? handlers, string? filter, bool children, bool json, string? output) =>
        {
            await TraceCommand.AttachAsync(handlers, filter, children, json, output, verbose);
        }, verboseOption, handlersOption, filterOption, childrenOption, jsonOption, outputOption);

        return command;
    }

    private static Command CreatePipelineCommand(Option<string?> gamePathOption, Option<bool> verboseOption)
    {
        var command = new Command("pipeline", "High-performance data extraction, conversion, and cleanup pipeline");

        var outputOption = new Option<string?>(
            aliases: ["--output", "-o"],
            description: "Output directory (default: data/extracted)");
        command.AddOption(outputOption);

        var packsOption = new Option<string?>(
            aliases: ["--packs", "-p"],
            description: "Custom packs directory (default: game/data/packs)");
        command.AddOption(packsOption);

        var convertOption = new Option<bool>(
            aliases: ["--convert", "-c"],
            getDefaultValue: () => true,
            description: "Convert binary formats to readable (G4TX→PNG, cfg.bin→JSON, etc.)");
        command.AddOption(convertOption);

        var deleteAfterExtractOption = new Option<bool>(
            "--delete-cpk",
            description: "Delete CPK after full extraction (FREES DISK SPACE)");
        command.AddOption(deleteAfterExtractOption);

        var deleteAfterConvertOption = new Option<bool>(
            "--delete-binary",
            description: "Delete original binary after conversion (KEEP ONLY READABLE FILES)");
        command.AddOption(deleteAfterConvertOption);

        var parallelCpksOption = new Option<int>(
            "--parallel-cpks",
            getDefaultValue: () => 0,
            description: "Number of CPKs to extract in parallel (default: CPU/2)");
        command.AddOption(parallelCpksOption);

        var parallelConversionsOption = new Option<int>(
            "--parallel-conversions",
            getDefaultValue: () => 0,
            description: "Number of files to convert in parallel (default: CPU*2)");
        command.AddOption(parallelConversionsOption);

        var noResumeOption = new Option<bool>(
            "--no-resume",
            description: "Don't resume from previous run (start fresh)");
        command.AddOption(noResumeOption);

        var patternOption = new Option<string>(
            "--pattern",
            getDefaultValue: () => "*.cpk",
            description: "CPK file pattern to process");
        command.AddOption(patternOption);

        var noRecursiveOption = new Option<bool>(
            "--no-recursive",
            description: "Don't search subdirectories for CPKs");
        command.AddOption(noRecursiveOption);

        // Use SetHandler with context to handle many options
        command.SetHandler(async context =>
        {
            var gamePath = context.ParseResult.GetValueForOption(gamePathOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var packs = context.ParseResult.GetValueForOption(packsOption);
            var convert = context.ParseResult.GetValueForOption(convertOption);
            var deleteAfterExtract = context.ParseResult.GetValueForOption(deleteAfterExtractOption);
            var deleteAfterConvert = context.ParseResult.GetValueForOption(deleteAfterConvertOption);
            var parallelCpks = context.ParseResult.GetValueForOption(parallelCpksOption);
            var parallelConversions = context.ParseResult.GetValueForOption(parallelConversionsOption);
            var noResume = context.ParseResult.GetValueForOption(noResumeOption);
            var pattern = context.ParseResult.GetValueForOption(patternOption) ?? "*.cpk";
            var noRecursive = context.ParseResult.GetValueForOption(noRecursiveOption);
            
            await PipelineCommand.ExecuteAsync(
                gamePath,
                output,
                packs,
                convert,
                deleteAfterExtract,
                deleteAfterConvert,
                parallelCpks,
                parallelConversions,
                resume: !noResume,
                recursive: !noRecursive,
                pattern,
                verbose);
        });

        return command;
    }
}
