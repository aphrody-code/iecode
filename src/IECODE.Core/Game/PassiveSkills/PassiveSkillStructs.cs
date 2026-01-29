using System.Runtime.InteropServices;

namespace IECODE.Core.Game.PassiveSkills;

/// <summary>
/// Build type categories for passive skills.
/// Mapped from CFG.BIN data analysis.
/// </summary>
public enum PassiveSkillBuildType : int
{
    /// <summary>Default/Unknown build type.</summary>
    Default = 0,
    
    /// <summary>ひっさつ (Hissatsu) - Offensive power.</summary>
    Knockout = 1,
    
    /// <summary>テンション (Tenshon) - Momentum/spirit effects.</summary>
    Tension = 2,
    
    /// <summary>カウンター (Kauntā) - Defensive counter-attacks.</summary>
    Counter = 3,
    
    /// <summary>キズナ (Kizuna) - Bond/link effects.</summary>
    Kizuna = 4,
    
    /// <summary>ラフプレイ (Rafu Purei) - Physical play effects.</summary>
    RoughPlay = 5,
    
    /// <summary>せいぎ (Seigi) - Justice/Fair play bonuses.</summary>
    Justice = 6,
}

/// <summary>
/// Effect type categories based on hash analysis.
/// These hashes identify what stat/attribute the effect modifies.
/// </summary>
public enum PassiveSkillEffectType : uint
{
    /// <summary>KICK Power boost (Effect IDs 0-9). Values: 50%-150%.</summary>
    KickPower = 0x8A52A068,      // -1974296472 signed
    
    /// <summary>GUARD Power boost (Effect IDs 10-19). Values: 50%-110%.</summary>
    GuardPower = 0x135BF1D2,     // 324792786 signed
    
    /// <summary>CATCH Power boost (Effect IDs 20-29). Values: 100%-350% (GK-focused).</summary>
    CatchPower = 0x645CC144,     // 1683800388 signed
    
    /// <summary>BODY Power boost (Effect IDs 30-39). Values: 50%-150%.</summary>
    BodyPower = 0xFA3854E7,      // -96971545 signed
    
    /// <summary>CONTROL Power boost (Effect IDs 40-49). Values: 50%-150%.</summary>
    ControlPower = 0x8D3F6471,   // -1925225359 signed
    
    /// <summary>SPEED Power boost (Effect IDs 50-59). Values: 50%-150%.</summary>
    SpeedPower = 0x143635CB,     // 339097035 signed
    
    /// <summary>STAMINA boost (Effect IDs 60-69). Values: 200%-450% (highest).</summary>
    StaminaPower = 0x6331055D,   // 1664157021 signed
    
    /// <summary>Miscellaneous effects (Effect IDs 70-79). Values: 50%-150%.</summary>
    Miscellaneous = 0xF38E18CC,  // -208791348 signed
}

/// <summary>
/// Known hash values for effect data and display.
/// </summary>
public static class PassiveSkillHashes
{
    /// <summary>Common effect data marker (found in EFFECT_DATA).</summary>
    public const uint EffectDataMarker = 0x1FE57E95;  // 535144085 signed
    
    /// <summary>Grand total display hash.</summary>
    public const uint GrandTotalMarker = 0x2501C856; // 621144150 signed
    
    /// <summary>Sentinel value indicating unused field.</summary>
    public const int SentinelValue = -992181094;     // 0xC4C43E1A
    
    /// <summary>Gets the effect type name from hash.</summary>
    public static string GetEffectTypeName(uint hash) => hash switch
    {
        (uint)PassiveSkillEffectType.KickPower => "KICK",
        (uint)PassiveSkillEffectType.GuardPower => "GUARD",
        (uint)PassiveSkillEffectType.CatchPower => "CATCH",
        (uint)PassiveSkillEffectType.BodyPower => "BODY",
        (uint)PassiveSkillEffectType.ControlPower => "CONTROL",
        (uint)PassiveSkillEffectType.SpeedPower => "SPEED",
        (uint)PassiveSkillEffectType.StaminaPower => "STAMINA",
        (uint)PassiveSkillEffectType.Miscellaneous => "MISC",
        _ => $"UNKNOWN_{hash:X8}"
    };
}

/// <summary>
/// Passive skill execution timing.
/// Determines when the effect triggers during a match.
/// </summary>
public enum PassiveSkillTiming : int
{
    /// <summary>Effect is always active.</summary>
    Always = 1,
    
    /// <summary>Triggers on ball contact.</summary>
    OnBallTouch = 2,
    
    /// <summary>Triggers on shoot action.</summary>
    OnShoot = 3,
    
    /// <summary>Triggers on dribble action.</summary>
    OnDribble = 4,
    
    /// <summary>Triggers on tackle action.</summary>
    OnTackle = 5,
    
    /// <summary>Triggers at match start.</summary>
    OnMatchStart = 6,
    
    /// <summary>Triggers on goal scored.</summary>
    OnGoalScored = 7,
    
    /// <summary>Triggers on goal conceded.</summary>
    OnGoalConceded = 8,
}

/// <summary>
/// Passive skill target type.
/// Determines who is affected by the skill effect.
/// </summary>
public enum PassiveSkillTarget : int
{
    /// <summary>Affects the skill holder.</summary>
    Self = 1,
    
    /// <summary>Affects allied team members.</summary>
    Ally = 2,
    
    /// <summary>Affects enemy team members.</summary>
    Enemy = 3,
    
    /// <summary>Affects entire team.</summary>
    Team = 4,
    
    /// <summary>Affects the ball.</summary>
    Ball = 5,
    
    /// <summary>Affects goalkeeper.</summary>
    Goalkeeper = 6,
}

/// <summary>
/// Passive Skill Entry from passive_skill_config.cfg.bin.
/// Structure: PASSIVE_SKILL_INFO_{N}
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PassiveSkillInfo
{
    /// <summary>CRC32 hash of skill identifier (e.g., "PSK_001").</summary>
    public uint SkillHash;
    
    /// <summary>Hash for text table lookup (name/description).</summary>
    public uint NameHash;
    
    /// <summary>Reserved field (usually 0).</summary>
    public int Reserved0;
    
    /// <summary>Reserved field (usually 0).</summary>
    public int Reserved1;
    
    /// <summary>Build type category (0-6).</summary>
    public int BuildType;
    
    /// <summary>Reserved field (usually 0).</summary>
    public int Reserved2;
    
    /// <summary>Gets the build type as enum.</summary>
    public readonly PassiveSkillBuildType GetBuildType() => (PassiveSkillBuildType)BuildType;
}

/// <summary>
/// Effect reference linking passive to effect implementation.
/// Structure: PASSIVE_SKILL_INFO_REF_EFFECT_{N}
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PassiveSkillEffectRef
{
    /// <summary>Reference ID to PASSIVE_SKILL_EFFECT_INFO table.</summary>
    public int EffectId;
    
    /// <summary>Number of effects in this passive (usually 1-3).</summary>
    public int EffectCount;
}

/// <summary>
/// Icon reference for buff display.
/// Structure: PASSIVE_SKILL_INFO_REF_BUFF_ICON_{N}
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PassiveSkillBuffIcon
{
    /// <summary>Primary buff icon index.</summary>
    public int IconIndex1;
    
    /// <summary>Secondary icon index (overlay or variant).</summary>
    public int IconIndex2;
}

/// <summary>
/// Complete passive skill definition with all references.
/// </summary>
public class PassiveSkillDefinition
{
    /// <summary>Unique skill ID (from table index).</summary>
    public int Id { get; set; }
    
    /// <summary>Main skill info structure.</summary>
    public PassiveSkillInfo Info { get; set; }
    
    /// <summary>Buff icon reference.</summary>
    public PassiveSkillBuffIcon BuffIcon { get; set; }
    
    /// <summary>Effect reference.</summary>
    public PassiveSkillEffectRef EffectRef { get; set; }
    
    /// <summary>Resolved skill name (from text table).</summary>
    public string? Name { get; set; }
    
    /// <summary>Resolved skill description (from text table).</summary>
    public string? Description { get; set; }
    
    /// <summary>Build type as string.</summary>
    public string BuildTypeName => Info.GetBuildType().ToString();
}

/// <summary>
/// Effect condition data for timing/target/execution.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PassiveSkillCondition
{
    /// <summary>Condition type hash.</summary>
    public uint ConditionHash;
    
    /// <summary>Condition parameter 1.</summary>
    public int Param1;
    
    /// <summary>Condition parameter 2.</summary>
    public int Param2;
}

/// <summary>
/// Effect data containing actual stat modifications.
/// Structure: PASSIVE_SKILL_EFFECT_INFO_EFFECT_DATA
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PassiveSkillEffectData
{
    /// <summary>Effect type hash (e.g., "STAT_BOOST_KICK").</summary>
    public uint EffectTypeHash;
    
    /// <summary>Modifier value (percentage, flat value, etc.).</summary>
    public int ModifierValue;
    
    /// <summary>Additional effect parameter.</summary>
    public int EffectParam;
}

/// <summary>
/// Grand total info with buff/build type icons.
/// Structure: PASSIVE_SKILL_EFFECT_INFO_GRAND_TOTAL_INFO
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PassiveSkillGrandTotalInfo
{
    /// <summary>Category hash.</summary>
    public uint CategoryHash;
    
    /// <summary>Percentage value (e.g., 26 = 26% boost).</summary>
    public int PercentageValue;
    
    /// <summary>Build type icon reference.</summary>
    public int BuildTypeIcon;
    
    /// <summary>Buff icon reference.</summary>
    public int BuffIconId;
}

/// <summary>
/// Complete effect definition from passive_skill_effect_config.cfg.bin.
/// </summary>
public class PassiveSkillEffect
{
    /// <summary>Effect ID matching EffectRef.EffectId.</summary>
    public int Id { get; set; }
    
    /// <summary>When this effect triggers.</summary>
    public List<PassiveSkillCondition> ExecTimingConditions { get; set; } = [];
    
    /// <summary>Who is affected by this effect.</summary>
    public List<PassiveSkillCondition> TargetConditions { get; set; } = [];
    
    /// <summary>Conditions for effect execution.</summary>
    public List<PassiveSkillCondition> ExecConditions { get; set; } = [];
    
    /// <summary>Actual effect modifications.</summary>
    public List<PassiveSkillEffectData> Effects { get; set; } = [];
    
    /// <summary>Display info for UI.</summary>
    public PassiveSkillGrandTotalInfo GrandTotalInfo { get; set; }
}

/// <summary>
/// Player's equipped passive skills.
/// Maps to in-memory player character data.
/// </summary>
public class PlayerPassiveSkillSlots
{
    /// <summary>Maximum slots (5 for ★5 players).</summary>
    public const int MaxSlots = 5;
    
    /// <summary>Equipped skill IDs per slot.</summary>
    public int[] SlotSkillIds { get; } = new int[MaxSlots];
    
    /// <summary>Whether each slot is enabled.</summary>
    public bool[] SlotEnabled { get; } = new bool[MaxSlots];
    
    /// <summary>Player rarity (determines available slots).</summary>
    public int Rarity { get; set; }
    
    /// <summary>Gets number of unlocked slots based on rarity.</summary>
    public int UnlockedSlots => Math.Min(Rarity, MaxSlots);
    
    /// <summary>Gets equipped skill at specified slot (0-indexed).</summary>
    public int GetSlotSkill(int slot) => slot < UnlockedSlots ? SlotSkillIds[slot] : 0;
    
    /// <summary>Checks if slot is available and enabled.</summary>
    public bool IsSlotActive(int slot) => slot < UnlockedSlots && SlotEnabled[slot];
}

/// <summary>
/// Ability learning type info from ability_learning_config.cfg.bin.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AbilityLearningTypeInfo
{
    /// <summary>Learning type ID (0-71).</summary>
    public int TypeId;
    
    /// <summary>Unknown flag (usually 1).</summary>
    public int Flag1;
    
    /// <summary>Category type (usually 2).</summary>
    public int Category;
    
    /// <summary>Count or index value.</summary>
    public int Count;
}
