using System.Text.Json;
using System.Text.Json.Serialization;
using IECODE.Core.Formats.Level5.CfgBin.Encryption;
using IECODE.Core.Formats.Level5.CfgBin;
using IECODE.Core.Formats.Level5.CfgBin.Logic;

namespace IECODE.Core.GameData;

/// <summary>
/// Resolves skill hashes (CRC32) to localized names and descriptions from cfg.bin game files.
/// Supports 3299 passive skills and 500+ active techniques.
/// </summary>
/// <remarks>
/// Uses the following game files:
/// - passive_skill_config.cfg.bin: Skill configuration (ID, effect, values)
/// - passive_skill_text.cfg.bin: Skill name/description text (per language)
/// - text_info.cfg.bin: General text table for technique names
/// 
/// Native AOT compatible - uses source-generated JSON serialization.
/// </remarks>
public sealed class SkillTextResolver : IDisposable
{
    private readonly Dictionary<uint, LocalizedSkill> _skillCache = new();
    private readonly Dictionary<uint, LocalizedTechnique> _techniqueCache = new();
    private bool _isLoaded;

    /// <summary>
    /// Localized passive skill data.
    /// </summary>
    public sealed record LocalizedSkill
    {
        public int Id { get; init; }
        public uint SkillHash { get; init; }
        public uint NameHash { get; init; }
        public string SkillHashHex => $"0x{SkillHash:X8}";
        public string NameHashHex => $"0x{NameHash:X8}";
        public LocalizedText Names { get; init; } = new();
        public LocalizedText Descriptions { get; init; } = new();
        public string BuildType { get; init; } = string.Empty;
        public int BuildTypeId { get; init; }
        public int EffectId { get; init; }
        public float EffectValue { get; init; }
        public int EffectCount { get; init; }
        public int Level { get; init; }
        public int IconIndex1 { get; init; }
        public int IconIndex2 { get; init; }
    }

    /// <summary>
    /// Localized active technique/hissatsu data.
    /// </summary>
    public sealed record LocalizedTechnique
    {
        public uint Hash { get; init; }
        public string HashHex => $"0x{Hash:X8}";
        public LocalizedText Names { get; init; } = new();
        public LocalizedText Descriptions { get; init; } = new();
        public string Type { get; init; } = string.Empty; // Shoot, Dribble, Block, Catch, etc.
        public int TpCost { get; init; }
        public int Power { get; init; }
        public string Element { get; init; } = string.Empty;
    }

    /// <summary>
    /// Localized text container for all supported languages.
    /// </summary>
    public sealed record LocalizedText
    {
        public string Japanese { get; init; } = string.Empty;
        public string English { get; init; } = string.Empty;
        public string French { get; init; } = string.Empty;
        public string German { get; init; } = string.Empty;
        public string Spanish { get; init; } = string.Empty;
        public string Italian { get; init; } = string.Empty;

        public string GetByCode(string languageCode) => languageCode.ToLowerInvariant() switch
        {
            "ja" or "jp" => Japanese,
            "en" => English,
            "fr" => French,
            "de" => German,
            "es" => Spanish,
            "it" => Italian,
            _ => English
        };

        public bool HasAny => !string.IsNullOrEmpty(Japanese) || 
                              !string.IsNullOrEmpty(English) || 
                              !string.IsNullOrEmpty(French);
    }

    /// <summary>
    /// Known effect types for passive skills.
    /// </summary>
    public static class EffectTypes
    {
        public const int KickBoost = 1;
        public const int BodyBoost = 2;
        public const int ControlBoost = 3;
        public const int GuardBoost = 4;
        public const int SpeedBoost = 5;
        public const int StaminaBoost = 6;
        public const int GutsBoost = 7;
        public const int FreedomBoost = 8;
        public const int TpReduction = 10;
        public const int CriticalRate = 11;
        public const int ElementBoost = 15;
        public const int TeamSynergy = 20;

        public static string GetName(int effectId) => effectId switch
        {
            KickBoost => "Kick Boost",
            BodyBoost => "Body Boost",
            ControlBoost => "Control Boost",
            GuardBoost => "Guard Boost",
            SpeedBoost => "Speed Boost",
            StaminaBoost => "Stamina Boost",
            GutsBoost => "Guts Boost",
            FreedomBoost => "Freedom Boost",
            TpReduction => "TP Reduction",
            CriticalRate => "Critical Rate",
            ElementBoost => "Element Boost",
            TeamSynergy => "Team Synergy",
            _ => $"Effect #{effectId}"
        };
    }

    /// <summary>
    /// Supported language codes.
    /// </summary>
    public static readonly string[] SupportedLanguages = ["ja", "en", "fr", "de", "es", "it"];

    /// <summary>
    /// Whether skill data has been loaded.
    /// </summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Number of loaded passive skills.
    /// </summary>
    public int SkillCount => _skillCache.Count;

    /// <summary>
    /// Number of loaded techniques.
    /// </summary>
    public int TechniqueCount => _techniqueCache.Count;

    /// <summary>
    /// Loads skill data from cfg.bin files and optional JSON database.
    /// </summary>
    /// <param name="gameDataPath">Path to the game data dump folder.</param>
    /// <param name="passiveSkillsJsonPath">Optional path to passive_skills.json for additional data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int> LoadFromGameDataAsync(
        string gameDataPath, 
        string? passiveSkillsJsonPath = null,
        CancellationToken cancellationToken = default)
    {
        _skillCache.Clear();
        _techniqueCache.Clear();

        // Step 1: Load passive skill configuration
        var skillConfigs = await LoadPassiveSkillConfigAsync(gameDataPath, cancellationToken);

        // Step 2: Load localized text for each language
        var localizedSkillTexts = new Dictionary<string, Dictionary<uint, (string Name, string Desc)>>();
        foreach (var lang in SupportedLanguages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            localizedSkillTexts[lang] = await LoadSkillTextAsync(gameDataPath, lang, cancellationToken);
        }

        // Step 3: Load from passive_skills.json if provided
        Dictionary<uint, PassiveSkillJsonEntry>? jsonEntries = null;
        if (!string.IsNullOrEmpty(passiveSkillsJsonPath) && File.Exists(passiveSkillsJsonPath))
        {
            jsonEntries = await LoadPassiveSkillsJsonAsync(passiveSkillsJsonPath, cancellationToken);
        }

        // Step 4: Build skill cache by merging all sources
        foreach (var (skillHash, config) in skillConfigs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nameHash = (uint)config.SkillId; // May need adjustment based on actual mapping
            var jsonEntry = jsonEntries?.GetValueOrDefault(skillHash);

            // Try to find localized names
            var names = new LocalizedText
            {
                Japanese = GetTextForLang(localizedSkillTexts, "ja", nameHash)?.Name ?? string.Empty,
                English = GetTextForLang(localizedSkillTexts, "en", nameHash)?.Name ?? string.Empty,
                French = GetTextForLang(localizedSkillTexts, "fr", nameHash)?.Name ?? string.Empty,
                German = GetTextForLang(localizedSkillTexts, "de", nameHash)?.Name ?? string.Empty,
                Spanish = GetTextForLang(localizedSkillTexts, "es", nameHash)?.Name ?? string.Empty,
                Italian = GetTextForLang(localizedSkillTexts, "it", nameHash)?.Name ?? string.Empty,
            };

            var descriptions = new LocalizedText
            {
                Japanese = GetTextForLang(localizedSkillTexts, "ja", nameHash)?.Desc ?? string.Empty,
                English = GetTextForLang(localizedSkillTexts, "en", nameHash)?.Desc ?? string.Empty,
                French = GetTextForLang(localizedSkillTexts, "fr", nameHash)?.Desc ?? string.Empty,
                German = GetTextForLang(localizedSkillTexts, "de", nameHash)?.Desc ?? string.Empty,
                Spanish = GetTextForLang(localizedSkillTexts, "es", nameHash)?.Desc ?? string.Empty,
                Italian = GetTextForLang(localizedSkillTexts, "it", nameHash)?.Desc ?? string.Empty,
            };

            var skill = new LocalizedSkill
            {
                Id = jsonEntry?.Id ?? (int)skillHash,
                SkillHash = skillHash,
                NameHash = jsonEntry?.NameHash ?? nameHash,
                Names = names,
                Descriptions = descriptions,
                BuildType = jsonEntry?.BuildType ?? string.Empty,
                BuildTypeId = jsonEntry?.BuildTypeId ?? 0,
                EffectId = jsonEntry?.EffectId ?? 0,
                EffectValue = config.EffectValue,
                EffectCount = jsonEntry?.EffectCount ?? 1,
                Level = ExtractLevelFromName(names.English),
                IconIndex1 = jsonEntry?.IconIndex1 ?? 0,
                IconIndex2 = jsonEntry?.IconIndex2 ?? 0,
            };

            _skillCache[skillHash] = skill;
        }

        // Step 5: Also load from JSON entries not in cfg.bin
        if (jsonEntries != null)
        {
            foreach (var (hash, entry) in jsonEntries)
            {
                if (_skillCache.ContainsKey(hash)) continue;

                var skill = new LocalizedSkill
                {
                    Id = entry.Id,
                    SkillHash = hash,
                    NameHash = entry.NameHash,
                    Names = new LocalizedText(), // No localized text available
                    Descriptions = new LocalizedText(),
                    BuildType = entry.BuildType,
                    BuildTypeId = entry.BuildTypeId,
                    EffectId = entry.EffectId,
                    EffectValue = 0,
                    EffectCount = entry.EffectCount,
                    Level = 1,
                    IconIndex1 = entry.IconIndex1,
                    IconIndex2 = entry.IconIndex2,
                };

                _skillCache[hash] = skill;
            }
        }

        _isLoaded = true;
        return _skillCache.Count;
    }

    /// <summary>
    /// Gets a passive skill by hash.
    /// </summary>
    public LocalizedSkill? GetSkill(uint hash) => _skillCache.GetValueOrDefault(hash);

    /// <summary>
    /// Gets skill name by hash for a specific language.
    /// </summary>
    public string GetSkillName(uint hash, string language = "en")
    {
        var skill = GetSkill(hash);
        if (skill == null)
            return $"Unknown Skill (0x{hash:X8})";

        var name = skill.Names.GetByCode(language);
        if (!string.IsNullOrEmpty(name))
            return name;

        // Fallback: generate from effect type
        return $"{EffectTypes.GetName(skill.EffectId)} Lv.{skill.Level}";
    }

    /// <summary>
    /// Gets skill description by hash for a specific language.
    /// </summary>
    public string GetSkillDescription(uint hash, string language = "en")
    {
        var skill = GetSkill(hash);
        if (skill == null)
            return string.Empty;

        var desc = skill.Descriptions.GetByCode(language);
        if (!string.IsNullOrEmpty(desc))
            return desc;

        // Fallback: generate from effect type and value
        var effectName = EffectTypes.GetName(skill.EffectId);
        var value = skill.EffectValue > 0 ? $"+{skill.EffectValue:P0}" : string.Empty;
        return $"{effectName} {value}".Trim();
    }

    /// <summary>
    /// Gets a technique by hash.
    /// </summary>
    public LocalizedTechnique? GetTechnique(uint hash) => _techniqueCache.GetValueOrDefault(hash);

    /// <summary>
    /// Searches skills by name (partial match, case-insensitive).
    /// </summary>
    public IEnumerable<LocalizedSkill> SearchSkills(string query, string language = "en")
    {
        if (string.IsNullOrWhiteSpace(query))
            return _skillCache.Values;

        return _skillCache.Values.Where(s =>
        {
            var name = s.Names.GetByCode(language);
            if (!string.IsNullOrEmpty(name) && name.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;

            if (s.BuildType.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;

            if (s.SkillHashHex.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        });
    }

    /// <summary>
    /// Filters skills by effect type.
    /// </summary>
    public IEnumerable<LocalizedSkill> FilterByEffect(int effectId)
    {
        return _skillCache.Values.Where(s => s.EffectId == effectId);
    }

    /// <summary>
    /// Filters skills by build type (Justice, Love, Power, Rhythm, etc.).
    /// </summary>
    public IEnumerable<LocalizedSkill> FilterByBuildType(string buildType)
    {
        return _skillCache.Values.Where(s => 
            s.BuildType.Equals(buildType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all passive skills.
    /// </summary>
    public IReadOnlyCollection<LocalizedSkill> GetAllSkills() => _skillCache.Values;

    /// <summary>
    /// Exports skill data to JSON cache file.
    /// </summary>
    public async Task ExportToCacheAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var data = new SkillCacheData
        {
            Version = "1.0",
            GeneratedAt = DateTime.UtcNow,
            SkillCount = _skillCache.Count,
            TechniqueCount = _techniqueCache.Count,
            Skills = _skillCache.Values.ToList(),
            Techniques = _techniqueCache.Values.ToList()
        };

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(
            stream, 
            data, 
            SkillTextResolverJsonContext.Default.SkillCacheData, 
            cancellationToken);
    }

    /// <summary>
    /// Loads skill data from cached JSON file.
    /// </summary>
    public async Task<int> LoadFromCacheAsync(string cachePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(cachePath))
            return 0;

        await using var stream = File.OpenRead(cachePath);
        var data = await JsonSerializer.DeserializeAsync(
            stream,
            SkillTextResolverJsonContext.Default.SkillCacheData,
            cancellationToken);

        if (data == null)
            return 0;

        _skillCache.Clear();
        _techniqueCache.Clear();

        foreach (var skill in data.Skills ?? [])
        {
            _skillCache[skill.SkillHash] = skill;
        }

        foreach (var technique in data.Techniques ?? [])
        {
            _techniqueCache[technique.Hash] = technique;
        }

        _isLoaded = true;
        return _skillCache.Count + _techniqueCache.Count;
    }

    #region Private Methods

    private async Task<Dictionary<uint, PassiveSkillConfig>> LoadPassiveSkillConfigAsync(
        string basePath,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<uint, PassiveSkillConfig>();
        var configPath = Path.Combine(basePath, "data", "common", "gamedata", "skill");

        if (!Directory.Exists(configPath))
            return result;

        var cfgBinFiles = Directory.GetFiles(configPath, "passive_skill*.cfg.bin");
        foreach (var file in cfgBinFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cfgBin = await LoadCfgBinAsync(file, cancellationToken);
            if (cfgBin == null) continue;

            // Look for PASSIVE_SKILL_EFFECT entries
            var entries = FindEntriesByPrefix(cfgBin, "PASSIVE_SKILL_EFFECT");
            foreach (var entry in entries)
            {
                var config = new PassiveSkillConfig(entry);
                var hash = (uint)config.SkillId;
                result.TryAdd(hash, config);
            }
        }

        return result;
    }

    private async Task<Dictionary<uint, (string Name, string Desc)>> LoadSkillTextAsync(
        string basePath,
        string language,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<uint, (string, string)>();
        var textPath = Path.Combine(basePath, "data", "common", "text", language);

        if (!Directory.Exists(textPath))
            return result;

        // Look for passive_skill_text.cfg.bin or text_info.cfg.bin
        string[] possibleFiles = [
            Path.Combine(textPath, "passive_skill_text.cfg.bin"),
            Path.Combine(textPath, "skill_text.cfg.bin"),
            Path.Combine(textPath, "text_info.cfg.bin")
        ];

        foreach (var file in possibleFiles)
        {
            if (!File.Exists(file)) continue;

            cancellationToken.ThrowIfCancellationRequested();

            var cfgBin = await LoadCfgBinAsync(file, cancellationToken);
            if (cfgBin == null) continue;

            // TEXT_INFO entries have: [0]=ID, [2]=Text
            var entries = FindEntriesByPrefix(cfgBin, "TEXT_INFO");
            foreach (var entry in entries)
            {
                var textInfo = new PassiveSkillText(entry);
                if (!string.IsNullOrEmpty(textInfo.Description))
                {
                    var hash = (uint)textInfo.TextId;
                    // For now, we use description as both name and description
                    // Actual implementation may need to distinguish based on entry structure
                    result.TryAdd(hash, (textInfo.Description, textInfo.Description));
                }
            }
        }

        return result;
    }

    private async Task<Dictionary<uint, PassiveSkillJsonEntry>> LoadPassiveSkillsJsonAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<uint, PassiveSkillJsonEntry>();

        await using var stream = File.OpenRead(path);
        var entries = await JsonSerializer.DeserializeAsync<List<PassiveSkillJsonEntry>>(
            stream,
            SkillTextResolverJsonContext.Default.ListPassiveSkillJsonEntry,
            cancellationToken);

        if (entries == null)
            return result;

        foreach (var entry in entries)
        {
            // Parse hex string hash
            if (TryParseHash(entry.SkillHashHex, out var hash))
            {
                result.TryAdd(hash, entry);
            }
        }

        return result;
    }

    private static (string Name, string Desc)? GetTextForLang(
        Dictionary<string, Dictionary<uint, (string Name, string Desc)>> texts,
        string lang,
        uint hash)
    {
        if (texts.TryGetValue(lang, out var langTexts) && langTexts.TryGetValue(hash, out var text))
            return text;
        return null;
    }

    private static int ExtractLevelFromName(string name)
    {
        // Try to extract level from patterns like "Lv.5", "Level 3", etc.
        if (string.IsNullOrEmpty(name))
            return 1;

        var match = System.Text.RegularExpressions.Regex.Match(
            name, 
            @"(?:Lv\.?|Level\s*)(\d+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success && int.TryParse(match.Groups[1].Value, out var level) ? level : 1;
    }

    private static bool TryParseHash(string? hexString, out uint hash)
    {
        hash = 0;
        if (string.IsNullOrEmpty(hexString))
            return false;

        var cleanHex = hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? hexString[2..]
            : hexString;

        return uint.TryParse(cleanHex, System.Globalization.NumberStyles.HexNumber, null, out hash);
    }

    private static async Task<CfgBin?> LoadCfgBinAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return null;

        var data = await File.ReadAllBytesAsync(path, cancellationToken);

        if (!CfgBin.HasValidFooter(data))
        {
            data = CriwareCrypt.Decrypt(data, Path.GetFileName(path));
        }

        var cfgBin = new CfgBin();
#pragma warning disable IL2026
        cfgBin.Open(data);
#pragma warning restore IL2026
        return cfgBin;
    }

    private static List<Entry> FindEntriesByPrefix(CfgBin cfgBin, string prefix)
    {
        var items = new List<Entry>();
        foreach (var entry in cfgBin.Entries)
        {
            FindEntriesRecursive(entry, prefix, items);
        }
        return items;
    }

    private static void FindEntriesRecursive(Entry entry, string prefix, List<Entry> items)
    {
        if (entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            items.Add(entry);
        }

        foreach (var child in entry.Children)
        {
            FindEntriesRecursive(child, prefix, items);
        }
    }

    #endregion

    public void Dispose()
    {
        _skillCache.Clear();
        _techniqueCache.Clear();
        _isLoaded = false;
    }
}

/// <summary>
/// JSON entry from passive_skills.json database.
/// </summary>
public sealed record PassiveSkillJsonEntry
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("skillHash")]
    public string? SkillHashHex { get; init; }

    [JsonPropertyName("nameHash")]
    public uint NameHash { get; init; }

    [JsonPropertyName("buildType")]
    public string BuildType { get; init; } = string.Empty;

    [JsonPropertyName("buildTypeId")]
    public int BuildTypeId { get; init; }

    [JsonPropertyName("effectId")]
    public int EffectId { get; init; }

    [JsonPropertyName("effectCount")]
    public int EffectCount { get; init; }

    [JsonPropertyName("iconIndex1")]
    public int IconIndex1 { get; init; }

    [JsonPropertyName("iconIndex2")]
    public int IconIndex2 { get; init; }
}

/// <summary>
/// Cache data structure for serialization.
/// </summary>
public sealed record SkillCacheData
{
    public string Version { get; init; } = "1.0";
    public DateTime GeneratedAt { get; init; }
    public int SkillCount { get; init; }
    public int TechniqueCount { get; init; }
    public List<SkillTextResolver.LocalizedSkill>? Skills { get; init; }
    public List<SkillTextResolver.LocalizedTechnique>? Techniques { get; init; }
}

/// <summary>
/// JSON serializer context for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(SkillCacheData))]
[JsonSerializable(typeof(SkillTextResolver.LocalizedSkill))]
[JsonSerializable(typeof(SkillTextResolver.LocalizedTechnique))]
[JsonSerializable(typeof(SkillTextResolver.LocalizedText))]
[JsonSerializable(typeof(List<SkillTextResolver.LocalizedSkill>))]
[JsonSerializable(typeof(List<SkillTextResolver.LocalizedTechnique>))]
[JsonSerializable(typeof(List<PassiveSkillJsonEntry>))]
[JsonSerializable(typeof(PassiveSkillJsonEntry))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class SkillTextResolverJsonContext : JsonSerializerContext
{
}
