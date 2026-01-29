using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace IECODE.Core.Memory;

/// <summary>
/// Service pour éditer les techniques (Hissatsu) des joueurs dans nie.exe.
/// Utilise le MemoryEditorService pour les opérations de bas niveau.
/// </summary>
/// <remarks>
/// Structure mémoire d'un slot de technique (8 bytes):
/// - Bytes 0-3: Hash CRC32 de la technique (int32)
/// - Bytes 4-5: Niveau de la technique (ushort, 0-99)
/// - Bytes 6-7: Flags d'état (ushort)
/// </remarks>
public sealed partial class TechniqueEditorService : IDisposable
{
    #region Fields

    private readonly MemoryEditorService _memoryService;
    private readonly bool _ownsService;
    private bool _disposed;

    #endregion

    #region Properties

    /// <summary>
    /// Indique si le service est attaché au processus du jeu.
    /// </summary>
    public bool IsAttached => _memoryService.IsAttached;

    /// <summary>
    /// Adresse de base du module nie.exe.
    /// </summary>
    public IntPtr ModuleBase => _memoryService.ModuleBase;

    #endregion

    #region Constructors

    /// <summary>
    /// Crée une nouvelle instance de TechniqueEditorService.
    /// </summary>
    public TechniqueEditorService()
    {
        _memoryService = new MemoryEditorService();
        _ownsService = true;
    }

    /// <summary>
    /// Crée une nouvelle instance de TechniqueEditorService avec un service mémoire existant.
    /// </summary>
    public TechniqueEditorService(MemoryEditorService memoryService)
    {
        _memoryService = memoryService;
        _ownsService = false;
    }

    #endregion

    #region Process Attachment

    /// <summary>
    /// S'attache au processus nie.exe.
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si l'attachement échoue</param>
    /// <returns>True si l'attachement réussit</returns>
    public bool Attach(out string errorMessage)
    {
        return _memoryService.AttachToProcess(out errorMessage);
    }

    /// <summary>
    /// Détache du processus.
    /// </summary>
    public void Detach()
    {
        if (_ownsService)
        {
            _memoryService.DetachFromProcess();
        }
    }

    #endregion

    #region Memory Operations

    private long ReadInt64(IntPtr address)
    {
        long offset = address.ToInt64() - ModuleBase.ToInt64();
        Span<byte> data = stackalloc byte[8];
        if (_memoryService.ReadBytes(offset, data) == 8)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(data);
        }
        return 0;
    }

    private int ReadInt32(IntPtr address)
    {
        long offset = address.ToInt64() - ModuleBase.ToInt64();
        Span<byte> data = stackalloc byte[4];
        if (_memoryService.ReadBytes(offset, data) == 4)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(data);
        }
        return 0;
    }

    private bool WriteBytes(IntPtr address, ReadOnlySpan<byte> data)
    {
        long offset = address.ToInt64() - ModuleBase.ToInt64();
        return _memoryService.WriteBytes(offset, data);
    }

    #endregion

    #region Team & Player Address Resolution

    /// <summary>
    /// Obtient l'adresse de base de l'équipe en mémoire.
    /// </summary>
    /// <returns>Adresse de base ou 0 si non disponible</returns>
    public long GetTeamBaseAddress()
    {
        if (!IsAttached || ModuleBase == IntPtr.Zero)
            return 0;

        IntPtr gameStatePtr = new IntPtr(ModuleBase.ToInt64() + TechniqueConstants.RVA_GAME_STATE);
        long gameState = ReadInt64(gameStatePtr);

        if (gameState == 0)
            return 0;

        return gameState + TechniqueConstants.TEAM_DATA_OFFSET;
    }

    /// <summary>
    /// Obtient l'adresse mémoire d'un joueur.
    /// </summary>
    /// <param name="playerIndex">Index du joueur (0-15)</param>
    /// <returns>Adresse du joueur ou 0 si invalide</returns>
    public long GetPlayerAddress(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= TechniqueConstants.MAX_TEAM_SIZE)
            return 0;

        long teamBase = GetTeamBaseAddress();
        if (teamBase == 0)
            return 0;

        return teamBase + (playerIndex * TechniqueConstants.PLAYER_ENTRY_SIZE);
    }

    #endregion

    #region Technique Operations

    /// <summary>
    /// Structure représentant un slot de technique.
    /// </summary>
    public readonly record struct TechniqueSlot(
        int SlotIndex,
        long Address,
        int Hash,
        ushort Level,
        ushort Flags)
    {
        /// <summary>Hash formaté en hexadécimal.</summary>
        public string HashHex => $"0x{(uint)Hash:X8}";

        /// <summary>Nom de la technique.</summary>
        public string Name => TechniqueConstants.GetTechniqueName(Hash);

        /// <summary>Catégorie de la technique.</summary>
        public string Category => TechniqueConstants.GetTechniqueCategory(Hash);

        /// <summary>Indique si le slot est vide.</summary>
        public bool IsEmpty => Hash == 0;
    }

    /// <summary>
    /// Lit les techniques d'un joueur.
    /// </summary>
    /// <param name="playerIndex">Index du joueur (0-15)</param>
    /// <returns>Liste des slots de techniques</returns>
    public TechniqueSlot[] ReadPlayerTechniques(int playerIndex)
    {
        var result = new TechniqueSlot[TechniqueConstants.MAX_SKILL_SLOTS];
        long playerAddr = GetPlayerAddress(playerIndex);

        if (playerAddr == 0)
            return result;

        long skillBase = playerAddr + TechniqueConstants.OFFSET_SKILL_SLOTS;
        long moduleBase = ModuleBase.ToInt64();

        Span<byte> data = stackalloc byte[TechniqueConstants.SKILL_SLOT_SIZE];

        for (int i = 0; i < TechniqueConstants.MAX_SKILL_SLOTS; i++)
        {
            long slotAddr = skillBase + (i * TechniqueConstants.SKILL_SLOT_SIZE);
            long offset = slotAddr - moduleBase;

            if (_memoryService.ReadBytes(offset, data) == TechniqueConstants.SKILL_SLOT_SIZE)
            {
                int hash = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
                ushort level = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2));
                ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));

                result[i] = new TechniqueSlot(i, slotAddr, hash, level, flags);
            }
        }

        return result;
    }

    /// <summary>
    /// Écrit une technique dans un slot de joueur.
    /// </summary>
    /// <param name="playerIndex">Index du joueur (0-15)</param>
    /// <param name="slotIndex">Index du slot (0-4)</param>
    /// <param name="techniqueHash">Hash CRC32 de la technique</param>
    /// <param name="level">Niveau (1-99, défaut: 99)</param>
    /// <param name="flags">Flags d'état (défaut: 0x0001 = actif)</param>
    /// <returns>True si l'écriture réussit</returns>
    public bool WriteTechnique(int playerIndex, int slotIndex, int techniqueHash, ushort level = 99, ushort flags = 0x0001)
    {
        if (slotIndex < 0 || slotIndex >= TechniqueConstants.MAX_SKILL_SLOTS)
            return false;

        long playerAddr = GetPlayerAddress(playerIndex);
        if (playerAddr == 0)
            return false;

        long slotAddr = playerAddr + TechniqueConstants.OFFSET_SKILL_SLOTS + (slotIndex * TechniqueConstants.SKILL_SLOT_SIZE);

        // Structure: Hash (4) + Level (2) + Flags (2)
        Span<byte> data = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(data[..4], techniqueHash);
        BinaryPrimitives.WriteUInt16LittleEndian(data[4..6], level);
        BinaryPrimitives.WriteUInt16LittleEndian(data[6..8], flags);

        return WriteBytes(new IntPtr(slotAddr), data);
    }

    /// <summary>
    /// Efface une technique d'un slot (remet à zéro).
    /// </summary>
    public bool ClearTechnique(int playerIndex, int slotIndex)
    {
        return WriteTechnique(playerIndex, slotIndex, 0, 0, 0);
    }

    /// <summary>
    /// Assigne la technique Ouragan Cyclonique (arashi_tatsumaki) à un joueur.
    /// </summary>
    /// <param name="playerIndex">Index du joueur (0-15)</param>
    /// <param name="slotIndex">Index du slot (0-4, défaut: 0)</param>
    /// <returns>True si l'écriture réussit</returns>
    public bool AssignArashiTatsumaki(int playerIndex, int slotIndex = 0)
    {
        return WriteTechnique(playerIndex, slotIndex, TechniqueConstants.HiddenTechniques.ARASHI_TATSUMAKI);
    }

    /// <summary>
    /// Assigne une technique prédéfinie à un joueur par son nom.
    /// </summary>
    public bool AssignTechniqueByName(int playerIndex, int slotIndex, string techniqueName)
    {
        int? hash = techniqueName.ToLowerInvariant() switch
        {
            "arashi_tatsumaki" or "ouragan_cyclonique" => TechniqueConstants.HiddenTechniques.ARASHI_TATSUMAKI,
            "fire_tornado" or "tornade_de_feu" => TechniqueConstants.HiddenTechniques.FIRE_TORNADO,
            "eternal_blizzard" or "blizzard_eternel" => TechniqueConstants.HiddenTechniques.ETERNAL_BLIZZARD,
            "big_bang" => TechniqueConstants.HiddenTechniques.BIG_BANG,
            "super_nova" => TechniqueConstants.HiddenTechniques.SUPER_NOVA,
            "god_hand" => TechniqueConstants.HiddenTechniques.GOD_HAND,
            "majin_the_hand" => TechniqueConstants.HiddenTechniques.MAJIN_THE_HAND,
            "chaos_meteor" => TechniqueConstants.HiddenTechniques.CHAOS_METEOR,
            "death_drop" => TechniqueConstants.HiddenTechniques.DEATH_DROP,
            "white_hurricane" or "ouragan_blanc" => TechniqueConstants.HiddenTechniques.WHITE_HURRICANE,
            "galactica_fall" => TechniqueConstants.HiddenTechniques.GALACTICA_FALL,
            "the_earth_infinity" => TechniqueConstants.HiddenTechniques.THE_EARTH_INFINITY,
            "dark_matter" => TechniqueConstants.HiddenTechniques.DARK_MATTER,
            "illusion_ball" => TechniqueConstants.HiddenTechniques.ILLUSION_BALL,
            "sky_walk" => TechniqueConstants.HiddenTechniques.SKY_WALK,
            "heavens_time" => TechniqueConstants.HiddenTechniques.HEAVENS_TIME,
            "the_wall" => TechniqueConstants.HiddenTechniques.THE_WALL,
            "dimension_cut" => TechniqueConstants.HiddenTechniques.DIMENSION_CUT,
            _ => null
        };

        if (hash.HasValue)
        {
            return WriteTechnique(playerIndex, slotIndex, hash.Value);
        }

        return false;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        
        if (_ownsService)
        {
            _memoryService.Dispose();
        }
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~TechniqueEditorService()
    {
        Dispose();
    }

    #endregion
}
