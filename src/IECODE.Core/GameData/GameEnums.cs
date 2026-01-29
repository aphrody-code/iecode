namespace IECODE.Core.GameData;

/// <summary>
/// Player rarity tiers in Inazuma Eleven: Victory Road.
/// </summary>
/// <remarks>
/// Memory offset: PLAYER_RARITY (0x3C), 2 bytes.
/// Values verified from starSignCharaInfo.json (4439 characters).
/// Galaxy&amp;LBX DLC (v1.5.0, 2025-12-21) added BASARA tier (52 eligible characters).
/// </remarks>
public enum PlayerRarity : byte
{
    /// <summary>Normal rarity (4203 characters)</summary>
    N = 0,
    
    /// <summary>Rare rarity (150 characters)</summary>
    R = 2,
    
    /// <summary>Ultra Rare rarity (27 characters)</summary>
    UR = 5,
    
    /// <summary>Legendary Rare rarity (29 characters)</summary>
    LR = 6,
    
    /// <summary>Legend rarity (30 characters) - Can use passive skills</summary>
    Legend = 7,
    
    /// <summary>
    /// BASARA rarity - Highest tier added in Galaxy&amp;LBX DLC.
    /// Can switch between all 6 builds (basaraBuildType 0-5) and have moves reassigned.
    /// Requires high-rarity Spirits from "Atrium of the Untamed" shop.
    /// 52 characters eligible (basaraBuildInfo.json).
    /// </summary>
    BASARA = 20
}

/// <summary>
/// Player positions on the field.
/// </summary>
/// <remarks>
/// Memory offset: PLAYER_POSITION (0x20), 4 bytes.
/// Values verified from growthTableMain.json (48 entries, 12 per position).
/// </remarks>
public enum PlayerPosition : byte
{
    /// <summary>Goalkeeper (position 1)</summary>
    GK = 1,
    
    /// <summary>Forward (position 2) - Highest Kick stat potential</summary>
    FW = 2,
    
    /// <summary>Midfielder (position 3)</summary>
    MF = 3,
    
    /// <summary>Defender (position 4) - Highest Intelligence stat potential</summary>
    DF = 4
}

/// <summary>
/// Player elements (attributes).
/// </summary>
/// <remarks>
/// Memory offset: PLAYER_ELEMENT (0x24), 4 bytes.
/// Values verified from chara-param.ts parser.
/// </remarks>
public enum PlayerElement : byte
{
    /// <summary>Wind element (風) - 1</summary>
    Wind = 1,
    
    /// <summary>Forest element (林) - 2</summary>
    Forest = 2,
    
    /// <summary>Fire element (火) - 3</summary>
    Fire = 3,
    
    /// <summary>Mountain element (山) - 4</summary>
    Mountain = 4
}

/// <summary>
/// Chronicle Mode routes available in the game.
/// </summary>
/// <remarks>
/// Galaxy&amp;LBX DLC (v1.5.0, 2025-12-21) added Galaxy and LBX routes.
/// </remarks>
public enum ChronicleRoute : byte
{
    /// <summary>Original Inazuma Eleven route</summary>
    Original = 0,
    
    /// <summary>Inazuma Eleven GO route</summary>
    Go = 1,
    
    /// <summary>Inazuma Eleven GO Chrono Stone route</summary>
    ChronoStone = 2,
    
    /// <summary>
    /// Inazuma Eleven GO Galaxy route.
    /// Features planet stadiums with unique effects and Totems.
    /// Added in Galaxy&amp;LBX DLC (2025-12-21).
    /// </summary>
    Galaxy = 3,
    
    /// <summary>
    /// Inazuma Eleven GO vs. Little Battlers eXperience W route.
    /// Features Inazuma Legend National and Despairdoes teams.
    /// Added in Galaxy&amp;LBX DLC (2025-12-21).
    /// </summary>
    LBX = 4,
    
    /// <summary>Inazuma Eleven GO: Shine route</summary>
    GoShine = 5,
    
    /// <summary>Inazuma Eleven GO: Dark route</summary>
    GoDark = 6,
    
    /// <summary>Inazuma Eleven GO 2 Chrono Stone: Thunderflash route</summary>
    ChronoStoneThunderflash = 7,
    
    /// <summary>Inazuma Eleven GO 2 Chrono Stone: Wildfire route</summary>
    ChronoStoneWildfire = 8
}

/// <summary>
/// Totem types for hyper-dimensional enhancement.
/// </summary>
/// <remarks>
/// Totems are a new feature in the Galaxy Route (Galaxy&amp;LBX DLC).
/// They provide special enhancements during matches.
/// Element values match PlayerElement (1-4).
/// </remarks>
public enum TotemType : byte
{
    /// <summary>No totem active</summary>
    None = 0,
    
    /// <summary>Wind Totem (風) - Enhances speed and agility</summary>
    Wind = 1,
    
    /// <summary>Forest Totem (林) - Enhances stamina recovery</summary>
    Forest = 2,
    
    /// <summary>Fire Totem (火) - Enhances attack power</summary>
    Fire = 3,
    
    /// <summary>Mountain Totem (山) - Enhances defense and stability</summary>
    Mountain = 4
}

/// <summary>
/// Spirit element types for BASARA upgrades.
/// </summary>
/// <remarks>
/// High-rarity Spirits are required at the "Atrium of the Untamed" shop
/// to obtain BASARA players (Galaxy&amp;LBX DLC feature).
/// Element values match PlayerElement.
/// </remarks>
public enum SpiritElement : byte
{
    /// <summary>Wind Spirit (風)</summary>
    Wind = 1,
    
    /// <summary>Forest Spirit (林)</summary>
    Forest = 2,
    
    /// <summary>Fire Spirit (火)</summary>
    Fire = 3,
    
    /// <summary>Mountain Spirit (山)</summary>
    Mountain = 4
}

/// <summary>
/// Game modes available in Inazuma Eleven: Victory Road.
/// </summary>
public enum GameMode : byte
{
    /// <summary>Story Mode - Follow Destin Billows' adventure</summary>
    Story = 0,
    
    /// <summary>RE Story - Replay story from beginning while keeping progression</summary>
    REStory = 1,
    
    /// <summary>Chronicle Mode - Scout 5400+ characters from past/present titles</summary>
    Chronicle = 2,
    
    /// <summary>BB Stadium - Battle various teams including Earth Eleven</summary>
    BBStadium = 3,
    
    /// <summary>Victory Road - Online tournament mode</summary>
    VictoryRoad = 4,
    
    /// <summary>2P Tag Mode - Cooperative multiplayer with buff sigils</summary>
    Tag2P = 5,
    
    /// <summary>Commander Ladder - Online commander-only ranked mode</summary>
    CommanderLadder = 6,
    
    /// <summary>Fast Commander - Accelerated bot matches</summary>
    FastCommander = 7
}

/// <summary>
/// Known teams in Inazuma Eleven: Victory Road.
/// </summary>
/// <remarks>
/// Galaxy&amp;LBX DLC (2025-12-21) added:
/// - Earth Eleven (Galaxy route)
/// - South Cirrus Junior High A/B/C (BB Stadium)
/// - Inazuma Legend National (LBX route)
/// - Despairdoes (LBX route)
/// </remarks>
public static class KnownTeams
{
    // Original teams
    public const string RaimonFirst = "RAIMON_FIRST";
    public const string RaimonSecond = "RAIMON_SECOND";
    public const string Teikoku = "TEIKOKU";
    public const string Zeus = "ZEUS";
    public const string Aliea = "ALIEA";
    public const string InazumaJapan = "INAZUMA_JAPAN";
    
    // GO teams
    public const string RaimonGo = "RAIMON_GO";
    public const string Dragonlink = "DRAGONLINK";
    public const string Protocol = "PROTOCOL_OMEGA";
    public const string Chrono = "EL_DORADO";
    
    // Galaxy teams (added in Galaxy&LBX DLC)
    public const string EarthEleven = "EARTH_ELEVEN";
    public const string Faram = "FARAM_OBIUS";
    public const string Gurdon = "GURDON_ELEVEN";
    public const string Sazanaara = "SAZANAARA_ELEVEN";
    
    // LBX teams (added in Galaxy&LBX DLC)
    public const string InazumaLegendNational = "INAZUMA_LEGEND_NATIONAL";
    public const string Despairdoes = "DESPAIRDOES";
    
    // Victory Road original teams (added in Galaxy&LBX DLC for BB Stadium)
    public const string SouthCirrusA = "SOUTH_CIRRUS_A";
    public const string SouthCirrusB = "SOUTH_CIRRUS_B";
    public const string SouthCirrusC = "SOUTH_CIRRUS_C";
}

/// <summary>
/// Planet stadium types with unique effects (Galaxy Route).
/// </summary>
/// <remarks>
/// Each planet's stadium introduces unique effects during matches.
/// Part of the Galaxy Route in Galaxy&amp;LBX DLC.
/// </remarks>
public enum PlanetStadium : byte
{
    /// <summary>Standard Earth stadium</summary>
    Earth = 0,
    
    /// <summary>Sandorius - Desert planet stadium</summary>
    Sandorius = 1,
    
    /// <summary>Sazanaara - Water planet stadium</summary>
    Sazanaara = 2,
    
    /// <summary>Ratoniik - Ice planet stadium</summary>
    Ratoniik = 3,
    
    /// <summary>Gurdon - Mechanical planet stadium</summary>
    Gurdon = 4,
    
    /// <summary>Faram Obius - Final planet stadium</summary>
    FaramObius = 5
}
