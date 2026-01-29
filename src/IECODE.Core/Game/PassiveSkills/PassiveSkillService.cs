using IECODE.Core.Formats.Level5.CfgBin;
using IECODE.Core.Formats.Level5.CfgBin.Logic;

namespace IECODE.Core.Game.PassiveSkills;

/// <summary>
/// Service for loading and parsing passive skill data from CFG.BIN files.
/// Supports passive_skill_config.cfg.bin and passive_skill_effect_config.cfg.bin.
/// </summary>
public sealed class PassiveSkillService
{
    private const string PassiveSkillInfoPrefix = "PASSIVE_SKILL_INFO_";
    private const string PassiveSkillInfoRefEffectPrefix = "PASSIVE_SKILL_INFO_REF_EFFECT_";
    private const string PassiveSkillInfoRefBuffIconPrefix = "PASSIVE_SKILL_INFO_REF_BUFF_ICON_";
    private const string PassiveSkillEffectInfoPrefix = "PASSIVE_SKILL_EFFECT_INFO_";

    private readonly Dictionary<int, PassiveSkillDefinition> _skills = [];
    private readonly Dictionary<int, PassiveSkillEffect> _effects = [];

    /// <summary>
    /// Gets all loaded passive skill definitions.
    /// </summary>
    public IReadOnlyDictionary<int, PassiveSkillDefinition> Skills => _skills;

    /// <summary>
    /// Gets all loaded passive skill effects.
    /// </summary>
    public IReadOnlyDictionary<int, PassiveSkillEffect> Effects => _effects;

    /// <summary>
    /// Gets passive skill by ID.
    /// </summary>
    public PassiveSkillDefinition? GetSkill(int id) => _skills.GetValueOrDefault(id);

    /// <summary>
    /// Gets effect by ID.
    /// </summary>
    public PassiveSkillEffect? GetEffect(int id) => _effects.GetValueOrDefault(id);

    /// <summary>
    /// Loads passive skill definitions from passive_skill_config.cfg.bin.
    /// </summary>
    /// <param name="data">Raw CFG.BIN file data.</param>
    /// <returns>Number of skills loaded.</returns>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("CFG.BIN parsing uses reflection.")]
    public int LoadSkillConfig(byte[] data)
    {
        if (!CfgBin.HasValidFooter(data))
            throw new InvalidDataException("Invalid cfg.bin file: missing footer.");

        var cfg = new CfgBin();
        cfg.Open(data);

        var loadedCount = 0;
        var entries = FlattenEntries(cfg.Entries);

        // Parse PASSIVE_SKILL_INFO entries
        var skillInfoEntries = entries
            .Where(e => e.Name.StartsWith(PassiveSkillInfoPrefix) && !e.Name.Contains("REF"))
            .ToList();

        foreach (var entry in skillInfoEntries)
        {
            if (!TryParseSkillId(entry.Name, PassiveSkillInfoPrefix, out int id))
                continue;

            var skillDef = new PassiveSkillDefinition { Id = id };

            // Parse PASSIVE_SKILL_INFO_{id} (6 Int variables)
            if (entry.Variables.Count >= 6)
            {
                skillDef.Info = new PassiveSkillInfo
                {
                    SkillHash = (uint)GetIntValue(entry.Variables, 0),
                    NameHash = (uint)GetIntValue(entry.Variables, 1),
                    Reserved0 = GetIntValue(entry.Variables, 2),
                    Reserved1 = GetIntValue(entry.Variables, 3),
                    BuildType = GetIntValue(entry.Variables, 4),
                    Reserved2 = GetIntValue(entry.Variables, 5)
                };
            }

            // Find matching REF_BUFF_ICON entry
            var buffIconName = $"{PassiveSkillInfoRefBuffIconPrefix}{id}";
            var buffIconEntry = entries.FirstOrDefault(e => e.Name == buffIconName);
            if (buffIconEntry?.Variables.Count >= 2)
            {
                skillDef.BuffIcon = new PassiveSkillBuffIcon
                {
                    IconIndex1 = GetIntValue(buffIconEntry.Variables, 0),
                    IconIndex2 = GetIntValue(buffIconEntry.Variables, 1)
                };
            }

            // Find matching REF_EFFECT entry
            var effectRefName = $"{PassiveSkillInfoRefEffectPrefix}{id}";
            var effectRefEntry = entries.FirstOrDefault(e => e.Name == effectRefName);
            if (effectRefEntry?.Variables.Count >= 2)
            {
                skillDef.EffectRef = new PassiveSkillEffectRef
                {
                    EffectId = GetIntValue(effectRefEntry.Variables, 0),
                    EffectCount = GetIntValue(effectRefEntry.Variables, 1)
                };
            }

            _skills[id] = skillDef;
            loadedCount++;
        }

        return loadedCount;
    }

    /// <summary>
    /// Loads passive skill effects from passive_skill_effect_config.cfg.bin.
    /// </summary>
    /// <param name="data">Raw CFG.BIN file data.</param>
    /// <returns>Number of effects loaded.</returns>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("CFG.BIN parsing uses reflection.")]
    public int LoadEffectConfig(byte[] data)
    {
        if (!CfgBin.HasValidFooter(data))
            throw new InvalidDataException("Invalid cfg.bin file: missing footer.");

        var cfg = new CfgBin();
        cfg.Open(data);

        var loadedCount = 0;
        var entries = FlattenEntries(cfg.Entries);

        // Find top-level PASSIVE_SKILL_EFFECT_INFO entries
        var effectListEntries = entries
            .Where(e => e.Name.StartsWith("PASSIVE_SKILL_EFFECT_INFO_LIST_BEG_"))
            .ToList();

        foreach (var listEntry in effectListEntries)
        {
            if (!TryParseSkillId(listEntry.Name, "PASSIVE_SKILL_EFFECT_INFO_LIST_BEG_", out int id))
                continue;

            var effect = new PassiveSkillEffect { Id = id };

            // Parse child entries for effect data
            foreach (var child in listEntry.Children)
            {
                ParseEffectEntry(child, effect);
            }

            _effects[id] = effect;
            loadedCount++;
        }

        return loadedCount;
    }

    /// <summary>
    /// Links skill definitions to their effect implementations.
    /// Call after loading both skill config and effect config.
    /// </summary>
    public void LinkSkillsToEffects()
    {
        foreach (var skill in _skills.Values)
        {
            if (skill.EffectRef.EffectId > 0 && _effects.TryGetValue(skill.EffectRef.EffectId, out var effect))
            {
                // Effect is linked via EffectRef.EffectId
                // Skills can reference effects by ID for runtime lookup
            }
        }
    }

    /// <summary>
    /// Gets skills filtered by build type.
    /// </summary>
    public IEnumerable<PassiveSkillDefinition> GetSkillsByBuildType(PassiveSkillBuildType buildType)
    {
        return _skills.Values.Where(s => s.Info.GetBuildType() == buildType);
    }

    /// <summary>
    /// Gets all skills with their effects resolved.
    /// </summary>
    public IEnumerable<(PassiveSkillDefinition Skill, PassiveSkillEffect? Effect)> GetSkillsWithEffects()
    {
        foreach (var skill in _skills.Values)
        {
            var effect = skill.EffectRef.EffectId > 0
                ? _effects.GetValueOrDefault(skill.EffectRef.EffectId)
                : null;
            yield return (skill, effect);
        }
    }

    /// <summary>
    /// Exports skill data to JSON format.
    /// </summary>
    public string ExportToJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"skills\": [");

        var skillList = _skills.Values.OrderBy(s => s.Id).ToList();
        for (int i = 0; i < skillList.Count; i++)
        {
            var skill = skillList[i];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"id\": {skill.Id},");
            sb.AppendLine($"      \"skillHash\": \"0x{skill.Info.SkillHash:X8}\",");
            sb.AppendLine($"      \"nameHash\": \"0x{skill.Info.NameHash:X8}\",");
            sb.AppendLine($"      \"buildType\": \"{skill.BuildTypeName}\",");
            sb.AppendLine($"      \"buildTypeId\": {skill.Info.BuildType},");
            sb.AppendLine($"      \"effectId\": {skill.EffectRef.EffectId},");
            sb.AppendLine($"      \"effectCount\": {skill.EffectRef.EffectCount},");
            sb.AppendLine($"      \"iconIndex1\": {skill.BuffIcon.IconIndex1},");
            sb.AppendLine($"      \"iconIndex2\": {skill.BuffIcon.IconIndex2}");
            sb.Append("    }");
            sb.AppendLine(i < skillList.Count - 1 ? "," : "");
        }

        sb.AppendLine("  ],");
        sb.AppendLine($"  \"totalSkills\": {_skills.Count},");
        sb.AppendLine($"  \"totalEffects\": {_effects.Count},");
        
        // Add effect type summary
        sb.AppendLine("  \"effectTypes\": {");
        sb.AppendLine($"    \"KICK (0x8A52A068)\": \"Power boost for kick actions (50%-150%)\",");
        sb.AppendLine($"    \"GUARD (0x135BF1D2)\": \"Power boost for guard actions (50%-110%)\",");
        sb.AppendLine($"    \"CATCH (0x645CC144)\": \"Power boost for goalkeeper catch (100%-350%)\",");
        sb.AppendLine($"    \"BODY (0xFA3854E7)\": \"Body stat boost (50%-150%)\",");
        sb.AppendLine($"    \"CONTROL (0x8D3F6471)\": \"Control stat boost (50%-150%)\",");
        sb.AppendLine($"    \"SPEED (0x143635CB)\": \"Speed stat boost (50%-150%)\",");
        sb.AppendLine($"    \"STAMINA (0x6331055D)\": \"Stamina boost (200%-450%)\",");
        sb.AppendLine($"    \"MISC (0xF38E18CC)\": \"Miscellaneous effects (50%-150%)\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Gets detailed effect analysis with decoded type names.
    /// </summary>
    public IEnumerable<EffectAnalysis> AnalyzeEffects()
    {
        foreach (var effect in _effects.Values.OrderBy(e => e.Id))
        {
            foreach (var effectData in effect.Effects)
            {
                yield return new EffectAnalysis
                {
                    EffectId = effect.Id,
                    TypeHash = effectData.EffectTypeHash,
                    TypeName = PassiveSkillHashes.GetEffectTypeName(effectData.EffectTypeHash),
                    ModifierValue = effectData.ModifierValue,
                    PercentageDisplay = effect.GrandTotalInfo.PercentageValue
                };
            }
        }
    }

    /// <summary>
    /// Gets summary statistics about loaded effects.
    /// </summary>
    public EffectStatistics GetEffectStatistics()
    {
        var stats = new EffectStatistics();
        
        foreach (var effect in _effects.Values)
        {
            foreach (var effectData in effect.Effects)
            {
                var typeName = PassiveSkillHashes.GetEffectTypeName(effectData.EffectTypeHash);
                
                if (!stats.EffectTypeCount.ContainsKey(typeName))
                {
                    stats.EffectTypeCount[typeName] = 0;
                    stats.EffectTypeMinValue[typeName] = float.MaxValue;
                    stats.EffectTypeMaxValue[typeName] = float.MinValue;
                }
                
                stats.EffectTypeCount[typeName]++;
                
                // ModifierValue is likely a float packed as int
                float value = BitConverter.Int32BitsToSingle(effectData.ModifierValue);
                if (float.IsNaN(value) || float.IsInfinity(value))
                    value = effectData.ModifierValue / 100f;
                
                stats.EffectTypeMinValue[typeName] = Math.Min(stats.EffectTypeMinValue[typeName], value);
                stats.EffectTypeMaxValue[typeName] = Math.Max(stats.EffectTypeMaxValue[typeName], value);
            }
        }
        
        return stats;
    }

    /// <summary>
    /// Clears all loaded data.
    /// </summary>
    public void Clear()
    {
        _skills.Clear();
        _effects.Clear();
    }

    #region Private Helpers

    private static List<Entry> FlattenEntries(List<Entry> entries)
    {
        var result = new List<Entry>();
        foreach (var entry in entries)
        {
            result.Add(entry);
            result.AddRange(FlattenEntries(entry.Children));
        }
        return result;
    }

    private void ParseEffectEntry(Entry entry, PassiveSkillEffect effect)
    {
        // Parse exec timing data
        if (entry.Name.Contains("EXEC_TIMING_DATA"))
        {
            if (entry.Name.Contains("COND_DATA") && !entry.Name.Contains("LIST"))
            {
                effect.ExecTimingConditions.Add(ParseCondition(entry));
            }
        }
        // Parse target data
        else if (entry.Name.Contains("TARGET_DATA"))
        {
            if (entry.Name.Contains("COND_DATA") && !entry.Name.Contains("LIST"))
            {
                effect.TargetConditions.Add(ParseCondition(entry));
            }
        }
        // Parse exec data
        else if (entry.Name.Contains("EXEC_DATA"))
        {
            if (entry.Name.Contains("COND_DATA") && !entry.Name.Contains("LIST"))
            {
                effect.ExecConditions.Add(ParseCondition(entry));
            }
            else if (entry.Name.Contains("EFFECT_DATA") && !entry.Name.Contains("LIST"))
            {
                effect.Effects.Add(ParseEffectData(entry));
            }
        }
        // Parse grand total info
        else if (entry.Name.Contains("GRAND_TOTAL_INFO") && !entry.Name.Contains("LIST") && !entry.Name.Contains("DATA"))
        {
            effect.GrandTotalInfo = ParseGrandTotalInfo(entry);
        }

        // Recurse into children
        foreach (var child in entry.Children)
        {
            ParseEffectEntry(child, effect);
        }
    }

    private static PassiveSkillCondition ParseCondition(Entry entry)
    {
        return new PassiveSkillCondition
        {
            ConditionHash = (uint)GetIntValue(entry.Variables, 0),
            Param1 = GetIntValue(entry.Variables, 1),
            Param2 = GetIntValue(entry.Variables, 2)
        };
    }

    private static PassiveSkillEffectData ParseEffectData(Entry entry)
    {
        return new PassiveSkillEffectData
        {
            EffectTypeHash = (uint)GetIntValue(entry.Variables, 0),
            ModifierValue = GetIntValue(entry.Variables, 1),
            EffectParam = GetIntValue(entry.Variables, 2)
        };
    }

    private static PassiveSkillGrandTotalInfo ParseGrandTotalInfo(Entry entry)
    {
        return new PassiveSkillGrandTotalInfo
        {
            CategoryHash = (uint)GetIntValue(entry.Variables, 0),
            PercentageValue = GetIntValue(entry.Variables, 1),
            BuildTypeIcon = GetIntValue(entry.Variables, 2),
            BuffIconId = GetIntValue(entry.Variables, 3)
        };
    }

    private static bool TryParseSkillId(string name, string prefix, out int id)
    {
        id = 0;
        if (!name.StartsWith(prefix))
            return false;

        var suffix = name[prefix.Length..];
        return int.TryParse(suffix, out id);
    }

    private static int GetIntValue(List<Variable> variables, int index)
    {
        if (index < 0 || index >= variables.Count)
            return 0;

        var variable = variables[index];
        if (variable.Value is int i)
            return i;
        if (variable.Value is float f)
            return (int)f;
        if (int.TryParse(variable.Value?.ToString(), out int parsed))
            return parsed;

        return 0;
    }

    #endregion
}

/// <summary>
/// Analysis result for a single effect.
/// </summary>
public sealed class EffectAnalysis
{
    /// <summary>Effect ID from effect config.</summary>
    public int EffectId { get; set; }
    
    /// <summary>Effect type hash (identifies stat category).</summary>
    public uint TypeHash { get; set; }
    
    /// <summary>Decoded type name (KICK, GUARD, etc.).</summary>
    public string TypeName { get; set; } = "";
    
    /// <summary>Raw modifier value from config.</summary>
    public int ModifierValue { get; set; }
    
    /// <summary>Percentage value for display.</summary>
    public int PercentageDisplay { get; set; }
    
    /// <summary>Gets modifier as float (0.5 = 50%).</summary>
    public float ModifierAsFloat => BitConverter.Int32BitsToSingle(ModifierValue);
}

/// <summary>
/// Statistics summary for loaded effects.
/// </summary>
public sealed class EffectStatistics
{
    /// <summary>Count of effects per type.</summary>
    public Dictionary<string, int> EffectTypeCount { get; } = [];
    
    /// <summary>Minimum value per type.</summary>
    public Dictionary<string, float> EffectTypeMinValue { get; } = [];
    
    /// <summary>Maximum value per type.</summary>
    public Dictionary<string, float> EffectTypeMaxValue { get; } = [];
}

