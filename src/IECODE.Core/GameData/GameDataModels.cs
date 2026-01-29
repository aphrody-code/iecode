using System.Text.Json.Serialization;
using IECODE.Core.Formats.Level5.CfgBin;
using IECODE.Core.Formats.Level5.CfgBin.Logic;

namespace IECODE.Core.GameData;

/// <summary>
/// Base class for game data entries mapped from generic CfgBin entries.
/// </summary>
public abstract class GameDataEntry
{
    [JsonIgnore]
    public Entry OriginalEntry { get; set; }

    protected GameDataEntry(Entry entry)
    {
        OriginalEntry = entry;
        MapFromEntry(entry);
    }

    protected abstract void MapFromEntry(Entry entry);

    protected int GetInt(Entry entry, int index, int defaultValue = 0)
    {
        if (index >= entry.Variables.Count) return defaultValue;
        return entry.Variables[index].GetInt32();
    }

    protected float GetFloat(Entry entry, int index, float defaultValue = 0f)
    {
        if (index >= entry.Variables.Count) return defaultValue;
        return entry.Variables[index].GetSingle();
    }

    protected string GetString(Entry entry, int index, string defaultValue = "")
    {
        if (index >= entry.Variables.Count) return defaultValue;
        return entry.Variables[index].GetString() ?? defaultValue;
    }
}

public class SubtitleData : GameDataEntry
{
    public int TextIdHash { get; set; }
    public float StartTime { get; set; }
    public float EndTime { get; set; }
    
    // Sometimes there are multiple time ranges?
    public float StartTime2 { get; set; }
    public float EndTime2 { get; set; }

    public SubtitleData(Entry entry) : base(entry) { }

    protected override void MapFromEntry(Entry entry)
    {
        // Based on EV_SUBTITLE_DATA analysis
        // Variable_0: Int (Text ID / Hash)
        // Variable_1: Float (Start Time)
        // Variable_2: Float (End Time) or Int?
        // Variable_3: Float (Start Time 2?)
        // Variable_4: Float (End Time 2?)

        TextIdHash = GetInt(entry, 0);
        StartTime = GetFloat(entry, 1);
        
        // Variable_2 can be float or int based on JSON sample, but usually float for time
        // In the sample: Variable_2 was Int 9 in one case, Float 23.75 in another.
        // This implies the structure might vary or it's a union?
        // Let's try to read as float if possible.
        
        var v2 = entry.Variables.Count > 2 ? entry.Variables[2] : null;
        if (v2 != null)
        {
            if (v2.Type == VariableType.Float && v2.Value is float f) EndTime = f;
            else if (v2.Type == VariableType.Int && v2.Value is int i) EndTime = i;
        }
    }
}

public class AuraSkillConfig : GameDataEntry
{
    public int EffectId { get; set; }
    public int Duration { get; set; }
    public int Param1 { get; set; }
    public int Param2 { get; set; }

    public AuraSkillConfig(Entry entry) : base(entry) { }

    protected override void MapFromEntry(Entry entry)
    {
        // AURA_CMD_UNIQUE_EFFECT
        // Variable_0: Int (Likely ID)
        EffectId = GetInt(entry, 0);
        Duration = GetInt(entry, 1); // Guess
        Param1 = GetInt(entry, 2);
        Param2 = GetInt(entry, 3);
    }
}

public class ItemConfig : GameDataEntry
{
    public int ItemId { get; set; }
    public int NameId { get; set; }
    public int Price { get; set; }
    public int Rarity { get; set; }
    public string? ScriptName { get; set; }

    public ItemConfig(Entry entry) : base(entry) { }

    protected override void MapFromEntry(Entry entry)
    {
        // ITEM_CONSUME_INFO / ITEM_EQUIP_INFO
        // Based on sample:
        // Variable_0: Int (1594826412) -> Likely ID
        // Variable_11: String ("btl_re000001") -> Script Name / Resource ID
        
        ItemId = GetInt(entry, 0);
        // Mapping other fields requires more analysis, but we can map what we see
        ScriptName = GetString(entry, 11);
    }
}

public class CharacterBaseInfo : GameDataEntry
{
    public int CharacterId { get; set; }
    public string? ModelId { get; set; }
    public int NameId { get; set; }

    public CharacterBaseInfo(Entry entry) : base(entry) { }

    protected override void MapFromEntry(Entry entry)
    {
        // CHARA_BASE_INFO (from CharacterDataPipeline)
        // Variable_0: ID?
        // Variable_1: Model ID (String)
        // Variable_3: Name ID (Int)

        CharacterId = GetInt(entry, 0);
        ModelId = GetString(entry, 1);
        NameId = GetInt(entry, 3);
    }
}

public class NounInfo : GameDataEntry
{
    public int Crc32 { get; set; }
    public string Name { get; set; } = "";

    public NounInfo(Entry entry) : base(entry) { }

    protected override void MapFromEntry(Entry entry)
    {
        Crc32 = GetInt(entry, 0);
        Name = GetString(entry, 5);
    }
}

public class CharacterParam : GameDataEntry
{
    public int CharacterId { get; set; }
    public int Kick { get; set; }
    public int Control { get; set; }
    public int Technic { get; set; }
    public int Intelligence { get; set; }
    public int Pressure { get; set; }
    public int Agility { get; set; }
    public int Physical { get; set; }
    
    public CharacterParam(Entry entry) : base(entry) { }

    protected override void MapFromEntry(Entry entry)
    {
        CharacterId = GetInt(entry, 0);
        // Based on PLAYER_STATS_SYSTEM_V2.md and common Level-5 patterns
        Kick = GetInt(entry, 1);
        Control = GetInt(entry, 2);
        Technic = GetInt(entry, 3);
        Intelligence = GetInt(entry, 4);
        Pressure = GetInt(entry, 5);
        Agility = GetInt(entry, 6);
        Physical = GetInt(entry, 7);
    }
}

public class PassiveSkillConfig : GameDataEntry
{
    public int SkillId { get; set; }
    public float EffectValue { get; set; }

    public PassiveSkillConfig(Entry entry) : base(entry) { }

    protected override void MapFromEntry(Entry entry)
    {
        // PASSIVE_SKILL_EFFECT
        // Variable_0: Int (ID)
        // Variable_1: Float (Value)
        SkillId = GetInt(entry, 0);
        EffectValue = GetFloat(entry, 1);
    }
}

public class PassiveSkillText : GameDataEntry
{
    public int TextId { get; set; }
    public string Description { get; set; } = "";

    public PassiveSkillText(Entry entry) : base(entry) { }

    protected override void MapFromEntry(Entry entry)
    {
        // TEXT_INFO
        // Variable_0: Int (ID)
        // Variable_2: String (Description)
        TextId = GetInt(entry, 0);
        Description = GetString(entry, 2);
    }
}
