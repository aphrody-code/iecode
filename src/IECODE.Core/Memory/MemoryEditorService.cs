using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using IECODE.Core.Memory.Backend;

namespace IECODE.Core.Memory;

/// <summary>
/// Service for process memory manipulation.
/// Provides safe, AOT-compatible memory reading/writing with proper resource management.
/// Supports both Windows (kernel32) and Linux (/proc/mem).
/// </summary>
public sealed partial class MemoryEditorService : IDisposable
{
    #region Fields

    private readonly IMemoryBackend _backend;
    private readonly string _processName;
    private bool _disposed;

    // Code caves pour les patches complexes (store item multiplier)
    private IntPtr _storeItemMultiplierCodeCave1 = IntPtr.Zero;
    private IntPtr _storeItemMultiplierCodeCave2 = IntPtr.Zero;
    private IntPtr _storeItemMultiplierCodeCave3 = IntPtr.Zero;

    // Spirit Increment Injection
    private IntPtr _heroSpiritIncrementCodeCave = IntPtr.Zero;
    private IntPtr _eliteSpiritIncrementCodeCave = IntPtr.Zero;

    // Passive Value Editing
    private IntPtr _passiveValueCodeCave = IntPtr.Zero;
    private IntPtr _passiveValAdrAddress = IntPtr.Zero;
    private IntPtr _passiveValTypeAddress = IntPtr.Zero;
    private IntPtr _passiveValueHookAddress = IntPtr.Zero;

    // Spirit Card Injection
    private IntPtr _spiritCardInjectionCodeCave = IntPtr.Zero;
    private IntPtr _spiritCardHookAddress = IntPtr.Zero;
    private IntPtr _cfHerospiritAddTypeAddress = IntPtr.Zero;
    private IntPtr _cfHerospiritIDAddress = IntPtr.Zero;
    private bool _isSpiritCardInjectionEnabled;

    // Unlimited Spirits
    private IntPtr _teamDockHero1CodeCave = IntPtr.Zero;
    private IntPtr _teamDockHero1HookAddress = IntPtr.Zero;
    private IntPtr _teamDockHero2HookAddress = IntPtr.Zero;
    private bool _isUnlimitedSpiritsEnabled;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new Memory Editor service instance.
    /// </summary>
    /// <param name="processName">Target process name (without .exe extension)</param>
    public MemoryEditorService(string processName = "nie")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        _processName = processName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _backend = new WindowsMemoryBackend();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _backend = new LinuxMemoryBackend();
        }
        else
        {
            throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Indique si le service est attaché au processus cible.
    /// </summary>
    public bool IsAttached => _backend.IsAttached;

    /// <summary>
    /// Adresse de base du module principal du processus.
    /// </summary>
    public IntPtr ModuleBase => _backend.ModuleBase;

    #endregion

    #region Process Management

    /// <summary>
    /// Attache le service au processus cible.
    /// </summary>
    /// <returns>True si l'attachement réussit</returns>
    public bool AttachToProcess()
    {
        return AttachToProcess(out _);
    }

    /// <summary>
    /// Attache le service au processus cible avec message d'erreur détaillé.
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si l'attachement échoue</param>
    /// <returns>True si l'attachement réussit</returns>
    public bool AttachToProcess(out string errorMessage)
    {
        return _backend.Attach(_processName, out errorMessage);
    }
    
    /// <summary>
    /// Détache le service du processus.
    /// IMPORTANT: Restaure tous les patches actifs avant de libérer les ressources
    /// pour éviter les crashes du jeu.
    /// </summary>
    public void DetachFromProcess()
    {
        // CRITICAL: Restore store item multiplier hooks BEFORE freeing code caves!
        if (_storeItemMultiplierCodeCave1 != IntPtr.Zero ||
            _storeItemMultiplierCodeCave2 != IntPtr.Zero ||
            _storeItemMultiplierCodeCave3 != IntPtr.Zero)
        {
            try
            {
                WriteBytesProtected(MemoryAddresses.STORE_MULTIPLIER_HOOK1, MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES);
                WriteBytesProtected(MemoryAddresses.STORE_MULTIPLIER_HOOK2, MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES);
                WriteBytesProtected(MemoryAddresses.STORE_MULTIPLIER_HOOK3, MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES);
            }
            catch { }
        }
        
        CleanupCodeCaves();
        _backend.Detach();
    }

    /// <summary>
    /// Vérifie si le processus cible est en cours d'exécution.
    /// </summary>
    public bool IsProcessRunning()
    {
        return Process.GetProcessesByName(_processName).Length > 0;
    }

    /// <summary>
    /// Obtient le statut du processus sous forme de chaîne.
    /// </summary>
    public string GetProcessStatus()
    {
        if (!IsProcessRunning())
        {
            return "Process not running";
        }

        if (IsAttached)
        {
            return "Attached";
        }

        return "Process running (not attached)";
    }

    #endregion

    #region Memory Reading

    /// <summary>
    /// Reads a sequence of bytes from process memory into a span.
    /// </summary>
    /// <param name="baseOffset">Offset from module base</param>
    /// <param name="buffer">Buffer to read into</param>
    /// <returns>Number of bytes read</returns>
    public int ReadBytes(long baseOffset, Span<byte> buffer)
    {
        if (!IsAttached || buffer.IsEmpty) return 0;
        IntPtr address = IntPtr.Add(ModuleBase, (int)baseOffset);
        return _backend.Read(address, buffer);
    }

    /// <summary>
    /// Reads a sequence of bytes from process memory.
    /// </summary>
    /// <param name="baseOffset">Offset from module base</param>
    /// <param name="length">Number of bytes to read</param>
    /// <returns>Byte array containing the read data, or empty array on failure</returns>
    public byte[] ReadBytes(long baseOffset, int length)
    {
        if (!IsAttached || length <= 0) return Array.Empty<byte>();
        
        byte[] buffer = new byte[length];
        IntPtr address = IntPtr.Add(ModuleBase, (int)baseOffset);
        
        int read = _backend.Read(address, buffer);
        return read > 0 ? buffer : Array.Empty<byte>();
    }

    /// <summary>
    /// Reads an Int32 value from process memory.
    /// </summary>
    /// <param name="baseOffset">Offset from module base</param>
    /// <param name="offsets">Pointer offset chain</param>
    /// <returns>Read value, or 0 if operation fails</returns>
    public int ReadValue(long baseOffset, ReadOnlySpan<int> offsets)
    {
        if (!IsAttached) return 0;

        IntPtr address = ResolvePointerChain(baseOffset, offsets);
        if (address == IntPtr.Zero) return 0;

        Span<byte> buffer = stackalloc byte[4];
        if (_backend.Read(address, buffer) != 4) return 0;

        return System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    /// <summary>
    /// Reads a Float32 value from process memory.
    /// </summary>
    /// <param name="baseOffset">Offset from module base</param>
    /// <param name="offsets">Pointer offset chain</param>
    /// <returns>Read value, or 0.0f if operation fails</returns>
    public float ReadFloatValue(long baseOffset, ReadOnlySpan<int> offsets)
    {
        if (!IsAttached) return 0f;

        IntPtr address = ResolvePointerChain(baseOffset, offsets);
        if (address == IntPtr.Zero) return 0f;

        Span<byte> buffer = stackalloc byte[4];
        if (_backend.Read(address, buffer) != 4) return 0f;

        return System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    #endregion

    #region Memory Writing

    /// <summary>
    /// Writes an Int32 value to process memory.
    /// </summary>
    /// <param name="baseOffset">Offset from module base</param>
    /// <param name="offsets">Pointer offset chain</param>
    /// <param name="value">Value to write</param>
    /// <returns>True if write succeeds</returns>
    public bool WriteValue(long baseOffset, ReadOnlySpan<int> offsets, int value)
    {
        if (!IsAttached) return false;

        IntPtr address = ResolvePointerChain(baseOffset, offsets);
        if (address == IntPtr.Zero) return false;

        Span<byte> buffer = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        
        return _backend.Write(address, buffer);
    }

    /// <summary>
    /// Writes a Float32 value to process memory.
    /// </summary>
    /// <param name="baseOffset">Offset from module base</param>
    /// <param name="offsets">Pointer offset chain</param>
    /// <param name="value">Value to write</param>
    /// <returns>True if write succeeds</returns>
    public bool WriteFloatValue(long baseOffset, ReadOnlySpan<int> offsets, float value)
    {
        if (!IsAttached) return false;

        IntPtr address = ResolvePointerChain(baseOffset, offsets);
        if (address == IntPtr.Zero) return false;

        Span<byte> buffer = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        
        return _backend.Write(address, buffer);
    }

    /// <summary>
    /// Écrit des bytes bruts à une adresse relative au module.
    /// Utilisé pour les patches (NOP, instruction changes).
    /// </summary>
    /// <param name="address">Adresse relative au module</param>
    /// <param name="bytes">Bytes à écrire</param>
    /// <returns>True si l'écriture réussit</returns>
    public bool WriteBytes(long address, ReadOnlySpan<byte> bytes)
    {
        if (!IsAttached) return false;
        IntPtr targetAddress = new IntPtr(ModuleBase.ToInt64() + address);
        return _backend.Write(targetAddress, bytes);
    }

    /// <summary>
    /// Écrit des bytes bruts à une adresse relative au module.
    /// </summary>
    public bool WriteBytes(long address, byte[] bytes) => WriteBytes(address, (ReadOnlySpan<byte>)bytes);

    #endregion

    #region Pointer Resolution

    /// <summary>
    /// Resolves a pointer chain to obtain the final address.
    /// </summary>
    /// <param name="baseOffset">Initial offset from module</param>
    /// <param name="offsets">Chain of offsets to follow</param>
    /// <returns>Final address, or IntPtr.Zero on failure</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IntPtr ResolvePointerChain(long baseOffset, ReadOnlySpan<int> offsets)
    {
        if (offsets.IsEmpty)
            return IntPtr.Add(ModuleBase, (int)baseOffset);

        IntPtr address = IntPtr.Add(ModuleBase, (int)baseOffset);
        
        Span<byte> buffer = stackalloc byte[IntPtr.Size];
        if (_backend.Read(address, buffer) != IntPtr.Size) return IntPtr.Zero;

        address = IntPtr.Size == 8
            ? new IntPtr(System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(buffer))
            : new IntPtr(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(buffer));

        for (int i = 0; i < offsets.Length - 1; i++)
        {
            address = IntPtr.Add(address, offsets[i]);

            if (_backend.Read(address, buffer) != IntPtr.Size) return IntPtr.Zero;

            address = IntPtr.Size == 8
                ? new IntPtr(System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(buffer))
                : new IntPtr(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(buffer));
        }

        return IntPtr.Add(address, offsets[^1]);
    }

    #endregion

    #region Patch Helpers

    /// <summary>
    /// Applies a memory patch with validation.
    /// Checks if the patch is already applied or if the original bytes match before writing.
    /// </summary>
    private bool ApplyPatch(long address, byte[] original, byte[] patched, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!IsAttached)
        {
            errorMessage = "Not attached to process";
            return false;
        }

        int length = original.Length;
        if (patched.Length != length)
        {
            errorMessage = "Patch size mismatch";
            return false;
        }

        Span<byte> actual = stackalloc byte[length];
        if (ReadBytes(address, actual) != length)
        {
            errorMessage = $"Failed to read memory at 0x{address:X}";
            return false;
        }

        // Check if already patched
        if (actual.SequenceEqual(patched))
        {
            return true;
        }

        // Check if original bytes match
        if (actual.SequenceEqual(original))
        {
            return WriteBytesProtected(address, patched);
        }

        errorMessage = $"Adresse 0x{address:X}: bytes invalides. " +
                      $"Attendu: {BitConverter.ToString(original)}, " +
                      $"Trouvé: {BitConverter.ToString(actual.ToArray())}";
        return false;
    }

    #endregion

    #region Advanced Patches - Stars Freeze

    /// <summary>
    /// Active le freeze des Stars (les Stars ne diminuent plus).
    /// nie.exe+D95F1D: mov [rax+10],edx → NOP NOP NOP
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si le patch échoue</param>
    public bool FreezeStars(out string errorMessage)
    {
        return ApplyPatch(MemoryAddresses.FREEZE_STARS_ADDRESS, 
                         MemoryAddresses.ORIGINAL_FREEZE_STARS_BYTES, 
                         MemoryAddresses.NOP_BYTES, 
                         out errorMessage);
    }

    /// <summary>
    /// Active le freeze des Stars (les Stars ne diminuent plus).
    /// nie.exe+D95F1D: mov [rax+10],edx → NOP NOP NOP
    /// </summary>
    public bool FreezeStars()
    {
        return FreezeStars(out _);
    }

    /// <summary>
    /// Désactive le freeze des Stars.
    /// </summary>
    public bool UnfreezeStars()
    {
        return WriteBytesProtected(MemoryAddresses.FREEZE_STARS_ADDRESS, MemoryAddresses.ORIGINAL_FREEZE_STARS_BYTES);
    }

    #endregion

    #region Advanced Patches - Flowers Increment

    /// <summary>
    /// Active l'auto-increment des Flowers (au lieu de diminuer, elles augmentent).
    /// nie.exe+D95F15: sub ecx,ebp → add ecx,ebp
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si le patch échoue</param>
    public bool EnableFlowersIncrement(out string errorMessage)
    {
        return ApplyPatch(MemoryAddresses.FLOWER_INCREMENT_ADDRESS,
                         MemoryAddresses.FLOWER_ORIGINAL_BYTES,
                         MemoryAddresses.FLOWER_INCREMENT_BYTES,
                         out errorMessage);
    }

    /// <summary>
    /// Active l'auto-increment des Flowers.
    /// </summary>
    public bool EnableFlowersIncrement()
    {
        return EnableFlowersIncrement(out _);
    }

    /// <summary>
    /// Désactive l'auto-increment des Flowers.
    /// </summary>
    public bool DisableFlowersIncrement()
    {
        return WriteBytesProtected(MemoryAddresses.FLOWER_INCREMENT_ADDRESS, MemoryAddresses.FLOWER_ORIGINAL_BYTES);
    }

    #endregion

    #region Advanced Patches - Spirits Freeze

    /// <summary>
    /// Active le freeze des Spirits (les Spirits ne diminuent plus).
    /// </summary>
    public bool FreezeSpirits()
    {
        // nie.exe+CE9A46: mov [rax+0C],bp → NOP NOP NOP NOP
        bool success1 = WriteBytesProtected(MemoryAddresses.SPIRIT_FREEZE_ADDRESS, MemoryAddresses.SPIRIT_FREEZE_BYTES);
        
        // nie.exe+CE9A15 (Elite spirits): mov [r8+rdi+10],bp → NOP x6
        bool success2 = WriteBytesProtected(MemoryAddresses.ELITE_SPIRIT_FREEZE_ADDRESS, MemoryAddresses.ELITE_SPIRIT_FREEZE_BYTES);

        return success1 && success2;
    }

    /// <summary>
    /// Désactive le freeze des Spirits.
    /// </summary>
    public bool UnfreezeSpirits()
    {
        bool success1 = WriteBytesProtected(MemoryAddresses.SPIRIT_FREEZE_ADDRESS, MemoryAddresses.SPIRIT_ORIGINAL_BYTES);
        bool success2 = WriteBytesProtected(MemoryAddresses.ELITE_SPIRIT_FREEZE_ADDRESS, MemoryAddresses.ELITE_SPIRIT_ORIGINAL_BYTES);
        return success1 && success2;
    }

    #endregion

    #region Advanced Patches - Cooldown Freeze

    /// <summary>
    /// Active le freeze des cooldowns (les compétences n'ont plus de temps de recharge).
    /// nie.exe+5C874: SUBSS xmm0, xmm1 → NOP x4
    /// </summary>
    public bool FreezeCooldown()
    {
        return WriteBytesProtected(MemoryAddresses.COOLDOWN_FREEZE_ADDRESS, MemoryAddresses.COOLDOWN_FREEZE_BYTES);
    }

    /// <summary>
    /// Désactive le freeze des cooldowns.
    /// </summary>
    public bool UnfreezeCooldown()
    {
        return WriteBytesProtected(MemoryAddresses.COOLDOWN_FREEZE_ADDRESS, MemoryAddresses.COOLDOWN_ORIGINAL_BYTES);
    }

    #endregion

    #region Advanced Patches - Item Limit Bypass

    /// <summary>
    /// Active le bypass de limite d'items (999 → 65535).
    /// nie.exe+D770A8: CMP EAX, 0x3E7 → CMP EAX, 0xFFFF
    /// </summary>
    public bool EnableItemLimitBypass()
    {
        return WriteBytesProtected(MemoryAddresses.ITEM_LIMIT_ADDRESS, MemoryAddresses.ITEM_LIMIT_BYPASSED_BYTES);
    }

    /// <summary>
    /// Désactive le bypass de limite d'items.
    /// </summary>
    public bool DisableItemLimitBypass()
    {
        return WriteBytesProtected(MemoryAddresses.ITEM_LIMIT_ADDRESS, MemoryAddresses.ITEM_LIMIT_ORIGINAL_BYTES);
    }

    #endregion

    #region Advanced Patches - Universal Item Check

    /// <summary>
    /// Active le bypass universel de vérification d'items (toutes les vérifications passent).
    /// nie.exe+D95170: function prologue → MOV AL, 1; RET
    /// </summary>
    public bool EnableUniversalItemCheck()
    {
        return WriteBytesProtected(MemoryAddresses.UNIVERSAL_ITEM_CHECK_ADDRESS, MemoryAddresses.ITEM_CHECK_BYPASS_BYTES);
    }

    /// <summary>
    /// Désactive le bypass universel de vérification d'items.
    /// </summary>
    public bool DisableUniversalItemCheck()
    {
        return WriteBytesProtected(MemoryAddresses.UNIVERSAL_ITEM_CHECK_ADDRESS, MemoryAddresses.ITEM_CHECK_ORIGINAL_BYTES);
    }

    #endregion

    #region Advanced Patches - Beans Increment

    /// <summary>
    /// Active l'auto-increment des Beans (au lieu de diminuer, elles augmentent).
    /// nie.exe+D94775: SUB → ADD
    /// </summary>
    public bool EnableBeansIncrement()
    {
        return WriteBytesProtected(MemoryAddresses.BEAN_INCREMENT_ADDRESS, MemoryAddresses.BEAN_INCREMENT_BYTES);
    }

    /// <summary>
    /// Désactive l'auto-increment des Beans.
    /// </summary>
    public bool DisableBeansIncrement()
    {
        return WriteBytesProtected(MemoryAddresses.BEAN_INCREMENT_ADDRESS, MemoryAddresses.BEAN_ORIGINAL_BYTES);
    }

    #endregion

    #region Advanced Patches - Infinite TP (v2.0)

    /// <summary>
    /// Active l'Infinite TP (les points TP ne diminuent plus lors de l'utilisation des techniques).
    /// nie.exe+CE8A20: sub [rsi+10], ecx → NOP x4
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si le patch échoue</param>
    public bool FreezeTP(out string errorMessage)
    {
        return ApplyPatch(MemoryAddresses.TP_FREEZE_ADDRESS,
                         MemoryAddresses.TP_FREEZE_ORIGINAL,
                         MemoryAddresses.TP_FREEZE_PATCHED,
                         out errorMessage);
    }

    /// <summary>
    /// Active l'Infinite TP.
    /// </summary>
    public bool FreezeTP()
    {
        return FreezeTP(out _);
    }

    /// <summary>
    /// Désactive l'Infinite TP.
    /// </summary>
    public bool UnfreezeTP()
    {
        return WriteBytesProtected(MemoryAddresses.TP_FREEZE_ADDRESS, MemoryAddresses.TP_FREEZE_ORIGINAL);
    }

    #endregion

    #region Advanced Patches - Instant Skill Charge (v2.0)

    /// <summary>
    /// Active le chargement instantané des techniques (pas de temps de charge).
    /// nie.exe+CE8B40: addss xmm0, xmm1 → xorps xmm0, xmm0; nop
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si le patch échoue</param>
    public bool EnableInstantSkillCharge(out string errorMessage)
    {
        return ApplyPatch(MemoryAddresses.SKILL_CHARGE_ADDRESS,
                         MemoryAddresses.SKILL_CHARGE_ORIGINAL,
                         MemoryAddresses.SKILL_CHARGE_PATCHED,
                         out errorMessage);
    }

    /// <summary>
    /// Active le chargement instantané des techniques.
    /// </summary>
    public bool EnableInstantSkillCharge()
    {
        return EnableInstantSkillCharge(out _);
    }

    /// <summary>
    /// Désactive le chargement instantané des techniques.
    /// </summary>
    public bool DisableInstantSkillCharge()
    {
        return WriteBytesProtected(MemoryAddresses.SKILL_CHARGE_ADDRESS, MemoryAddresses.SKILL_CHARGE_ORIGINAL);
    }

    #endregion

    #region Advanced Patches - Stamina Freeze (v2.0)

    /// <summary>
    /// Active le Stamina Freeze (les joueurs ne se fatiguent plus).
    /// nie.exe+CE8C10: sub [rsi+54], eax → NOP x3
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si le patch échoue</param>
    public bool FreezeStamina(out string errorMessage)
    {
        return ApplyPatch(MemoryAddresses.STAMINA_FREEZE_ADDRESS,
                         MemoryAddresses.STAMINA_FREEZE_ORIGINAL,
                         MemoryAddresses.STAMINA_FREEZE_PATCHED,
                         out errorMessage);
    }

    /// <summary>
    /// Active le Stamina Freeze.
    /// </summary>
    public bool FreezeStamina()
    {
        return FreezeStamina(out _);
    }

    /// <summary>
    /// Désactive le Stamina Freeze.
    /// </summary>
    public bool UnfreezeStamina()
    {
        return WriteBytesProtected(MemoryAddresses.STAMINA_FREEZE_ADDRESS, MemoryAddresses.STAMINA_FREEZE_ORIGINAL);
    }

    #endregion

    #region Advanced Patches - Time Freeze (v2.0)

    /// <summary>
    /// Active le Time Freeze (le temps de match ne s'écoule plus).
    /// nie.exe+5C8A0: subss xmm0, [rip+xx] → NOP x4
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si le patch échoue</param>
    public bool FreezeMatchTime(out string errorMessage)
    {
        return ApplyPatch(MemoryAddresses.TIME_FREEZE_ADDRESS,
                         MemoryAddresses.TIME_FREEZE_ORIGINAL,
                         MemoryAddresses.TIME_FREEZE_PATCHED,
                         out errorMessage);
    }

    /// <summary>
    /// Active le Time Freeze.
    /// </summary>
    public bool FreezeMatchTime()
    {
        return FreezeMatchTime(out _);
    }

    /// <summary>
    /// Désactive le Time Freeze.
    /// </summary>
    public bool UnfreezeMatchTime()
    {
        return WriteBytesProtected(MemoryAddresses.TIME_FREEZE_ADDRESS, MemoryAddresses.TIME_FREEZE_ORIGINAL);
    }

    #endregion

    #region Advanced Patches - Unlock All Characters (v2.0)

    /// <summary>
    /// Débloque tous les personnages en bypassant la vérification de déverrouillage.
    /// nie.exe+D26F00: prologue → MOV AL, 1; RET
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si le patch échoue</param>
    public bool UnlockAllCharacters(out string errorMessage)
    {
        return ApplyPatch(MemoryAddresses.UNLOCK_CHARS_ADDRESS,
                         MemoryAddresses.UNLOCK_CHARS_ORIGINAL,
                         MemoryAddresses.UNLOCK_CHARS_PATCHED,
                         out errorMessage);
    }

    /// <summary>
    /// Débloque tous les personnages.
    /// </summary>
    public bool UnlockAllCharacters()
    {
        return UnlockAllCharacters(out _);
    }

    /// <summary>
    /// Restaure le verrouillage des personnages.
    /// </summary>
    public bool RestoreCharacterLocks()
    {
        return WriteBytesProtected(MemoryAddresses.UNLOCK_CHARS_ADDRESS, MemoryAddresses.UNLOCK_CHARS_ORIGINAL);
    }

    #endregion

    #region Advanced Patches - Unlock All Techniques (v2.0)

    /// <summary>
    /// Débloque toutes les techniques en bypassant la vérification de déverrouillage.
    /// nie.exe+CC6A90: prologue → MOV AL, 1; RET
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si le patch échoue</param>
    public bool UnlockAllTechniques(out string errorMessage)
    {
        return ApplyPatch(MemoryAddresses.UNLOCK_TECHS_ADDRESS,
                         MemoryAddresses.UNLOCK_TECHS_ORIGINAL,
                         MemoryAddresses.UNLOCK_TECHS_PATCHED,
                         out errorMessage);
    }

    /// <summary>
    /// Débloque toutes les techniques.
    /// </summary>
    public bool UnlockAllTechniques()
    {
        return UnlockAllTechniques(out _);
    }

    /// <summary>
    /// Restaure le verrouillage des techniques.
    /// </summary>
    public bool RestoreTechniqueLocks()
    {
        return WriteBytesProtected(MemoryAddresses.UNLOCK_TECHS_ADDRESS, MemoryAddresses.UNLOCK_TECHS_ORIGINAL);
    }

    #endregion

    #region Advanced Patches - Score Multiplier (v2.0)

    /// <summary>
    /// Active le multiplicateur de score (chaque but compte pour 5).
    /// nie.exe+D95200: add [rax], 1 → add [rax], 5
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si le patch échoue</param>
    public bool EnableScoreMultiplier(out string errorMessage)
    {
        return ApplyPatch(MemoryAddresses.SCORE_MULTIPLIER_ADDRESS,
                         MemoryAddresses.SCORE_MULT_ORIGINAL,
                         MemoryAddresses.SCORE_MULT_X5,
                         out errorMessage);
    }

    /// <summary>
    /// Active le multiplicateur de score.
    /// </summary>
    public bool EnableScoreMultiplier()
    {
        return EnableScoreMultiplier(out _);
    }

    /// <summary>
    /// Désactive le multiplicateur de score.
    /// </summary>
    public bool DisableScoreMultiplier()
    {
        return WriteBytesProtected(MemoryAddresses.SCORE_MULTIPLIER_ADDRESS, MemoryAddresses.SCORE_MULT_ORIGINAL);
    }

    #endregion

    #region Advanced Patches - Item Multiplier

    /// <summary>
    /// Valide qu'une adresse mémoire est dans une plage raisonnable pour éviter les crashs.
    /// </summary>
    private static bool IsValidUserModeAddress(long address)
    {
        // Adresses user-mode Windows x64: 0x10000 à 0x7FFFFFFFFFFF
        // Évite les adresses nulles, kernel-mode ou invalides
        const long MIN_USER_ADDRESS = 0x10000;
        const long MAX_USER_ADDRESS = 0x7FFFFFFFFFFF;
        return address >= MIN_USER_ADDRESS && address <= MAX_USER_ADDRESS;
    }

    /// <summary>
    /// Multiplie tous les items de l'inventaire par un facteur donné.
    /// Lit les quantités à 0x18b38 et les multiplie.
    /// </summary>
    /// <param name="multiplier">Facteur de multiplication (2-100)</param>
    /// <param name="maxItemSlots">Nombre de slots d'items à traiter (défaut: 256)</param>
    /// <param name="maxQuantity">Quantité maximale par item (défaut: 999)</param>
    /// <returns>Nombre d'items modifiés, ou -1 en cas d'erreur</returns>
    public int MultiplyAllItems(int multiplier = 2, int maxItemSlots = 256, int maxQuantity = 999)
    {
        if (!IsAttached || _backend == null) return -1;
        if (multiplier < 1 || multiplier > 100) return -1;
        if (maxItemSlots < 1 || maxItemSlots > 1024) return -1; // Limite raisonnable

        try
        {
            // Lire le pointeur de base: DAT_141fe1d60 + 0x6970
            Span<byte> ptrBuffer = stackalloc byte[8];
            IntPtr gameStatePtr = IntPtr.Add(ModuleBase, (int)MemoryAddresses.GAME_STATE_POINTER_RVA);
            
            if (_backend.Read(gameStatePtr, ptrBuffer) != 8) return -1;
            long gameState = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(ptrBuffer);
            if (!IsValidUserModeAddress(gameState)) return -1;

            // Lire le pointeur de ressources: gameState + 0x6970
            IntPtr resourcePtrAddr = new IntPtr(gameState + MemoryAddresses.RESOURCE_BASE_OFFSET);
            if (_backend.Read(resourcePtrAddr, ptrBuffer) != 8) return -1;
            long resourceBase = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(ptrBuffer);
            if (!IsValidUserModeAddress(resourceBase)) return -1;

            // Adresse des quantités d'items: resourceBase + 0x18b38
            long itemQuantitiesAddr = resourceBase + MemoryAddresses.ITEM_QUANTITIES_OFFSET;
            if (!IsValidUserModeAddress(itemQuantitiesAddr)) return -1;

            int modifiedCount = 0;
            Span<byte> quantityBuffer = stackalloc byte[4];

            for (int i = 0; i < maxItemSlots; i++)
            {
                long slotAddrValue = itemQuantitiesAddr + (i * 4);
                if (!IsValidUserModeAddress(slotAddrValue)) continue;
                IntPtr slotAddr = new IntPtr(slotAddrValue);
                
                if (_backend.Read(slotAddr, quantityBuffer) != 4) continue;
                
                int quantity = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(quantityBuffer);
                
                // Ignorer les slots vides ou avec quantité 0
                if (quantity <= 0) continue;
                
                // Calculer la nouvelle quantité
                int newQuantity = quantity * multiplier;
                if (newQuantity > maxQuantity) newQuantity = maxQuantity;
                if (newQuantity < 0) newQuantity = maxQuantity; // Overflow protection
                
                // Écrire la nouvelle quantité
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(quantityBuffer, newQuantity);
                if (_backend.Write(slotAddr, quantityBuffer))
                {
                    modifiedCount++;
                }
            }

            return modifiedCount;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Met tous les items de l'inventaire à une quantité maximale.
    /// </summary>
    /// <param name="maxQuantity">Quantité à définir (défaut: 999)</param>
    /// <param name="maxItemSlots">Nombre de slots d'items à traiter (défaut: 256)</param>
    /// <returns>Nombre d'items modifiés, ou -1 en cas d'erreur</returns>
    public int MaxAllItems(int maxQuantity = 999, int maxItemSlots = 256)
    {
        if (!IsAttached || _backend == null) return -1;
        if (maxQuantity < 1 || maxQuantity > 99999) return -1; // Limite raisonnable
        if (maxItemSlots < 1 || maxItemSlots > 1024) return -1;

        try
        {
            // Lire le pointeur de base
            Span<byte> ptrBuffer = stackalloc byte[8];
            IntPtr gameStatePtr = IntPtr.Add(ModuleBase, (int)MemoryAddresses.GAME_STATE_POINTER_RVA);
            
            if (_backend.Read(gameStatePtr, ptrBuffer) != 8) return -1;
            long gameState = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(ptrBuffer);
            if (!IsValidUserModeAddress(gameState)) return -1;

            // Lire le pointeur de ressources
            IntPtr resourcePtrAddr = new IntPtr(gameState + MemoryAddresses.RESOURCE_BASE_OFFSET);
            if (_backend.Read(resourcePtrAddr, ptrBuffer) != 8) return -1;
            long resourceBase = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(ptrBuffer);
            if (!IsValidUserModeAddress(resourceBase)) return -1;

            // Adresse des quantités d'items
            long itemQuantitiesAddr = resourceBase + MemoryAddresses.ITEM_QUANTITIES_OFFSET;
            if (!IsValidUserModeAddress(itemQuantitiesAddr)) return -1;

            int modifiedCount = 0;
            Span<byte> quantityBuffer = stackalloc byte[4];
            Span<byte> checkBuffer = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(quantityBuffer, maxQuantity);

            for (int i = 0; i < maxItemSlots; i++)
            {
                long slotAddrValue = itemQuantitiesAddr + (i * 4);
                if (!IsValidUserModeAddress(slotAddrValue)) continue;
                IntPtr slotAddr = new IntPtr(slotAddrValue);
                
                // Lire d'abord pour vérifier si le slot est utilisé
                if (_backend.Read(slotAddr, checkBuffer) != 4) continue;
                int currentQty = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(checkBuffer);
                
                // Ne modifier que les slots avec des items
                if (currentQty <= 0) continue;
                
                if (_backend.Write(slotAddr, quantityBuffer))
                {
                    modifiedCount++;
                }
            }

            return modifiedCount;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Scans memory for a pattern of bytes (AOB scan).
    /// </summary>
    /// <param name="pattern">The pattern to search for.</param>
    /// <param name="mask">Optional mask (0 = wildcard, 1 = match). If null, exact match.</param>
    /// <returns>Address of the first match, or IntPtr.Zero if not found.</returns>
    private IntPtr AOBScan(byte[] pattern, byte[]? mask = null)
    {
        if (!IsAttached || _backend == null)
            return IntPtr.Zero;

        try
        {
            // Scanning the entire module memory
            // We'll read in chunks to avoid massive allocations
            // Typically .text and .data sections are relevant.
            // For simplicity and matching the external tool, we scan the whole module.
            // Ideally we should use VirtualQueryEx but IMemoryBackend doesn't expose it yet.
            // We'll assume scanning from ModuleBase for a reasonable size (e.g. 128MB or until failure)
            // or just use a fixed large size if we can't determine module size easily via backend.
            // WindowsMemoryBackend uses EnumProcessModulesEx to get ModuleBase, but doesn't expose size.
            
            // NOTE: The external tool gets ModuleMemorySize from Process.MainModule.
            // We can do the same if we have the process instance, but Backend handles it.
            // Let's rely on reading until failure or a fixed limit for now, or just try to get process info.
            
            using var process = Process.GetProcessesByName(_processName).FirstOrDefault();
            if (process?.MainModule == null) return IntPtr.Zero;
            
            long moduleSize = process.MainModule.ModuleMemorySize;
            long startAddress = ModuleBase.ToInt64();
            
            int chunkSize = 4 * 1024 * 1024; // 4MB chunks
            byte[] buffer = new byte[chunkSize];
            
            // Create default mask if null
            if (mask == null)
            {
                mask = new byte[pattern.Length];
                for (int i = 0; i < mask.Length; i++) mask[i] = 1;
            }

            for (long offset = 0; offset < moduleSize; offset += chunkSize - pattern.Length)
            {
                IntPtr currentAddress = new IntPtr(startAddress + offset);
                int bytesToRead = (int)Math.Min(chunkSize, moduleSize - offset);
                
                // Read directly from backend
                // Note: buffer might not be fully filled if we hit unreadable memory
                // We'll resize the span to bytesToRead
                Span<byte> chunk = buffer.AsSpan(0, bytesToRead);
                int bytesRead = _backend.Read(currentAddress, chunk);
                
                if (bytesRead > 0)
                {
                    for (int i = 0; i < bytesRead - pattern.Length; i++)
                    {
                        bool found = true;
                        for (int j = 0; j < pattern.Length; j++)
                        {
                            if (mask[j] == 1 && buffer[i + j] != pattern[j])
                            {
                                found = false;
                                break;
                            }
                        }

                        if (found)
                        {
                            return new IntPtr(currentAddress.ToInt64() + i);
                        }
                    }
                }
            }

            return IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    #endregion

    #region Advanced Patches - Store Item Multiplier

    /// <summary>
    /// Vérifie si les bytes à une adresse correspondent aux bytes attendus.
    /// </summary>
    /// <param name="offset">Offset depuis la base du module</param>
    /// <param name="expectedBytes">Bytes attendus</param>
    /// <returns>True si les bytes correspondent</returns>
    public bool ValidateBytes(long offset, ReadOnlySpan<byte> expectedBytes)
    {
        if (!IsAttached)
            return false;

        Span<byte> actualBytes = stackalloc byte[expectedBytes.Length];
        if (ReadBytes(offset, actualBytes) != expectedBytes.Length)
            return false;

        return actualBytes.SequenceEqual(expectedBytes);
    }

    /// <summary>
    /// Active le multiplicateur d'items en magasin (x2457).
    /// Injecte du code cave pour multiplier la quantité lors de l'achat.
    /// ATTENTION: Cette fonctionnalité est risquée et peut crasher le jeu si les adresses sont obsolètes.
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si l'injection échoue</param>
    /// <returns>True si l'injection réussit</returns>
    public bool InjectStoreItemMultiplier(out string errorMessage)
    {
        errorMessage = string.Empty;
        
        if (!IsAttached)
        {
            errorMessage = "Non attaché au processus";
            return false;
        }

        try
        {
            // Validate original bytes BEFORE patching to avoid crashes
            if (!ValidateBytes(MemoryAddresses.STORE_MULTIPLIER_HOOK1, MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES))
            {
                byte[] actual = ReadBytes(MemoryAddresses.STORE_MULTIPLIER_HOOK1, 5);
                errorMessage = $"Hook1 (0x{MemoryAddresses.STORE_MULTIPLIER_HOOK1:X}): bytes invalides. " +
                              $"Attendu: {BitConverter.ToString(MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES)}, " +
                              $"Trouvé: {BitConverter.ToString(actual)}. Version du jeu incompatible.";
                return false;
            }

            if (!ValidateBytes(MemoryAddresses.STORE_MULTIPLIER_HOOK2, MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES))
            {
                byte[] actual = ReadBytes(MemoryAddresses.STORE_MULTIPLIER_HOOK2, 5);
                errorMessage = $"Hook2 (0x{MemoryAddresses.STORE_MULTIPLIER_HOOK2:X}): bytes invalides. " +
                              $"Attendu: {BitConverter.ToString(MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES)}, " +
                              $"Trouvé: {BitConverter.ToString(actual)}. Version du jeu incompatible.";
                return false;
            }

            if (!ValidateBytes(MemoryAddresses.STORE_MULTIPLIER_HOOK3, MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES))
            {
                byte[] actual = ReadBytes(MemoryAddresses.STORE_MULTIPLIER_HOOK3, 5);
                errorMessage = $"Hook3 (0x{MemoryAddresses.STORE_MULTIPLIER_HOOK3:X}): bytes invalides. " +
                              $"Attendu: {BitConverter.ToString(MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES)}, " +
                              $"Trouvé: {BitConverter.ToString(actual)}. Version du jeu incompatible.";
                return false;
            }

            // Allocate code caves for the three injection points
            _storeItemMultiplierCodeCave1 = _backend.AllocateMemory(256);
            _storeItemMultiplierCodeCave2 = _backend.AllocateMemory(256);
            _storeItemMultiplierCodeCave3 = _backend.AllocateMemory(256);

            if (_storeItemMultiplierCodeCave1 == IntPtr.Zero ||
                _storeItemMultiplierCodeCave2 == IntPtr.Zero ||
                _storeItemMultiplierCodeCave3 == IntPtr.Zero)
            {
                CleanupCodeCaves();
                errorMessage = "Impossible d'allouer la mémoire pour le code cave (Non supporté sur cette plateforme ?)";
                return false;
            }

            const int multiplier = 2457;

            // Each code cave: multiply ecx by 2457, then mov [rsi+10],ecx; mov eax,ebx; jmp back
            bool success = InjectMultiplierCodeCave(MemoryAddresses.STORE_MULTIPLIER_HOOK1, _storeItemMultiplierCodeCave1, multiplier);
            success &= InjectMultiplierCodeCave(MemoryAddresses.STORE_MULTIPLIER_HOOK2, _storeItemMultiplierCodeCave2, multiplier);
            success &= InjectMultiplierCodeCave(MemoryAddresses.STORE_MULTIPLIER_HOOK3, _storeItemMultiplierCodeCave3, multiplier);

            if (!success)
            {
                RemoveStoreItemMultiplier();
                errorMessage = "Échec de l'injection du code cave";
            }

            return success;
        }
        catch (Exception ex)
        {
            CleanupCodeCaves();
            errorMessage = $"Exception: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Active le multiplicateur d'items en magasin (x2457).
    /// </summary>
    public bool InjectStoreItemMultiplier()
    {
        return InjectStoreItemMultiplier(out _);
    }

    private bool InjectMultiplierCodeCave(long hookOffset, IntPtr codeCave, int multiplier)
    {
        IntPtr hookAddress = new IntPtr(ModuleBase.ToInt64() + hookOffset);
        IntPtr returnAddress = new IntPtr(hookAddress.ToInt64() + 5); // After 5-byte JMP

        // Build code cave assembly:
        // imul ecx, ecx, multiplier  ; ecx = ecx * 2457
        // mov [rsi+10], ecx          ; original instruction
        // mov eax, ebx               ; original instruction
        // jmp back                   ; return to original code
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // imul ecx, ecx, imm32 (6 bytes: 69 C9 xx xx xx xx)
        bw.Write((byte)0x69);
        bw.Write((byte)0xC9);
        bw.Write(multiplier);

        // mov [rsi+10], ecx (3 bytes: 89 4E 10)
        bw.Write((byte)0x89);
        bw.Write((byte)0x4E);
        bw.Write((byte)0x10);

        // mov eax, ebx (2 bytes: 8B C3)
        bw.Write((byte)0x8B);
        bw.Write((byte)0xC3);

        // jmp returnAddress (5 bytes: E9 xx xx xx xx)
        bw.Write((byte)0xE9);
        int jumpOffset = (int)(returnAddress.ToInt64() - (codeCave.ToInt64() + (int)ms.Position + 4));
        bw.Write(jumpOffset);

        byte[] codeCaveBytes = ms.ToArray();

        // Write code cave using direct WriteProcessMemory (not relative offset)
        if (!_backend.Write(codeCave, codeCaveBytes))
            return false;

        // Patch hook point with JMP to code cave
        Span<byte> jmpBytes = stackalloc byte[5];
        jmpBytes[0] = 0xE9; // JMP rel32
        int jmpOffset = (int)(codeCave.ToInt64() - hookAddress.ToInt64() - 5);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(jmpBytes[1..], jmpOffset);

        return WriteBytesProtected(hookOffset, jmpBytes.ToArray());
    }

    /// <summary>
    /// Désactive le multiplicateur d'items en magasin.
    /// </summary>
    public bool RemoveStoreItemMultiplier()
    {
        if (!IsAttached)
            return false;

        try
        {
            // Restore original bytes at all three hook points
            bool success = WriteBytesProtected(MemoryAddresses.STORE_MULTIPLIER_HOOK1, MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES);
            success &= WriteBytesProtected(MemoryAddresses.STORE_MULTIPLIER_HOOK2, MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES);
            success &= WriteBytesProtected(MemoryAddresses.STORE_MULTIPLIER_HOOK3, MemoryAddresses.STORE_MULTIPLIER_ORIGINAL_BYTES);

            CleanupCodeCaves();

            return success;
        }
        catch
        {
            return false;
        }
    }

    #region Advanced Patches - Passive Value Editing

    public bool InjectPassiveValueEditing()
    {
        if (!IsAttached) return false;

        try
        {
            // AOB scan for "48 8B 0F 0F 57 C9 F3 * * * E8 * * * * EB * 0F"
            byte[] aobPattern = {
                0x48, 0x8B, 0x0F, 0x0F, 0x57, 0xC9, 0xF3, 0x00, 0x00, 0x00,
                0xE8, 0x00, 0x00, 0x00, 0x00, 0xEB, 0x00, 0x0F
            };
            byte[] aobMask = {
                1, 1, 1, 1, 1, 1, 1, 0, 0, 0,
                1, 0, 0, 0, 0, 1, 0, 1
            };
            
            _passiveValueHookAddress = AOBScan(aobPattern, aobMask);

            if (_passiveValueHookAddress == IntPtr.Zero)
                throw new Exception("Failed to find Passive Value AOB pattern");

            // Allocate memory
            _passiveValueCodeCave = _backend.AllocateMemory(4096, _passiveValueHookAddress);
            if (_passiveValueCodeCave == IntPtr.Zero)
                throw new Exception("Failed to allocate memory");

            // Set addresses
            _passiveValAdrAddress = IntPtr.Add(_passiveValueCodeCave, 100);
            _passiveValTypeAddress = IntPtr.Add(_passiveValueCodeCave, 108);

            // Build injected code
            List<byte> injectedCode = new List<byte>();

            // mov [passiveValAdr],rax  (48 A3 + 8-byte address)
            injectedCode.AddRange(new byte[] { 0x48, 0xA3 });
            injectedCode.AddRange(BitConverter.GetBytes(_passiveValAdrAddress.ToInt64()));

            // mov cx,[rax+C]  (66 8B 48 0C)
            injectedCode.AddRange(new byte[] { 0x66, 0x8B, 0x48, 0x0C });

            // mov [passiveValType],cx  (66 89 0D + 4-byte RIP-relative offset)
            injectedCode.AddRange(new byte[] { 0x66, 0x89, 0x0D });
            long typeOffset = _passiveValTypeAddress.ToInt64() - (_passiveValueCodeCave.ToInt64() + injectedCode.Count + 4);
            injectedCode.AddRange(BitConverter.GetBytes((int)typeOffset));

            // Original code: mov rcx,[rdi]  (48 8B 0F)
            injectedCode.AddRange(new byte[] { 0x48, 0x8B, 0x0F });

            // Original code: xorps xmm1,xmm1  (0F 57 C9)
            injectedCode.AddRange(new byte[] { 0x0F, 0x57, 0xC9 });

            // jmp to return address (E9 + 4-byte offset)
            injectedCode.Add(0xE9);
            IntPtr returnAddress = IntPtr.Add(_passiveValueHookAddress, 6);
            long jmpOffset = returnAddress.ToInt64() - (_passiveValueCodeCave.ToInt64() + injectedCode.Count + 4);
            injectedCode.AddRange(BitConverter.GetBytes((int)jmpOffset));

            // Write injected code
            if (!_backend.Write(_passiveValueCodeCave, injectedCode.ToArray()))
            {
                _backend.FreeMemory(_passiveValueCodeCave);
                _passiveValueCodeCave = IntPtr.Zero;
                return false;
            }

            // Initialize variables
            _backend.Write(_passiveValAdrAddress, new byte[8]);
            _backend.Write(_passiveValTypeAddress, new byte[4]);

            // Create hook: jmp to code cave
            long jmpToCodeCave = _passiveValueCodeCave.ToInt64() - (_passiveValueHookAddress.ToInt64() + 5);
            byte[] hookBytes = new byte[6];
            hookBytes[0] = 0xE9;
            BitConverter.GetBytes((int)jmpToCodeCave).CopyTo(hookBytes, 1);
            hookBytes[5] = 0x90; // NOP

            if (!_backend.WriteProtected(_passiveValueHookAddress, hookBytes))
            {
                _backend.FreeMemory(_passiveValueCodeCave);
                _passiveValueCodeCave = IntPtr.Zero;
                return false;
            }

            return true;
        }
        catch
        {
            RemovePassiveValueEditing();
            return false;
        }
    }

    public bool RemovePassiveValueEditing()
    {
        if (!IsAttached) return false;

        try
        {
            bool success = true;

            if (_passiveValueHookAddress != IntPtr.Zero)
            {
                byte[] original = { 0x48, 0x8B, 0x0F, 0x0F, 0x57, 0xC9 };
                success &= _backend.WriteProtected(_passiveValueHookAddress, original);
                _passiveValueHookAddress = IntPtr.Zero;
            }

            if (_passiveValueCodeCave != IntPtr.Zero)
            {
                _backend.FreeMemory(_passiveValueCodeCave);
                _passiveValueCodeCave = IntPtr.Zero;
            }

            _passiveValAdrAddress = IntPtr.Zero;
            _passiveValTypeAddress = IntPtr.Zero;

            return success;
        }
        catch
        {
            return false;
        }
    }

    public (bool hasValue, int valueType, double currentValue) ReadPassiveValue()
    {
        if (!IsAttached || _passiveValAdrAddress == IntPtr.Zero) return (false, 0, 0);

        try
        {
            byte[] adrBuffer = new byte[8];
            if (_backend.Read(_passiveValAdrAddress, adrBuffer) != 8) return (false, 0, 0);
            
            long passiveValAdr = BitConverter.ToInt64(adrBuffer);
            if (passiveValAdr == 0) return (false, 0, 0);

            byte[] typeBuffer = new byte[4];
            if (_backend.Read(_passiveValTypeAddress, typeBuffer) != 4) return (false, 0, 0);
            
            int passiveValType = BitConverter.ToInt32(typeBuffer);

            byte[] valueBuffer = new byte[4];
            if (_backend.Read(new IntPtr(passiveValAdr), valueBuffer) != 4) return (false, 0, 0);

            double val = passiveValType == 2 
                ? BitConverter.ToSingle(valueBuffer) 
                : BitConverter.ToInt32(valueBuffer);

            return (true, passiveValType, val);
        }
        catch
        {
            return (false, 0, 0);
        }
    }

    public bool WritePassiveValue(string valueString)
    {
        if (!IsAttached || _passiveValAdrAddress == IntPtr.Zero) return false;

        try
        {
            byte[] adrBuffer = new byte[8];
            if (_backend.Read(_passiveValAdrAddress, adrBuffer) != 8) return false;
            
            long passiveValAdr = BitConverter.ToInt64(adrBuffer);
            if (passiveValAdr == 0) return false;

            byte[] typeBuffer = new byte[4];
            if (_backend.Read(_passiveValTypeAddress, typeBuffer) != 4) return false;
            
            int passiveValType = BitConverter.ToInt32(typeBuffer);
            byte[] valueBuffer;

            if (passiveValType == 2)
            {
                if (!float.TryParse(valueString, out float f)) return false;
                valueBuffer = BitConverter.GetBytes(f);
            }
            else
            {
                if (!int.TryParse(valueString, out int i)) return false;
                valueBuffer = BitConverter.GetBytes(i);
            }

            return _backend.Write(new IntPtr(passiveValAdr), valueBuffer);
        }
        catch
        {
            return false;
        }
    }

    #endregion

    public bool InjectSpiritIncrement()
    {
        if (!IsAttached)
            return false;

        try
        {
            // Heroes Spirits: AOB scan for "66 89 68 0C 48"
            byte[] heroesAOB = { 0x66, 0x89, 0x68, 0x0C, 0x48 };
            IntPtr heroesAddress = AOBScan(heroesAOB);

            if (heroesAddress == IntPtr.Zero)
                throw new Exception("Failed to find Heroes Spirits AOB pattern");

            // Inject Heroes Spirits (5 bytes to replace for jmp instruction)
            InjectSpiritAtAddress(heroesAddress, 5, true, ref _heroSpiritIncrementCodeCave);

            return true;
        }
        catch (Exception)
        {
            RemoveSpiritIncrement();
            return false;
        }
    }

    private bool InjectSpiritAtAddress(IntPtr address, int bytesToReplace, bool isHeroSpirit, ref IntPtr codeCave)
    {
        try
        {
            // Allocate memory near address (±2GB)
            // Backend.AllocateMemory supports hint address
            codeCave = _backend.AllocateMemory(2048, address);

            if (codeCave == IntPtr.Zero)
                throw new Exception("Failed to allocate memory within ±2GB range");

            // Build injected code
            byte[] injectedCode;
            int jmpOffsetPos;

            if (isHeroSpirit)
            {
                // Heroes: add bp, 2; mov [rax+0C],bp; jmp to original destination
                injectedCode = new byte[13];
                injectedCode[0] = 0x66; injectedCode[1] = 0x83; injectedCode[2] = 0xC5; injectedCode[3] = 0x02; // add bp, 2
                injectedCode[4] = 0x66; injectedCode[5] = 0x89; injectedCode[6] = 0x68; injectedCode[7] = 0x0C; // mov [rax+0C],bp
                injectedCode[8] = 0xE9; // jmp (offset will be calculated)
                jmpOffsetPos = 9;
            }
            else
            {
                // Elite: add bp, 2; mov [r8+rdi*2+10],bp; jmp back
                injectedCode = new byte[15];
                injectedCode[0] = 0x66; injectedCode[1] = 0x83; injectedCode[2] = 0xC5; injectedCode[3] = 0x02; // add bp, 2
                injectedCode[4] = 0x66; injectedCode[5] = 0x41; injectedCode[6] = 0x89; injectedCode[7] = 0x6C;
                injectedCode[8] = 0x78; injectedCode[9] = 0x10; // mov [r8+rdi*2+10],bp
                injectedCode[10] = 0xE9; // jmp (offset will be calculated)
                jmpOffsetPos = 11;
            }

            // Calculate jump back
            long originalDest = address.ToInt64() + bytesToReplace;
            long jmpOffset = originalDest - (codeCave.ToInt64() + injectedCode.Length);

            byte[] offsetBytes = BitConverter.GetBytes((int)jmpOffset);
            Array.Copy(offsetBytes, 0, injectedCode, jmpOffsetPos, 4);

            // Write injected code
            if (!_backend.Write(codeCave, injectedCode))
            {
                _backend.FreeMemory(codeCave);
                codeCave = IntPtr.Zero;
                return false;
            }

            // Create hook: replace original bytes with jump to code cave
            long jmpToCodeCave = codeCave.ToInt64() - (address.ToInt64() + 5);
            byte[] hookBytes = new byte[bytesToReplace];
            hookBytes[0] = 0xE9; // jmp opcode
            BitConverter.GetBytes((int)jmpToCodeCave).CopyTo(hookBytes, 1);

            // Fill remaining bytes with NOPs
            for (int i = 5; i < bytesToReplace; i++)
                hookBytes[i] = 0x90;

            if (!_backend.WriteProtected(address, hookBytes))
            {
                _backend.FreeMemory(codeCave);
                codeCave = IntPtr.Zero;
                return false;
            }

            return true;
        }
        catch
        {
            if (codeCave != IntPtr.Zero)
            {
                _backend.FreeMemory(codeCave);
                codeCave = IntPtr.Zero;
            }
            return false;
        }
    }

    public bool RemoveSpiritIncrement()
    {
        if (!IsAttached) return false;

        try
        {
            bool success = true;

            // Restore Heroes Spirits
            if (_heroSpiritIncrementCodeCave != IntPtr.Zero)
            {
                IntPtr heroesAddress = IntPtr.Add(ModuleBase, 0xCF178A);
                byte[] original = { 0x66, 0x89, 0x68, 0x0C, 0x48 };
                success &= _backend.WriteProtected(heroesAddress, original);
                
                _backend.FreeMemory(_heroSpiritIncrementCodeCave);
                _heroSpiritIncrementCodeCave = IntPtr.Zero;
            }

            return success;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Advanced Patches - Spirit Card Injection

    public bool EnableSpiritCardInjection()
    {
        if (!IsAttached) return false;
        if (_isSpiritCardInjectionEnabled) return true;

        try
        {
            // AOB scan for "49 8B 07 49 8B CF FF * * 45 * * 44"
            byte[] aobPattern = { 0x49, 0x8B, 0x07, 0x49, 0x8B, 0xCF, 0xFF, 0x00, 0x00, 0x45, 0x00, 0x00, 0x44 };
            byte[] aobMask = { 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 1 };

            _spiritCardHookAddress = AOBScan(aobPattern, aobMask);
            if (_spiritCardHookAddress == IntPtr.Zero)
                throw new Exception("Failed to find spirit card injection point");

            // Allocate memory
            _spiritCardInjectionCodeCave = _backend.AllocateMemory(4096, _spiritCardHookAddress);
            if (_spiritCardInjectionCodeCave == IntPtr.Zero)
                throw new Exception("Failed to allocate memory");

            // Memory layout
            _cfHerospiritAddTypeAddress = IntPtr.Add(_spiritCardInjectionCodeCave, 0x200);
            _cfHerospiritIDAddress = IntPtr.Add(_spiritCardInjectionCodeCave, 0x204);
            IntPtr addherospiritdataAddress = IntPtr.Add(_spiritCardInjectionCodeCave, 0x208);
            IntPtr addherospiritTempAddress = IntPtr.Add(_spiritCardInjectionCodeCave, 0x218);
            IntPtr herospiritIDAllAddress = IntPtr.Add(_spiritCardInjectionCodeCave, 0x300);

            // Initialize
            _backend.Write(_cfHerospiritAddTypeAddress, BitConverter.GetBytes(1)); // Add-One
            _backend.Write(_cfHerospiritIDAddress, BitConverter.GetBytes(0));

            // Build injection code
            List<byte> injectionCode = new List<byte>();

            // cmp [r15+8],1
            injectionCode.AddRange(new byte[] { 0x49, 0x83, 0x7F, 0x08, 0x01 });

            // jne code (placeholder)
            int jneCodeOffset = injectionCode.Count;
            injectionCode.AddRange(new byte[] { 0x0F, 0x85, 0x00, 0x00, 0x00, 0x00 });

            // cmp [cfHerospiritAddType],0
            injectionCode.AddRange(new byte[] { 0x83, 0x3D });
            int cfTypeOffset = (int)(_cfHerospiritAddTypeAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 5));
            injectionCode.AddRange(BitConverter.GetBytes(cfTypeOffset));
            injectionCode.Add(0x00);

            // je codeA (placeholder)
            int jeCodeAOffset = injectionCode.Count;
            injectionCode.AddRange(new byte[] { 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00 });

            // Add-One block
            int addOneStart = injectionCode.Count;
            
            // mov eax,[cfHerospiritID]
            injectionCode.AddRange(new byte[] { 0x8B, 0x05 });
            int cfIDOffset = (int)(_cfHerospiritIDAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 4));
            injectionCode.AddRange(BitConverter.GetBytes(cfIDOffset));

            // mov [addherospiritdata],eax
            injectionCode.AddRange(new byte[] { 0x89, 0x05 });
            int dataOffset1 = (int)(addherospiritdataAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 4));
            injectionCode.AddRange(BitConverter.GetBytes(dataOffset1));

            // call fncAddHerospirit (placeholder)
            int callFncOffset = injectionCode.Count;
            injectionCode.AddRange(new byte[] { 0xE8, 0x00, 0x00, 0x00, 0x00 });

            // jmp code (placeholder)
            int jmpFromAddOne = injectionCode.Count;
            injectionCode.AddRange(new byte[] { 0xE9, 0x00, 0x00, 0x00, 0x00 });

            // codeA (Add-All)
            int codeAStart = injectionCode.Count;
            
            // lea rcx,[herospiritIDAll]
            injectionCode.AddRange(new byte[] { 0x48, 0x8D, 0x0D });
            int allIDOffset = (int)(herospiritIDAllAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 4));
            injectionCode.AddRange(BitConverter.GetBytes(allIDOffset));

            // codeR (loop)
            int codeRStart = injectionCode.Count;
            
            // cmp [rcx],0
            injectionCode.AddRange(new byte[] { 0x83, 0x39, 0x00 });

            // je code (placeholder)
            int jeCodeFromLoop = injectionCode.Count;
            injectionCode.AddRange(new byte[] { 0x0F, 0x84, 0x00, 0x00, 0x00, 0x00 });

            // mov eax,[rcx]
            injectionCode.AddRange(new byte[] { 0x8B, 0x01 });

            // mov [addherospiritdata],eax
            injectionCode.AddRange(new byte[] { 0x89, 0x05 });
            int dataOffset2 = (int)(addherospiritdataAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 4));
            injectionCode.AddRange(BitConverter.GetBytes(dataOffset2));

            // call fncAddHerospirit (placeholder)
            int callFncFromLoop = injectionCode.Count;
            injectionCode.AddRange(new byte[] { 0xE8, 0x00, 0x00, 0x00, 0x00 });

            // add rcx,4
            injectionCode.AddRange(new byte[] { 0x48, 0x83, 0xC1, 0x04 });

            // jmp codeR
            injectionCode.Add(0xE9);
            injectionCode.AddRange(BitConverter.GetBytes(codeRStart - (injectionCode.Count + 4)));

            // code label
            int codeLabel = injectionCode.Count;

            // Patch jump offsets
            byte[] codeArray = injectionCode.ToArray();
            
            // jne code
            BitConverter.GetBytes(codeLabel - (jneCodeOffset + 6)).CopyTo(codeArray, jneCodeOffset + 2);
            // je codeA
            BitConverter.GetBytes(codeAStart - (jeCodeAOffset + 6)).CopyTo(codeArray, jeCodeAOffset + 2);
            // jmp code (from Add-One)
            BitConverter.GetBytes(codeLabel - (jmpFromAddOne + 5)).CopyTo(codeArray, jmpFromAddOne + 1);
            // je code (from loop)
            BitConverter.GetBytes(codeLabel - (jeCodeFromLoop + 6)).CopyTo(codeArray, jeCodeFromLoop + 2);

            injectionCode = codeArray.ToList();

            // Original code
            injectionCode.AddRange(new byte[] { 0x49, 0x8B, 0x07 }); // mov rax,[r15]
            injectionCode.AddRange(new byte[] { 0x49, 0x8B, 0xCF }); // mov rcx,r15

            // jmp return
            injectionCode.Add(0xE9);
            IntPtr returnAddress = IntPtr.Add(_spiritCardHookAddress, 6);
            long returnJmpOffset = returnAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 4);
            injectionCode.AddRange(BitConverter.GetBytes((int)returnJmpOffset));

            // fncAddHerospirit
            int fncStart = injectionCode.Count;

            // Patch calls to fnc
            codeArray = injectionCode.ToArray();
            BitConverter.GetBytes(fncStart - (callFncOffset + 5)).CopyTo(codeArray, callFncOffset + 1);
            BitConverter.GetBytes(fncStart - (callFncFromLoop + 5)).CopyTo(codeArray, callFncFromLoop + 1);
            injectionCode = codeArray.ToList();

            // Function body
            injectionCode.AddRange(new byte[] { 0x51, 0x52, 0x41, 0x50, 0x41, 0x51 }); // push regs

            // lea r8,[addherospiritdata]
            injectionCode.AddRange(new byte[] { 0x4C, 0x8D, 0x05 });
            int r8Offset = (int)(addherospiritdataAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 4));
            injectionCode.AddRange(BitConverter.GetBytes(r8Offset));

            // lea rdx,[addherospiritTemp]
            injectionCode.AddRange(new byte[] { 0x48, 0x8D, 0x15 });
            int rdxOffset = (int)(addherospiritTempAddress.ToInt64() - (_spiritCardInjectionCodeCave.ToInt64() + injectionCode.Count + 4));
            injectionCode.AddRange(BitConverter.GetBytes(rdxOffset));

            // mov r9d,1; mov rcx,r15; mov rax,[r15]; call [rax+20]
            injectionCode.AddRange(new byte[] { 
                0x41, 0xB9, 0x01, 0x00, 0x00, 0x00,
                0x49, 0x8B, 0xCF,
                0x49, 0x8B, 0x07,
                0xFF, 0x50, 0x20
            });

            // pop regs; ret
            injectionCode.AddRange(new byte[] { 0x41, 0x59, 0x41, 0x58, 0x5A, 0x59, 0xC3 });

            // Init data structure
            byte[] dataInit = new byte[16];
            BitConverter.GetBytes(1).CopyTo(dataInit, 8); // Quantity = 1
            _backend.Write(addherospiritdataAddress, dataInit);

            // Write code
            if (!_backend.Write(_spiritCardInjectionCodeCave, injectionCode.ToArray()))
            {
                DisableSpiritCardInjection();
                return false;
            }

            // Hook
            long hookJmpOffset = _spiritCardInjectionCodeCave.ToInt64() - (_spiritCardHookAddress.ToInt64() + 5);
            byte[] hookBytes = new byte[6];
            hookBytes[0] = 0xE9;
            BitConverter.GetBytes((int)hookJmpOffset).CopyTo(hookBytes, 1);
            hookBytes[5] = 0x90;

            if (!_backend.WriteProtected(_spiritCardHookAddress, hookBytes))
            {
                DisableSpiritCardInjection();
                return false;
            }

            _isSpiritCardInjectionEnabled = true;
            return true;
        }
        catch
        {
            DisableSpiritCardInjection();
            return false;
        }
    }

    public bool DisableSpiritCardInjection()
    {
        if (!IsAttached) return false;

        try
        {
            if (_spiritCardHookAddress != IntPtr.Zero)
            {
                byte[] original = { 0x49, 0x8B, 0x07, 0x49, 0x8B, 0xCF };
                _backend.WriteProtected(_spiritCardHookAddress, original);
                _spiritCardHookAddress = IntPtr.Zero;
            }

            if (_spiritCardInjectionCodeCave != IntPtr.Zero)
            {
                _backend.FreeMemory(_spiritCardInjectionCodeCave);
                _spiritCardInjectionCodeCave = IntPtr.Zero;
            }

            _isSpiritCardInjectionEnabled = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SetSpiritCardToAdd(uint spiritId, int quantity)
    {
        if (!_isSpiritCardInjectionEnabled) return false;

        try
        {
            _backend.Write(_cfHerospiritAddTypeAddress, BitConverter.GetBytes(1)); // Add-One
            _backend.Write(_cfHerospiritIDAddress, BitConverter.GetBytes(spiritId));
            return true;
        }
        catch { return false; }
    }

    public bool SetAllSpiritCardsToAdd(List<uint> spiritIds)
    {
        if (!_isSpiritCardInjectionEnabled) return false;

        try
        {
            _backend.Write(_cfHerospiritAddTypeAddress, BitConverter.GetBytes(0)); // Add-All
            
            IntPtr listAddr = IntPtr.Add(_spiritCardInjectionCodeCave, 0x300);
            List<byte> bytes = new List<byte>();
            foreach (var id in spiritIds)
                bytes.AddRange(BitConverter.GetBytes(id));
            bytes.AddRange(BitConverter.GetBytes(0));

            return _backend.Write(listAddr, bytes.ToArray());
        }
        catch { return false; }
    }

    public bool AddSpiritCardToTeam(uint spiritId, int quantity = 1)
    {
        if (!_isSpiritCardInjectionEnabled && !EnableSpiritCardInjection())
            return false;
        
        return SetSpiritCardToAdd(spiritId, quantity);
    }

    #endregion

    #region Advanced Patches - Unlimited Spirits

    public bool EnableUnlimitedSpirits()
    {
        if (!IsAttached) return false;
        if (_isUnlimitedSpiritsEnabled) return true;

        try
        {
            // AOB scan for first pattern: 75 03 8B 58 10 49
            byte[] aobPattern1 = { 0x75, 0x03, 0x8B, 0x58, 0x10, 0x49 };
            byte[] aobMask1 = { 1, 1, 1, 1, 1, 1 };
            _teamDockHero1HookAddress = AOBScan(aobPattern1, aobMask1);

            if (_teamDockHero1HookAddress == IntPtr.Zero)
                throw new Exception("Failed to find Team Dock Hero 1 AOB pattern");

            // AOB scan for second pattern: 75 * 8B 40 10 48
            byte[] aobPattern2 = { 0x75, 0x00, 0x8B, 0x40, 0x10, 0x48 };
            byte[] aobMask2 = { 1, 0, 1, 1, 1, 1 };
            _teamDockHero2HookAddress = AOBScan(aobPattern2, aobMask2);

            if (_teamDockHero2HookAddress == IntPtr.Zero)
                throw new Exception("Failed to find Team Dock Hero 2 AOB pattern");

            // Allocate code cave for first injection
            _teamDockHero1CodeCave = _backend.AllocateMemory(4096, _teamDockHero1HookAddress);
            if (_teamDockHero1CodeCave == IntPtr.Zero)
                throw new Exception("Failed to allocate code cave");

            // Build code cave
            List<byte> codeCave1 = new List<byte>();

            // jne return (placeholder)
            codeCave1.Add(0x75);
            int jneReturnOffset = codeCave1.Count;
            codeCave1.Add(0x00);

            // mov ebx,[rax+10]; cmp ebx,5; jl code; mov ebx,4
            codeCave1.AddRange(new byte[] { 
                0x8B, 0x58, 0x10,
                0x83, 0xFB, 0x05,
                0x7C, 0x05,
                0xBB, 0x04, 0x00, 0x00, 0x00 
            });

            // code: jmp return
            int codeLabel = codeCave1.Count;
            codeCave1.Add(0xE9);
            int jmpReturnOffset = codeCave1.Count;
            codeCave1.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

            int returnLabel = codeCave1.Count;

            // Patch jne
            codeCave1[jneReturnOffset] = (byte)(returnLabel - jneReturnOffset - 1);

            // Patch jmp return
            long returnAddress = _teamDockHero1HookAddress.ToInt64() + 5;
            long jmpTarget = returnAddress - (_teamDockHero1CodeCave.ToInt64() + codeLabel + 5);
            BitConverter.GetBytes((int)jmpTarget).CopyTo(codeCave1.ToArray(), jmpReturnOffset); 
            // Note: modifying local array won't affect list, need to be careful.
            // Let's rebuild list or use array logic like before.
            byte[] caveArray = codeCave1.ToArray();
            BitConverter.GetBytes((int)jmpTarget).CopyTo(caveArray, jmpReturnOffset);
            
            // Write code cave
            if (!_backend.Write(_teamDockHero1CodeCave, caveArray))
            {
                DisableUnlimitedSpirits();
                return false;
            }

            // Hook 1: jmp to cave
            long hookToCodeCave = _teamDockHero1CodeCave.ToInt64() - _teamDockHero1HookAddress.ToInt64() - 5;
            byte[] jmpToCodeCave = new byte[5];
            jmpToCodeCave[0] = 0xE9;
            BitConverter.GetBytes((int)hookToCodeCave).CopyTo(jmpToCodeCave, 1);

            if (!_backend.WriteProtected(_teamDockHero1HookAddress, jmpToCodeCave))
            {
                DisableUnlimitedSpirits();
                return false;
            }

            // Hook 2: Change 75 to EB (jne to jmp)
            if (!_backend.WriteProtected(_teamDockHero2HookAddress, new byte[] { 0xEB }))
            {
                DisableUnlimitedSpirits();
                return false;
            }

            _isUnlimitedSpiritsEnabled = true;
            return true;
        }
        catch
        {
            DisableUnlimitedSpirits();
            return false;
        }
    }

    public bool DisableUnlimitedSpirits()
    {
        if (!IsAttached) return false;

        try
        {
            bool success = true;

            // Restore hook 1: 75 03 8B 58 10
            if (_teamDockHero1HookAddress != IntPtr.Zero)
            {
                byte[] original = { 0x75, 0x03, 0x8B, 0x58, 0x10 };
                success &= _backend.WriteProtected(_teamDockHero1HookAddress, original);
            }

            // Restore hook 2: 75
            if (_teamDockHero2HookAddress != IntPtr.Zero)
            {
                byte[] original = { 0x75 };
                success &= _backend.WriteProtected(_teamDockHero2HookAddress, original);
            }

            // Free cave
            if (_teamDockHero1CodeCave != IntPtr.Zero)
            {
                _backend.FreeMemory(_teamDockHero1CodeCave);
                _teamDockHero1CodeCave = IntPtr.Zero;
            }

            _isUnlimitedSpiritsEnabled = false;
            return success;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    private void CleanupCodeCaves()
    {
        if (_storeItemMultiplierCodeCave1 != IntPtr.Zero) { _backend.FreeMemory(_storeItemMultiplierCodeCave1); _storeItemMultiplierCodeCave1 = IntPtr.Zero; }
        if (_storeItemMultiplierCodeCave2 != IntPtr.Zero) { _backend.FreeMemory(_storeItemMultiplierCodeCave2); _storeItemMultiplierCodeCave2 = IntPtr.Zero; }
        if (_storeItemMultiplierCodeCave3 != IntPtr.Zero) { _backend.FreeMemory(_storeItemMultiplierCodeCave3); _storeItemMultiplierCodeCave3 = IntPtr.Zero; }
        
        if (_heroSpiritIncrementCodeCave != IntPtr.Zero) { _backend.FreeMemory(_heroSpiritIncrementCodeCave); _heroSpiritIncrementCodeCave = IntPtr.Zero; }
        if (_eliteSpiritIncrementCodeCave != IntPtr.Zero) { _backend.FreeMemory(_eliteSpiritIncrementCodeCave); _eliteSpiritIncrementCodeCave = IntPtr.Zero; }
        
        if (_passiveValueCodeCave != IntPtr.Zero) { _backend.FreeMemory(_passiveValueCodeCave); _passiveValueCodeCave = IntPtr.Zero; }
        
        if (_spiritCardInjectionCodeCave != IntPtr.Zero) { _backend.FreeMemory(_spiritCardInjectionCodeCave); _spiritCardInjectionCodeCave = IntPtr.Zero; }
        
        if (_teamDockHero1CodeCave != IntPtr.Zero) { _backend.FreeMemory(_teamDockHero1CodeCave); _teamDockHero1CodeCave = IntPtr.Zero; }
    }

    #region Protected Write Helper

    /// <summary>
    /// Écrit des bytes avec protection mémoire temporairement modifiée.
    /// </summary>
    private bool WriteBytesProtected(long offset, ReadOnlySpan<byte> bytes)
    {
        if (!IsAttached)
            return false;

        try
        {
            IntPtr address = new IntPtr(ModuleBase.ToInt64() + offset);
            return _backend.WriteProtected(address, bytes);
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Libère les ressources utilisées par le service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        DetachFromProcess();
        _backend.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer de sécurité.
    /// </summary>
    ~MemoryEditorService()
    {
        Dispose();
    }

    #endregion
}
