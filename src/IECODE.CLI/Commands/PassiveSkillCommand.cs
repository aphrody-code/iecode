using System.Diagnostics.CodeAnalysis;
using IECODE.Core;
using IECODE.Core.Game.PassiveSkills;

namespace IECODE.CLI.Commands;

/// <summary>
/// Command for analyzing and exporting passive skill data.
/// Usage: iecode passive [subcommand] [options]
/// </summary>
public static class PassiveSkillCommand
{
    /// <summary>
    /// Analyze passive skill config files and export data.
    /// </summary>
    [RequiresUnreferencedCode("CFG.BIN parsing uses reflection.")]
    public static async Task AnalyzeAsync(
        string? gamePath,
        string? skillConfig,
        string? effectConfig,
        string? output,
        string? buildType,
        bool verbose)
    {
        try
        {
            using var game = new IEVRGame(gamePath);
            var service = new PassiveSkillService();

            // Load skill config
            if (!string.IsNullOrEmpty(skillConfig))
            {
                if (!File.Exists(skillConfig))
                {
                    Console.Error.WriteLine($"Error: Skill config not found: {skillConfig}");
                    Environment.ExitCode = 1;
                    return;
                }

                var data = await File.ReadAllBytesAsync(skillConfig);
                var count = service.LoadSkillConfig(data);
                Console.WriteLine($"Loaded {count} passive skill definitions");
            }

            // Load effect config
            if (!string.IsNullOrEmpty(effectConfig))
            {
                if (!File.Exists(effectConfig))
                {
                    Console.Error.WriteLine($"Error: Effect config not found: {effectConfig}");
                    Environment.ExitCode = 1;
                    return;
                }

                var data = await File.ReadAllBytesAsync(effectConfig);
                var count = service.LoadEffectConfig(data);
                Console.WriteLine($"Loaded {count} effect definitions");
            }

            // Link skills to effects
            service.LinkSkillsToEffects();

            // Filter by build type if specified
            if (!string.IsNullOrEmpty(buildType))
            {
                if (Enum.TryParse<PassiveSkillBuildType>(buildType, true, out var bt))
                {
                    var filtered = service.GetSkillsByBuildType(bt).ToList();
                    Console.WriteLine($"\nSkills with build type '{bt}': {filtered.Count}");

                    foreach (var skill in filtered.Take(20))
                    {
                        Console.WriteLine($"  [{skill.Id}] Hash: 0x{skill.Info.SkillHash:X8}, Effect: {skill.EffectRef.EffectId}");
                    }

                    if (filtered.Count > 20)
                    {
                        Console.WriteLine($"  ... and {filtered.Count - 20} more");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Unknown build type: {buildType}");
                    Console.Error.WriteLine($"Valid types: {string.Join(", ", Enum.GetNames<PassiveSkillBuildType>())}");
                }
            }

            // Show summary
            Console.WriteLine("\n=== Build Type Distribution ===");
            foreach (var bt in Enum.GetValues<PassiveSkillBuildType>())
            {
                var count = service.GetSkillsByBuildType(bt).Count();
                if (count > 0)
                {
                    Console.WriteLine($"  {bt}: {count} skills");
                }
            }

            // Export to JSON if output specified
            if (!string.IsNullOrEmpty(output))
            {
                var json = service.ExportToJson();
                await File.WriteAllTextAsync(output, json);
                Console.WriteLine($"\nExported to: {output}");
            }

            if (verbose)
            {
                Console.WriteLine("\n=== Sample Skills (first 10) ===");
                foreach (var (skill, effect) in service.GetSkillsWithEffects().Take(10))
                {
                    Console.WriteLine($"[{skill.Id}] {skill.BuildTypeName}");
                    Console.WriteLine($"  SkillHash: 0x{skill.Info.SkillHash:X8}");
                    Console.WriteLine($"  NameHash:  0x{skill.Info.NameHash:X8}");
                    Console.WriteLine($"  EffectRef: {skill.EffectRef.EffectId} (count: {skill.EffectRef.EffectCount})");
                    Console.WriteLine($"  Icons:     [{skill.BuffIcon.IconIndex1}, {skill.BuffIcon.IconIndex2}]");

                    if (effect != null)
                    {
                        Console.WriteLine($"  Effect Details:");
                        Console.WriteLine($"    Timing Conditions: {effect.ExecTimingConditions.Count}");
                        Console.WriteLine($"    Target Conditions: {effect.TargetConditions.Count}");
                        Console.WriteLine($"    Exec Conditions:   {effect.ExecConditions.Count}");
                        Console.WriteLine($"    Effects:           {effect.Effects.Count}");
                        
                        if (effect.GrandTotalInfo.PercentageValue > 0)
                        {
                            Console.WriteLine($"    Percentage: {effect.GrandTotalInfo.PercentageValue}%");
                        }
                    }
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// List all passive skill build types.
    /// </summary>
    public static void ListBuildTypes()
    {
        Console.WriteLine("Passive Skill Build Types:");
        Console.WriteLine("==========================");
        Console.WriteLine($"  0 = {PassiveSkillBuildType.Default}  (Default/Unknown)");
        Console.WriteLine($"  1 = {PassiveSkillBuildType.Knockout} (ノックアウト - Offensive)");
        Console.WriteLine($"  2 = {PassiveSkillBuildType.Tension}  (テンション - Momentum)");
        Console.WriteLine($"  3 = {PassiveSkillBuildType.Counter}  (カウンター - Defensive)");
        Console.WriteLine($"  4 = {PassiveSkillBuildType.Kizuna}   (キズナ - Bond)");
        Console.WriteLine($"  5 = {PassiveSkillBuildType.RoughPlay}(ラフプレー - Physical)");
        Console.WriteLine($"  6 = {PassiveSkillBuildType.Justice}  (ジャスティス - Fair Play)");
    }

    /// <summary>
    /// Show help for passive skill command.
    /// </summary>
    public static void ShowHelp()
    {
        Console.WriteLine(@"
Passive Skill Analysis Command
==============================

Usage:
  iecode passive analyze [options]
  iecode passive buildtypes
  iecode passive help

Commands:
  analyze     Parse passive skill config files and export data
  buildtypes  List all build type categories
  help        Show this help message

Analyze Options:
  --skill-config <path>   Path to passive_skill_config.cfg.bin
  --effect-config <path>  Path to passive_skill_effect_config.cfg.bin
  --output <path>         Export results to JSON file
  --build-type <type>     Filter by build type (Knockout, Tension, etc.)
  --verbose               Show detailed output

Examples:
  iecode passive buildtypes
  iecode passive analyze --skill-config passive_skill_config_0.08.86.cfg.bin
  iecode passive analyze --skill-config skill.cfg.bin --effect-config effect.cfg.bin --output skills.json
  iecode passive analyze --skill-config skill.cfg.bin --build-type Knockout --verbose
");
    }
}
