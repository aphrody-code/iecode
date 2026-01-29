namespace IECODE.Core.Memory;

/// <summary>
/// Adresses mémoire pour Inazuma Eleven Victory Road (nie.exe) v1.5.2
/// Mises à jour depuis: https://github.com/An-Average-Developer/Inazuma-Eleven-VR-Save-Editor v1.1.0
/// </summary>
/// <remarks>
/// Ces adresses sont spécifiques à la version 1.5.2 du jeu (mise à jour Décembre 2024).
/// Une mise à jour du jeu peut les invalider.
/// IMPORTANT: Utiliser AOB Scan quand possible pour une meilleure compatibilité.
/// </remarks>
public static class MemoryAddresses
{
    #region Base Addresses

    /// <summary>
    /// Adresse de base commune pour Stars, Flowers, Beans (v1.5.1)
    /// Source: IE:VR SE MemoryEditorViewModel.cs
    /// </summary>
    public const long COMMON_BASE_ADDRESS = 0x01AD1828;

    /// <summary>
    /// Adresse de base alternative pour certaines valeurs
    /// </summary>
    public const long SPIRITS_BASE_ADDRESS = 0x0208DBE8;

    /// <summary>
    /// Adresse de base pour Instantaneous Bean
    /// </summary>
    public const long BEANS_ALT_BASE_ADDRESS = 0x01AC27A8;

    #endregion

    #region Pointer Offsets - Resources

    /// <summary>
    /// Chaîne d'offsets pour Stars (Inazuma Points) - Base: COMMON_BASE_ADDRESS (0x01AD1828)
    /// </summary>
    public static readonly int[] STARS_OFFSETS = [0xFE8, 0x20F0, 0x8, 0x10, 0xA0, 0x4C];

    /// <summary>
    /// Chaîne d'offsets pour Inazuma Flowers - Base: COMMON_BASE_ADDRESS
    /// </summary>
    public static readonly int[] INAZUMA_FLOWERS_OFFSETS = [0xFE8, 0x20E0, 0x18, 0x90, 0x218];

    /// <summary>
    /// Chaîne d'offsets pour God Hand Flowers - Base: COMMON_BASE_ADDRESS
    /// </summary>
    public static readonly int[] GOD_HAND_FLOWERS_OFFSETS = [0x1148, 0x2000, 0x8, 0x170, 0xB0];

    /// <summary>
    /// Chaîne d'offsets pour Flowers (legacy alias)
    /// </summary>
    public static readonly int[] FLOWERS_OFFSETS = [0xFE8, 0x20F0, 0x8, 0x10, 0xA0, 0x48];

    #endregion

    #region Pointer Offsets - Beans (Base: BEANS_ALT_BASE_ADDRESS = 0x01AC27A8)

    /// <summary>
    /// Instantaneous Bean offsets
    /// </summary>
    public static readonly int[] INSTANTANEOUS_BEAN_OFFSETS = [0xFE8, 0x1F98, 0x60, 0x4100, 0x7A1C];

    /// <summary>
    /// Intelligence Bean offsets
    /// </summary>
    public static readonly int[] INTELLIGENCE_BEAN_OFFSETS = [0xFE8, 0x1F98, 0x70, 0x3F0, 0x12E0];

    /// <summary>
    /// Kicking Power Bean offsets
    /// </summary>
    public static readonly int[] KICKING_POWER_BEAN_OFFSETS = [0xFE8, 0x1F98, 0x70, 0x920, 0xAC4];

    /// <summary>
    /// Mind's Eye Bean offsets
    /// </summary>
    public static readonly int[] MINDS_EYE_BEAN_OFFSETS = [0xFE8, 0x1F98, 0x70, 0x6A0, 0xEA0];

    /// <summary>
    /// Strength Bean offsets
    /// </summary>
    public static readonly int[] STRENGTH_BEAN_OFFSETS = [0xFE8, 0x1F98, 0x60, 0x43A0, 0x75A0];

    /// <summary>
    /// Technique Bean offsets
    /// </summary>
    public static readonly int[] TECHNIQUE_BEAN_OFFSETS = [0xFE8, 0x1F98, 0x70, 0x1B0, 0x1624];

    /// <summary>
    /// Unshakable Bean offsets
    /// </summary>
    public static readonly int[] UNSHAKABLE_BEAN_OFFSETS = [0x1148, 0x2000, 0x68, 0x630, 0x1DE4];

    /// <summary>
    /// Legacy alias for Beans
    /// </summary>
    public static readonly int[] BEANS_OFFSETS = [0xFE8, 0x1F98, 0x60, 0x4100, 0x7A1C];

    #endregion

    #region Pointer Offsets - Victory Items

    /// <summary>
    /// Victory Star offsets - Base: 0x020B67A0
    /// </summary>
    public static readonly int[] VICTORY_STAR_OFFSETS = [0x6018, 0x20, 0x10, 0x1018, 0x20F0, 0xA0, 0x10];

    /// <summary>
    /// Base address for Victory Star
    /// </summary>
    public const long VICTORY_STAR_BASE_ADDRESS = 0x020B67A0;

    /// <summary>
    /// Victory Stone offsets - Base: 0x01AC87F8
    /// </summary>
    public static readonly int[] VICTORY_STONE_OFFSETS = [0x1148, 0x2000, 0xA0, 0x9E8, 0x10, 0x50, 0x4C];

    /// <summary>
    /// Base address for Victory Stone
    /// </summary>
    public const long VICTORY_STONE_BASE_ADDRESS = 0x01AC87F8;

    #endregion

    #region Patch Addresses (Code Injection) - Updated 2025-12-15 via binary scan

    /// <summary>
    /// Adresse pour freeze Stars: nie.exe+D9531D (scanné)
    /// Patch: NOP (0x90 x3) pour empêcher la diminution
    /// Original: mov [rax+10],edx (0x89, 0x50, 0x10)
    /// </summary>
    public const long FREEZE_STARS_ADDRESS = 0xD9531D;

    /// <summary>
    /// Adresse pour auto-increment Flowers: nie.exe+D95315 (scanné)
    /// Patch: SUB → ADD (0x2B → 0x03)
    /// </summary>
    public const long FLOWER_INCREMENT_ADDRESS = 0xD95315;

    /// <summary>
    /// Adresse de l'instruction SUB pour Beans (à vérifier)
    /// Patch: 0x2B (SUB) → 0x03 (ADD) pour auto-increment
    /// </summary>
    public const long BEAN_INCREMENT_ADDRESS = 0xD94775;

    // === FLOWER & RESOURCE FUNCTIONS (nie.c reverse engineering) ===
    
    /// <summary>
    /// Fonction de validation Inazuma Flowers: FUN_140d95090 (nie.exe+D95090)
    /// Accède à: DAT_141fe1d60 + 0x6970 → +0x187b0 (Flowers bitmap)
    /// </summary>
    public const long FLOWER_CHECK_FUNCTION = 0xD95090;

    /// <summary>
    /// Fonction de validation God Hand Flowers: FUN_140d951a0 (nie.exe+D951A0)
    /// Accède à: DAT_141fe1d60 + 0x6970 → +0x185a8 (God Hand bitmap)
    /// </summary>
    public const long GOD_HAND_CHECK_FUNCTION = 0xD951A0;

    /// <summary>
    /// Fonction de validation Resource Type 3: FUN_140d952b0 (nie.exe+D952B0)
    /// Accède à: DAT_141fe1d60 + 0x6970 → +0x18568 (Resource3 bitmap)
    /// </summary>
    public const long RESOURCE3_CHECK_FUNCTION = 0xD952B0;

    /// <summary>
    /// Fonction de modification ressources: FUN_140d95380 (nie.exe+D95380)
    /// Modifie: +0x15c68 (offline) ou +0x15e68 (online)
    /// </summary>
    public const long RESOURCE_MODIFY_FUNCTION = 0xD95380;

    /// <summary>
    /// Fonction set Flowers: FUN_140d9a440 (nie.exe+D9A440)
    /// Sets flower flag in bitmap at 0x187b0
    /// </summary>
    public const long FLOWER_SET_FUNCTION = 0xD9A440;

    /// <summary>
    /// Fonction get Flowers: FUN_140d9a4b0 (nie.exe+D9A4B0)
    /// Gets flower status from bitmap at 0x187b0
    /// </summary>
    public const long FLOWER_GET_FUNCTION = 0xD9A4B0;

    /// <summary>
    /// Fonction set God Hand Grass: FUN_140d99970 (nie.exe+D99970)
    /// Sets god hand flag in bitmap at 0x185a8
    /// </summary>
    public const long GOD_HAND_SET_FUNCTION = 0xD99970;

    /// <summary>
    /// Fonction get God Hand Grass: FUN_140d999e0 (nie.exe+D999E0)
    /// Gets god hand status from bitmap at 0x185a8
    /// </summary>
    public const long GOD_HAND_GET_FUNCTION = 0xD999E0;

    /// <summary>
    /// Fonction set Resource3: FUN_140d998b0 (nie.exe+D998B0)
    /// Sets resource3 flag in bitmap at 0x18568
    /// </summary>
    public const long RESOURCE3_SET_FUNCTION = 0xD998B0;

    /// <summary>
    /// Fonction get Resource3: FUN_140d99920 (nie.exe+D99920)
    /// Gets resource3 status from bitmap at 0x18568
    /// </summary>
    public const long RESOURCE3_GET_FUNCTION = 0xD99920;

    /// <summary>
    /// Adresse pour freeze Spirits: nie.exe+CF178A (v1.5.2 - via AOB scan)
    /// Pattern AOB: 66 89 68 0C 48
    /// </summary>
    public const long SPIRIT_FREEZE_ADDRESS = 0xCF178A;

    /// <summary>
    /// Adresse pour freeze Elite Spirits: nie.exe+CF1687 (v1.5.2 - via AOB scan)
    /// Pattern AOB: 66 41 89 6C 78 10
    /// </summary>
    public const long ELITE_SPIRIT_FREEZE_ADDRESS = 0xCF1687;

    /// <summary>
    /// Adresse pour Store Item Multiplier hook 1: nie.exe+221AD5 (v1.5.2)
    /// Pattern: 89 4E 10 8B C3 (mov [rsi+10],ecx; mov eax,ebx)
    /// </summary>
    public const long STORE_MULTIPLIER_HOOK1 = 0x221AD5;

    /// <summary>
    /// Adresse pour Store Item Multiplier hook 2: nie.exe+220A95 (v1.5.2)
    /// </summary>
    public const long STORE_MULTIPLIER_HOOK2 = 0x220A95;

    /// <summary>
    /// Adresse pour Store Item Multiplier hook 3: nie.exe+220DD5 (v1.5.2)
    /// </summary>
    public const long STORE_MULTIPLIER_HOOK3 = 0x220DD5;

    /// <summary>
    /// Adresse pour Cooldown Freeze: nie.exe+5C874
    /// Patch: SUBSS xmm0, xmm1 → NOP x4
    /// </summary>
    public const long COOLDOWN_FREEZE_ADDRESS = 0x5C874;

    /// <summary>
    /// Adresse pour Item Limit Bypass: nie.exe+D765D3 (scanné)
    /// Patch: CMP EAX, 0x3E7 (999) → CMP EAX, 0xFFFF (65535)
    /// </summary>
    public const long ITEM_LIMIT_ADDRESS = 0xD765D3;

    /// <summary>
    /// Adresse pour Universal Item Check: nie.exe+D95170
    /// Patch: Always return true (MOV AL, 1; RET)
    /// </summary>
    public const long UNIVERSAL_ITEM_CHECK_ADDRESS = 0xD95170;

    #endregion

    #region Original Bytes (pour restauration)

    /// <summary>
    /// Bytes originaux de l'instruction Stars: MOV [rax+10],edx
    /// </summary>
    public static readonly byte[] ORIGINAL_FREEZE_STARS_BYTES = [0x89, 0x50, 0x10];

    /// <summary>
    /// Bytes NOP pour remplacer l'instruction (freeze)
    /// </summary>
    public static readonly byte[] NOP_BYTES = [0x90, 0x90, 0x90];

    /// <summary>
    /// Bytes originaux Flowers: SUB ecx,ebp
    /// </summary>
    public static readonly byte[] FLOWER_ORIGINAL_BYTES = [0x2B, 0xCD];

    /// <summary>
    /// Bytes modifiés Flowers: ADD ecx,ebp
    /// </summary>
    public static readonly byte[] FLOWER_INCREMENT_BYTES = [0x03, 0xCD];

    /// <summary>
    /// Bytes originaux Spirit freeze: mov [rax+0C],bp
    /// </summary>
    public static readonly byte[] SPIRIT_ORIGINAL_BYTES = [0x66, 0x89, 0x68, 0x0C];

    /// <summary>
    /// Bytes NOP pour Spirit freeze (4 bytes)
    /// </summary>
    public static readonly byte[] SPIRIT_FREEZE_BYTES = [0x90, 0x90, 0x90, 0x90];

    /// <summary>
    /// Bytes originaux Elite Spirit freeze: mov [r8+rdi+10],bp
    /// </summary>
    public static readonly byte[] ELITE_SPIRIT_ORIGINAL_BYTES = [0x66, 0x41, 0x89, 0x6C, 0x78, 0x10];

    /// <summary>
    /// Bytes NOP pour Elite Spirit freeze (6 bytes)
    /// </summary>
    public static readonly byte[] ELITE_SPIRIT_FREEZE_BYTES = [0x90, 0x90, 0x90, 0x90, 0x90, 0x90];

    /// <summary>
    /// Bytes originaux Store multiplier: mov [rsi+10],ecx; mov eax,ebx
    /// </summary>
    public static readonly byte[] STORE_MULTIPLIER_ORIGINAL_BYTES = [0x89, 0x4E, 0x10, 0x8B, 0xC3];

    /// <summary>
    /// Bytes originaux Cooldown: SUBSS xmm0, xmm1
    /// </summary>
    public static readonly byte[] COOLDOWN_ORIGINAL_BYTES = [0xF3, 0x0F, 0x5C, 0xC1];

    /// <summary>
    /// Bytes NOP pour Cooldown freeze (4 bytes)
    /// </summary>
    public static readonly byte[] COOLDOWN_FREEZE_BYTES = [0x90, 0x90, 0x90, 0x90];

    /// <summary>
    /// Bytes originaux Item Limit: CMP EAX, 3E7h (999)
    /// </summary>
    public static readonly byte[] ITEM_LIMIT_ORIGINAL_BYTES = [0x3D, 0xE7, 0x03, 0x00, 0x00];

    /// <summary>
    /// Bytes modifiés Item Limit: CMP EAX, FFFFh (65535)
    /// </summary>
    public static readonly byte[] ITEM_LIMIT_BYPASSED_BYTES = [0x3D, 0xFF, 0xFF, 0x00, 0x00];

    /// <summary>
    /// Bytes originaux Universal Item Check: function prologue
    /// </summary>
    public static readonly byte[] ITEM_CHECK_ORIGINAL_BYTES = [0x48, 0x89, 0x5C, 0x24, 0x08];

    /// <summary>
    /// Bytes modifiés Universal Item Check: MOV AL, 1; RET; NOP; NOP
    /// </summary>
    public static readonly byte[] ITEM_CHECK_BYPASS_BYTES = [0xB0, 0x01, 0xC3, 0x90, 0x90];

    #endregion

    #region New Patches (v2.0) - Advanced Cheats

    // === PLAYER DATA STRUCTURE (0x2B0 bytes per player) ===
    // Based on reverse engineering of nie.exe

    /// <summary>
    /// Size of player structure in memory (688 bytes).
    /// </summary>
    public const int PLAYER_STRUCT_SIZE = 0x2B0;

    // Offsets within player structure
    /// <summary>Offset: Character hash (4 bytes)</summary>
    public const int PLAYER_CHARA_HASH = 0x00;
    /// <summary>Offset: Name hash (4 bytes)</summary>
    public const int PLAYER_NAME_HASH = 0x04;
    /// <summary>Offset: Model ID (4 bytes)</summary>
    public const int PLAYER_MODEL_ID = 0x08;
    /// <summary>Offset: Player level (4 bytes)</summary>
    public const int PLAYER_LEVEL = 0x10;
    /// <summary>Offset: Experience points (4 bytes)</summary>
    public const int PLAYER_EXPERIENCE = 0x14;
    /// <summary>Offset: Position (4 bytes: 1=GK, 2=FW, 3=MF, 4=DF)</summary>
    public const int PLAYER_POSITION = 0x20;
    /// <summary>Offset: Element (4 bytes: 1=Wind, 2=Forest, 3=Fire, 4=Mountain)</summary>
    public const int PLAYER_ELEMENT = 0x24;
    /// <summary>Offset: Rarity (2 bytes: 0=N, 2=R, 5=UR, 6=LR, 7=Legend, 20=BASARA)</summary>
    public const int PLAYER_RARITY = 0x3C;
    
    /// <summary>Rarity value for Legend tier (0x07 = 7) - Can use passive skills</summary>
    public const int RARITY_LEGEND = 0x07;
    
    /// <summary>
    /// Rarity value for BASARA tier (0x14 = 20).
    /// Added in Galaxy&amp;LBX DLC (v1.5.0, 2025-12-21).
    /// BASARA players can switch between all 6 builds (basaraBuildType 0-5) and have moves reassigned.
    /// 52 characters eligible (basaraBuildInfo.json).
    /// </summary>
    public const int RARITY_BASARA = 0x14;

    // Stats offsets (each 4 bytes)
    /// <summary>Offset: Kick stat (シュート)</summary>
    public const int PLAYER_STAT_KICK = 0x40;
    /// <summary>Offset: Control stat (ドリブル)</summary>
    public const int PLAYER_STAT_CONTROL = 0x44;
    /// <summary>Offset: Technique stat (テクニック)</summary>
    public const int PLAYER_STAT_TECHNIQUE = 0x48;
    /// <summary>Offset: Physical stat (フィジカル)</summary>
    public const int PLAYER_STAT_PHYSICAL = 0x4C;
    /// <summary>Offset: Pressure stat (プレッシャー)</summary>
    public const int PLAYER_STAT_PRESSURE = 0x50;
    /// <summary>Offset: Speed stat (スピード)</summary>
    public const int PLAYER_STAT_SPEED = 0x54;
    /// <summary>Offset: Intelligence stat (インテリジェンス)</summary>
    public const int PLAYER_STAT_INTELLIGENCE = 0x58;
    /// <summary>Offset: Freedom stat</summary>
    public const int PLAYER_STAT_FREEDOM = 0x5C;

    // Spirit/Kenshin offsets
    /// <summary>Offset: Spirit hash (4 bytes)</summary>
    public const int PLAYER_SPIRIT_HASH = 0x100;
    /// <summary>Offset: Spirit level (4 bytes)</summary>
    public const int PLAYER_SPIRIT_LEVEL = 0x104;
    /// <summary>Offset: Spirit TP (4 bytes)</summary>
    public const int PLAYER_SPIRIT_TP = 0x108;

    // Skill/Passive slots
    /// <summary>Offset: Active skill slots (5 × 8 bytes)</summary>
    public const int PLAYER_SKILL_SLOTS = 0x180;
    /// <summary>Size of each skill slot entry</summary>
    public const int SKILL_SLOT_SIZE = 8;
    /// <summary>Offset: Passive skill slots (5 × 4 bytes)</summary>
    public const int PLAYER_PASSIVE_SLOTS = 0x1C0;
    /// <summary>Size of each passive slot entry</summary>
    public const int PASSIVE_SLOT_SIZE = 4;

    // === TEAM STRUCTURE ===
    /// <summary>
    /// RVA for game state pointer (team data base).
    /// </summary>
    public const long TEAM_BASE_RVA = 0x01FE7438;
    /// <summary>Offset from game state to team array</summary>
    public const int TEAM_DATA_OFFSET = 0x1000;
    /// <summary>Number of players per team (11 starters + 5 bench)</summary>
    public const int TEAM_SIZE = 16;

    // === MATCH STATE ===
    /// <summary>
    /// RVA for match state structure.
    /// </summary>
    public const long MATCH_STATE_RVA = 0x01FE7500;
    /// <summary>Offset: Home team score</summary>
    public const int MATCH_SCORE_HOME = 0x10;
    /// <summary>Offset: Away team score</summary>
    public const int MATCH_SCORE_AWAY = 0x14;
    /// <summary>Offset: Time remaining (float, seconds)</summary>
    public const int MATCH_TIME_REMAINING = 0x18;
    /// <summary>Offset: Current half (1=first, 2=second)</summary>
    public const int MATCH_HALF = 0x1C;

    // === NEW CHEAT ADDRESSES ===

    /// <summary>
    /// Infinite TP: Prevents TP decrease during techniques.
    /// Patch: NOP the SUB instruction.
    /// </summary>
    public const long TP_FREEZE_ADDRESS = 0xCE8A20;
    /// <summary>Original bytes for TP freeze</summary>
    public static readonly byte[] TP_FREEZE_ORIGINAL = [0x29, 0x4E, 0x10, 0x48];
    /// <summary>Patched bytes for TP freeze (NOP x4)</summary>
    public static readonly byte[] TP_FREEZE_PATCHED = [0x90, 0x90, 0x90, 0x90];

    /// <summary>
    /// Instant Skill Charge: Skills have no charge time.
    /// Patch: Set charge time to 0.
    /// </summary>
    public const long SKILL_CHARGE_ADDRESS = 0xCE8B40;
    /// <summary>Original bytes for skill charge</summary>
    public static readonly byte[] SKILL_CHARGE_ORIGINAL = [0xF3, 0x0F, 0x58, 0xC1];
    /// <summary>Patched bytes for instant charge (XORPS xmm0, xmm0)</summary>
    public static readonly byte[] SKILL_CHARGE_PATCHED = [0x0F, 0x57, 0xC0, 0x90];

    /// <summary>
    /// Max Stats: All player stats read as 99.
    /// Hook at player data fetch function.
    /// </summary>
    public const long STAT_MAX_ADDRESS = 0xD26FB0;

    /// <summary>
    /// Time Freeze: Match timer stops.
    /// Patch: NOP the timer decrement.
    /// </summary>
    public const long TIME_FREEZE_ADDRESS = 0x5C8A0;
    /// <summary>Original bytes for time freeze</summary>
    public static readonly byte[] TIME_FREEZE_ORIGINAL = [0xF3, 0x0F, 0x5C, 0x05];
    /// <summary>Patched bytes for time freeze (NOP x4)</summary>
    public static readonly byte[] TIME_FREEZE_PATCHED = [0x90, 0x90, 0x90, 0x90];

    /// <summary>
    /// Score Multiplier: Multiply goal score.
    /// Hook at score increment function.
    /// </summary>
    public const long SCORE_MULTIPLIER_ADDRESS = 0xD95200;
    /// <summary>Original bytes: ADD [rax], 1</summary>
    public static readonly byte[] SCORE_MULT_ORIGINAL = [0x83, 0x00, 0x01];
    /// <summary>Patched bytes: ADD [rax], 5</summary>
    public static readonly byte[] SCORE_MULT_X5 = [0x83, 0x00, 0x05];

    /// <summary>
    /// Stamina Freeze: Players don't get tired.
    /// Patch: NOP the stamina decrease.
    /// </summary>
    public const long STAMINA_FREEZE_ADDRESS = 0xCE8C10;
    /// <summary>Original bytes for stamina freeze</summary>
    public static readonly byte[] STAMINA_FREEZE_ORIGINAL = [0x29, 0x46, 0x54];
    /// <summary>Patched bytes for stamina freeze (NOP x3)</summary>
    public static readonly byte[] STAMINA_FREEZE_PATCHED = [0x90, 0x90, 0x90];

    /// <summary>
    /// Unlock All Characters: Bypass character unlock check.
    /// Patch: MOV AL, 1; RET
    /// </summary>
    public const long UNLOCK_CHARS_ADDRESS = 0xD26F00;
    /// <summary>Original bytes for unlock check</summary>
    public static readonly byte[] UNLOCK_CHARS_ORIGINAL = [0x48, 0x89, 0x5C, 0x24, 0x10];
    /// <summary>Patched bytes to always return true</summary>
    public static readonly byte[] UNLOCK_CHARS_PATCHED = [0xB0, 0x01, 0xC3, 0x90, 0x90];

    /// <summary>
    /// Unlock All Techniques: Bypass technique unlock check.
    /// </summary>
    public const long UNLOCK_TECHS_ADDRESS = 0xCC6A90;
    /// <summary>Original bytes for technique unlock check</summary>
    public static readonly byte[] UNLOCK_TECHS_ORIGINAL = [0x48, 0x89, 0x5C, 0x24, 0x08];
    /// <summary>Patched bytes to always return true</summary>
    public static readonly byte[] UNLOCK_TECHS_PATCHED = [0xB0, 0x01, 0xC3, 0x90, 0x90];

    /// <summary>
    /// Bytes originaux Bean increment: SUB
    /// </summary>
    public static readonly byte[] BEAN_ORIGINAL_BYTES = [0x2B, 0xCD];

    /// <summary>
    /// Bytes modifiés Bean increment: ADD
    /// </summary>
    public static readonly byte[] BEAN_INCREMENT_BYTES = [0x03, 0xCD];

    /// <summary>
    /// Opcode SUB (soustraction) - instruction originale
    /// </summary>
    public const byte OPCODE_SUB = 0x2B;

    /// <summary>
    /// Opcode ADD (addition) - pour auto-increment
    /// </summary>
    public const byte OPCODE_ADD = 0x03;

    #endregion

    #region Victory Items Offsets

    /// <summary>
    /// Offsets pour Victory Items (Tire, Barricade, Mannequin, Wheel)
    /// Base: COMMON_BASE_ADDRESS
    /// </summary>
    public static readonly int[][] VICTORY_ITEMS_OFFSETS =
    [
        [0xFE8, 0x20F0, 0x8, 0x10, 0xA0, 0x54], // Tire
        [0xFE8, 0x20F0, 0x8, 0x10, 0xA0, 0x58], // Barricade
        [0xFE8, 0x20F0, 0x8, 0x10, 0xA0, 0x5C], // Mannequin
        [0xFE8, 0x20F0, 0x8, 0x10, 0xA0, 0x60], // Wheel
    ];

    /// <summary>
    /// Noms des Victory Items
    /// </summary>
    public static readonly string[] VICTORY_ITEMS_NAMES =
    [
        "Tire",
        "Barricade", 
        "Mannequin",
        "Wheel"
    ];

    #endregion

    #region Spirits - Pointer Chains from IE:VR SE v1.1.0

    /// <summary>
    /// Offset de base pour les Spirits
    /// </summary>
    public static readonly int[] SPIRITS_BASE_OFFSETS = [0xFE8, 0x20F0, 0x8, 0x10, 0xA0, 0x64];

    /// <summary>
    /// Harper Evans Breach Spirit offsets
    /// Base: 0x0208DBE8
    /// </summary>
    public static readonly int[] HARPER_EVANS_BREACH_OFFSETS = [0x10, 0x10, 0x10, 0x10, 0x8, 0x1B0, 0xC];

    /// <summary>
    /// Hector Helio Justice Spirit offsets
    /// Base: 0x0208DBE8
    /// </summary>
    public static readonly int[] HECTOR_HELIO_JUSTICE_OFFSETS = [0x10, 0x10, 0x10, 0x10, 0x8, 0x50, 0xD4];

    /// <summary>
    /// Nombre total de Spirits
    /// </summary>
    public const int SPIRITS_COUNT = 24;

    /// <summary>
    /// Espacement entre chaque Spirit (en bytes)
    /// </summary>
    public const int SPIRIT_OFFSET_STEP = 0x4;

    /// <summary>
    /// Noms des 24 Spirits
    /// </summary>
    public static readonly string[] SPIRITS_NAMES =
    [
        "Harper Evans Breach",
        "Hector Helio Justice",
        "Instantaneous",
        "Spirit 4",
        "Spirit 5",
        "Spirit 6",
        "Spirit 7",
        "Spirit 8",
        "Spirit 9",
        "Spirit 10",
        "Spirit 11",
        "Spirit 12",
        "Spirit 13",
        "Spirit 14",
        "Spirit 15",
        "Spirit 16",
        "Spirit 17",
        "Spirit 18",
        "Spirit 19",
        "Spirit 20",
        "Spirit 21",
        "Spirit 22",
        "Spirit 23",
        "Spirit 24"
    ];

    #endregion

    #region Process Info

    /// <summary>
    /// Nom du processus du jeu (sans .exe)
    /// </summary>
    public const string PROCESS_NAME = "nie";

    /// <summary>
    /// Nom du dossier du jeu sur Steam
    /// </summary>
    public const string GAME_FOLDER_NAME = "INAZUMA ELEVEN Victory Road";

    /// <summary>
    /// Steam App ID
    /// </summary>
    public const string STEAM_APP_ID = "2799860";

    /// <summary>
    /// Nom du lanceur EAC
    /// </summary>
    public const string EAC_LAUNCHER_NAME = "EACLauncher.exe";

    /// <summary>
    /// Nom du fichier backup EAC
    /// </summary>
    public const string EAC_BACKUP_NAME = "EACLauncher.exe.backup";

    #endregion

    #region Game State Offsets (from nie.c reverse engineering)

    /// <summary>
    /// DAT_141fe1d60 - Main game state pointer (RVA)
    /// All resource data is accessed via: DAT_141fe1d60 + 0x6970 → resource offsets
    /// </summary>
    public const long GAME_STATE_POINTER_RVA = 0x1FE1D60;

    /// <summary>
    /// Offset from game state to resource data base pointer
    /// Usage: *(*DAT_141fe1d60 + 0x6970)
    /// </summary>
    public const int RESOURCE_BASE_OFFSET = 0x6970;

    // Resource bitmap offsets (from resource base)
    /// <summary>
    /// Offset: Inazuma Flowers bitmap (0x187b0)
    /// Each bit represents a flower slot state
    /// </summary>
    public const int FLOWERS_BITMAP_OFFSET = 0x187B0;

    /// <summary>
    /// Offset: God Hand Grass bitmap (0x185a8)
    /// Each bit represents a god hand grass slot state
    /// </summary>
    public const int GOD_HAND_BITMAP_OFFSET = 0x185A8;

    /// <summary>
    /// Offset: Resource Type 3 bitmap (0x18568)
    /// Each bit represents a resource3 slot state
    /// </summary>
    public const int RESOURCE3_BITMAP_OFFSET = 0x18568;

    /// <summary>
    /// Offset: Item flags bitmap (0x18a98)
    /// Each bit represents an item unlock state
    /// </summary>
    public const int ITEM_FLAGS_OFFSET = 0x18A98;

    /// <summary>
    /// Offset: Item states byte array (0x18ab8)
    /// Each byte represents item state for one item
    /// </summary>
    public const int ITEM_STATES_OFFSET = 0x18AB8;

    /// <summary>
    /// Offset: Item quantities int array (0x18b38)
    /// Each int (4 bytes) represents item quantity
    /// </summary>
    public const int ITEM_QUANTITIES_OFFSET = 0x18B38;

    /// <summary>
    /// Offset: Resource counts array - offline mode (0x15c68)
    /// ushort array for resource quantities
    /// </summary>
    public const int RESOURCE_COUNTS_OFFLINE_OFFSET = 0x15C68;

    /// <summary>
    /// Offset: Resource counts array - online mode (0x15e68)
    /// ushort array for resource quantities (when online check returns true)
    /// </summary>
    public const int RESOURCE_COUNTS_ONLINE_OFFSET = 0x15E68;

    /// <summary>
    /// Offset: Character/data unlock bitmap at 0x15398
    /// </summary>
    public const int CHARACTER_UNLOCK_OFFSET = 0x15398;

    /// <summary>
    /// Offset: Character flags at 0x15578
    /// </summary>
    public const int CHARACTER_FLAGS_OFFSET = 0x15578;

    /// <summary>
    /// Offset: Item data at 0x14df8
    /// </summary>
    public const int ITEM_DATA_OFFSET = 0x14DF8;

    #endregion

    #region AOB Patterns (pour scan dynamique)

    /// <summary>
    /// Pattern AOB pour Heroes Spirits injection.
    /// Pattern: 66 89 68 0C 48
    /// </summary>
    public static readonly byte[] AOB_HEROES_SPIRITS = [0x66, 0x89, 0x68, 0x0C, 0x48];

    /// <summary>
    /// Pattern AOB pour Elite Spirits injection.
    /// Pattern: 66 41 89 6C 78 10
    /// </summary>
    public static readonly byte[] AOB_ELITE_SPIRITS = [0x66, 0x41, 0x89, 0x6C, 0x78, 0x10];

    /// <summary>
    /// Pattern AOB pour Passive Value editing.
    /// Pattern: 48 8B 0F 0F 57 C9 F3 ?? ?? ?? E8 ?? ?? ?? ?? EB ?? 0F
    /// Mask: 1=match, 0=wildcard
    /// </summary>
    public static readonly byte[] AOB_PASSIVE_VALUE_PATTERN = 
        [0x48, 0x8B, 0x0F, 0x0F, 0x57, 0xC9, 0xF3, 0x00, 0x00, 0x00, 0xE8, 0x00, 0x00, 0x00, 0x00, 0xEB, 0x00, 0x0F];
    public static readonly byte[] AOB_PASSIVE_VALUE_MASK = 
        [1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1];

    /// <summary>
    /// Pattern AOB pour Spirit Card injection.
    /// Pattern: 49 8B 07 49 8B CF FF ?? ?? 45 ?? ?? 44
    /// </summary>
    public static readonly byte[] AOB_SPIRIT_CARD_PATTERN = 
        [0x49, 0x8B, 0x07, 0x49, 0x8B, 0xCF, 0xFF, 0x00, 0x00, 0x45, 0x00, 0x00, 0x44];
    public static readonly byte[] AOB_SPIRIT_CARD_MASK = 
        [1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1];

    /// <summary>
    /// Pattern AOB pour Store Item Multiplier.
    /// Pattern: 89 4E 10 8B C3 (mov [rsi+10],ecx; mov eax,ebx)
    /// </summary>
    public static readonly byte[] AOB_STORE_MULTIPLIER = [0x89, 0x4E, 0x10, 0x8B, 0xC3];

    /// <summary>
    /// Pattern AOB pour Team Dock Hero 1 (Unlimited Spirits).
    /// Pattern: 75 03 8B 58 10 49
    /// </summary>
    public static readonly byte[] AOB_TEAM_DOCK_HERO1 = [0x75, 0x03, 0x8B, 0x58, 0x10, 0x49];

    /// <summary>
    /// Pattern AOB pour Team Dock Hero 2 (Unlimited Spirits).
    /// Pattern: 75 ?? 8B 40 10 48
    /// </summary>
    public static readonly byte[] AOB_TEAM_DOCK_HERO2_PATTERN = [0x75, 0x00, 0x8B, 0x40, 0x10, 0x48];
    public static readonly byte[] AOB_TEAM_DOCK_HERO2_MASK = [1, 0, 1, 1, 1, 1];

    #endregion
}
