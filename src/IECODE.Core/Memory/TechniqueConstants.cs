namespace IECODE.Core.Memory;

/// <summary>
/// Constantes pour l'édition des techniques (Hissatsu) dans nie.exe v1.5.1.
/// Structure mémoire basée sur l'analyse du TeamEditorToolViewModel.
/// </summary>
public static class TechniqueConstants
{
    #region Memory Offsets

    /// <summary>
    /// RVA du pointeur vers le Game State.
    /// </summary>
    public const int RVA_GAME_STATE = 0x01FE7438;

    /// <summary>
    /// Offset de la base des données d'équipe depuis Game State.
    /// </summary>
    public const int TEAM_DATA_OFFSET = 0x1000;

    /// <summary>
    /// Taille d'une entrée joueur en mémoire (bytes).
    /// </summary>
    public const int PLAYER_ENTRY_SIZE = 0x2B0;

    /// <summary>
    /// Nombre maximum de joueurs dans l'équipe.
    /// </summary>
    public const int MAX_TEAM_SIZE = 16;

    /// <summary>
    /// Offset des slots de techniques depuis le début de l'entrée joueur.
    /// </summary>
    public const int OFFSET_SKILL_SLOTS = 0x180;

    /// <summary>
    /// Taille d'un slot de technique (bytes).
    /// Structure: Hash (4) + Level (2) + Flags (2)
    /// </summary>
    public const int SKILL_SLOT_SIZE = 8;

    /// <summary>
    /// Nombre maximum de slots de techniques par joueur.
    /// </summary>
    public const int MAX_SKILL_SLOTS = 5;

    #endregion

    #region Technique Hashes (CRC32 signed)

    /// <summary>
    /// Techniques cachées découvertes dans le dossier _test/_inazuma.
    /// Hash = CRC32 signed du nom interne.
    /// </summary>
    public static class HiddenTechniques
    {
        // ═══════════════════════════════════════════════════════════
        // Techniques de Tir (whs = Hissatsu Shoot)
        // ═══════════════════════════════════════════════════════════

        /// <summary>whs0001 - Tornade de Feu</summary>
        public const int FIRE_TORNADO = -424694437;  // 0xE6A11CAB

        /// <summary>whs0002 - Blizzard Éternel</summary>
        public const int ETERNAL_BLIZZARD = -1195687657;  // 0xB8B45C97

        /// <summary>whs0016 - Death Drop</summary>
        public const int DEATH_DROP = 1313936124;  // 0x4E4E0AFC

        /// <summary>whs0025 - Ouragan Blanc</summary>
        public const int WHITE_HURRICANE = -571395476;  // 0xDDE5C26C

        /// <summary>whs0126 - Ouragan Cyclonique (arashi_tatsumaki) ⭐</summary>
        public const int ARASHI_TATSUMAKI = -1443946840;  // 0xA9EF1EA8

        /// <summary>whs0124 - BIG BANG ⭐⭐</summary>
        public const int BIG_BANG = 606920155;  // 0x242E225B

        /// <summary>whs0125 - SUPER NOVA ⭐⭐</summary>
        public const int SUPER_NOVA = -1680605012;  // 0x9BCA77AC

        /// <summary>whs0119 - Galactica Fall ⭐</summary>
        public const int GALACTICA_FALL = -232088912;  // 0xF22B02B0

        /// <summary>whs0121 - The Earth Infinity ⭐</summary>
        public const int THE_EARTH_INFINITY = -1539880698;  // 0xA4252F06

        /// <summary>whs0062 - Chaos Meteor</summary>
        public const int CHAOS_METEOR = 1091282927;  // 0x410EC7EF

        /// <summary>whs0095 - Dark Matter</summary>
        public const int DARK_MATTER = -1833890217;  // 0x92B95EC7

        /// <summary>whs0044 - Dragon Blaster</summary>
        public const int DRAGON_BLASTER = 462534393;  // 0x1B926EF9

        /// <summary>whs0006 - Pingouin Impérial N°7</summary>
        public const int EMPEROR_PENGUIN_7 = -1665780253;  // 0x9CA63DE3

        /// <summary>whs0048 - Tempête Dimensionnelle</summary>
        public const int DIMENSION_STORM = 1726787862;  // 0x66F90516

        /// <summary>whs0052 - Astro Break</summary>
        public const int ASTRO_BREAK = 1355067301;  // 0x50C1FF95

        // ═══════════════════════════════════════════════════════════
        // Techniques de Gardien (whk = Hissatsu Keeper)
        // ═══════════════════════════════════════════════════════════

        /// <summary>whk0012 - God Hand</summary>
        public const int GOD_HAND = 1251099195;  // 0x4A90E2BB

        /// <summary>whk0001 - Majin The Hand</summary>
        public const int MAJIN_THE_HAND = -1034700979;  // 0xC2517A4D

        /// <summary>whk0030 - God Hand X</summary>
        public const int GOD_HAND_X = 1854422268;  // 0x6E8FA4FC

        /// <summary>whk0028 - Mugen The Hand</summary>
        public const int MUGEN_THE_HAND = 1152050574;  // 0x44ACA98E

        /// <summary>whk0013 - God Hand V</summary>
        public const int GOD_HAND_V = -498582024;  // 0xE2460A78

        // ═══════════════════════════════════════════════════════════
        // Techniques de Défense (whd = Hissatsu Defense)
        // ═══════════════════════════════════════════════════════════

        /// <summary>whd0001 - The Wall</summary>
        public const int THE_WALL = 1295988424;  // 0x4D3E74C8

        /// <summary>whd0011 - Ice Ground</summary>
        public const int ICE_GROUND = -1715377626;  // 0x99C64B26

        /// <summary>whd0015 - Dimension Cut</summary>
        public const int DIMENSION_CUT = 1020177426;  // 0x3CDA5412

        // ═══════════════════════════════════════════════════════════
        // Techniques Offensives/Dribble (who = Hissatsu Offensive)
        // ═══════════════════════════════════════════════════════════

        /// <summary>who0002 - Illusion Ball</summary>
        public const int ILLUSION_BALL = 1688523825;  // 0x64A4F031

        /// <summary>who0018 - Sky Walk</summary>
        public const int SKY_WALK = -168854786;  // 0xF5E8317E

        /// <summary>who0027 - Heaven's Time</summary>
        public const int HEAVENS_TIME = -1182413447;  // 0xB98266F9

        /// <summary>who0036 - Spark Edge Dribble</summary>
        public const int SPARK_EDGE_DRIBBLE = 1403506906;  // 0x53A7A6DA
    }

    /// <summary>
    /// Noms affichables des techniques.
    /// </summary>
    public static readonly Dictionary<int, string> TechniqueNames = new()
    {
        // Shoots
        { HiddenTechniques.FIRE_TORNADO, "Tornade de Feu" },
        { HiddenTechniques.ETERNAL_BLIZZARD, "Blizzard Éternel" },
        { HiddenTechniques.DEATH_DROP, "Death Drop" },
        { HiddenTechniques.WHITE_HURRICANE, "Ouragan Blanc" },
        { HiddenTechniques.ARASHI_TATSUMAKI, "Ouragan Cyclonique ⭐" },
        { HiddenTechniques.BIG_BANG, "BIG BANG ⭐⭐" },
        { HiddenTechniques.SUPER_NOVA, "SUPER NOVA ⭐⭐" },
        { HiddenTechniques.GALACTICA_FALL, "Galactica Fall ⭐" },
        { HiddenTechniques.THE_EARTH_INFINITY, "The Earth Infinity ⭐" },
        { HiddenTechniques.CHAOS_METEOR, "Chaos Meteor" },
        { HiddenTechniques.DARK_MATTER, "Dark Matter" },
        { HiddenTechniques.DRAGON_BLASTER, "Dragon Blaster" },
        { HiddenTechniques.EMPEROR_PENGUIN_7, "Pingouin Impérial N°7" },
        { HiddenTechniques.DIMENSION_STORM, "Tempête Dimensionnelle" },
        { HiddenTechniques.ASTRO_BREAK, "Astro Break" },
        
        // Keepers
        { HiddenTechniques.GOD_HAND, "God Hand" },
        { HiddenTechniques.MAJIN_THE_HAND, "Majin The Hand" },
        { HiddenTechniques.GOD_HAND_X, "God Hand X" },
        { HiddenTechniques.MUGEN_THE_HAND, "Mugen The Hand" },
        { HiddenTechniques.GOD_HAND_V, "God Hand V" },
        
        // Defense
        { HiddenTechniques.THE_WALL, "The Wall" },
        { HiddenTechniques.ICE_GROUND, "Ice Ground" },
        { HiddenTechniques.DIMENSION_CUT, "Dimension Cut" },
        
        // Offensive
        { HiddenTechniques.ILLUSION_BALL, "Illusion Ball" },
        { HiddenTechniques.SKY_WALK, "Sky Walk" },
        { HiddenTechniques.HEAVENS_TIME, "Heaven's Time" },
        { HiddenTechniques.SPARK_EDGE_DRIBBLE, "Spark Edge Dribble" },
    };

    /// <summary>
    /// Catégories de techniques.
    /// </summary>
    public static readonly Dictionary<int, string> TechniqueCategories = new()
    {
        // Shoots (whs)
        { HiddenTechniques.FIRE_TORNADO, "Tir" },
        { HiddenTechniques.ETERNAL_BLIZZARD, "Tir" },
        { HiddenTechniques.DEATH_DROP, "Tir" },
        { HiddenTechniques.WHITE_HURRICANE, "Tir" },
        { HiddenTechniques.ARASHI_TATSUMAKI, "Tir" },
        { HiddenTechniques.BIG_BANG, "Tir" },
        { HiddenTechniques.SUPER_NOVA, "Tir" },
        { HiddenTechniques.GALACTICA_FALL, "Tir" },
        { HiddenTechniques.THE_EARTH_INFINITY, "Tir" },
        { HiddenTechniques.CHAOS_METEOR, "Tir" },
        { HiddenTechniques.DARK_MATTER, "Tir" },
        { HiddenTechniques.DRAGON_BLASTER, "Tir" },
        { HiddenTechniques.EMPEROR_PENGUIN_7, "Tir" },
        { HiddenTechniques.DIMENSION_STORM, "Tir" },
        { HiddenTechniques.ASTRO_BREAK, "Tir" },
        
        // Keepers (whk)
        { HiddenTechniques.GOD_HAND, "Gardien" },
        { HiddenTechniques.MAJIN_THE_HAND, "Gardien" },
        { HiddenTechniques.GOD_HAND_X, "Gardien" },
        { HiddenTechniques.MUGEN_THE_HAND, "Gardien" },
        { HiddenTechniques.GOD_HAND_V, "Gardien" },
        
        // Defense (whd)
        { HiddenTechniques.THE_WALL, "Défense" },
        { HiddenTechniques.ICE_GROUND, "Défense" },
        { HiddenTechniques.DIMENSION_CUT, "Défense" },
        
        // Offensive (who)
        { HiddenTechniques.ILLUSION_BALL, "Dribble" },
        { HiddenTechniques.SKY_WALK, "Dribble" },
        { HiddenTechniques.HEAVENS_TIME, "Dribble" },
        { HiddenTechniques.SPARK_EDGE_DRIBBLE, "Dribble" },
    };

    #endregion

    #region Utility Methods

    /// <summary>
    /// Obtient le nom d'une technique depuis son hash.
    /// </summary>
    public static string GetTechniqueName(int hash)
    {
        return TechniqueNames.TryGetValue(hash, out var name) 
            ? name 
            : $"Technique 0x{(uint)hash:X8}";
    }

    /// <summary>
    /// Obtient la catégorie d'une technique depuis son hash.
    /// </summary>
    public static string GetTechniqueCategory(int hash)
    {
        return TechniqueCategories.TryGetValue(hash, out var category) 
            ? category 
            : "Inconnu";
    }

    /// <summary>
    /// Liste toutes les techniques disponibles.
    /// </summary>
    public static IEnumerable<(int Hash, string Name, string Category)> GetAllTechniques()
    {
        foreach (var (hash, name) in TechniqueNames)
        {
            var category = GetTechniqueCategory(hash);
            yield return (hash, name, category);
        }
    }

    #endregion
}
