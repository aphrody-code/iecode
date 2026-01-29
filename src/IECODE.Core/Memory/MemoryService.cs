using System.Runtime.Versioning;

namespace IECODE.Core.Memory;

/// <summary>
/// Service d'édition mémoire temps réel pour le processus IEVR.
/// Wrapper autour de SaveEditor.Core.MemoryEditorService.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MemoryService : IDisposable
{
    private readonly IEVRGame _game;
    private MemoryEditorService? _editor;
    private bool _disposed;

    public MemoryService(IEVRGame game)
    {
        _game = game;
    }

    #region Properties

    /// <summary>
    /// Indique si le service est attaché au processus du jeu.
    /// </summary>
    public bool IsAttached => _editor?.IsAttached ?? false;

    /// <summary>
    /// Adresse de base du module nie.exe.
    /// </summary>
    public IntPtr ModuleBase => _editor?.ModuleBase ?? IntPtr.Zero;

    #endregion

    #region Connection

    /// <summary>
    /// Dernier message d'erreur lors de l'attachement.
    /// </summary>
    public string LastError { get; private set; } = string.Empty;

    /// <summary>
    /// Attache le service au processus nie.exe.
    /// </summary>
    /// <returns>True si l'attachement réussit</returns>
    public bool Attach()
    {
        if (_editor != null && _editor.IsAttached)
        {
            return true;
        }

        _editor?.Dispose();
        _editor = new MemoryEditorService("nie");

        bool result = _editor.AttachToProcess(out string errorMessage);
        LastError = errorMessage;
        return result;
    }

    /// <summary>
    /// Détache le service du processus.
    /// </summary>
    public void Detach()
    {
        _editor?.DetachFromProcess();
        LastError = string.Empty;
    }

    /// <summary>
    /// Vérifie si le jeu est en cours d'exécution.
    /// </summary>
    public bool IsProcessRunning()
    {
        return _editor?.IsProcessRunning() ?? new MemoryEditorService("nie").IsProcessRunning();
    }

    /// <summary>
    /// Obtient le statut de connexion.
    /// </summary>
    public string GetStatus()
    {
        return _editor?.GetProcessStatus() ?? "Not initialized";
    }

    #endregion

    #region Memory Reading

    /// <summary>
    /// Lit une valeur Int32 depuis la mémoire du jeu.
    /// </summary>
    /// <param name="baseOffset">Offset depuis la base du module</param>
    /// <param name="offsets">Chaîne de pointeurs</param>
    public int ReadInt32(long baseOffset, params int[] offsets)
    {
        EnsureAttached();
        return _editor!.ReadValue(baseOffset, offsets);
    }

    /// <summary>
    /// Lit une valeur Float32 depuis la mémoire du jeu.
    /// </summary>
    public float ReadFloat(long baseOffset, params int[] offsets)
    {
        EnsureAttached();
        return _editor!.ReadFloatValue(baseOffset, offsets);
    }

    /// <summary>
    /// Lit des bytes bruts depuis la mémoire dans un buffer.
    /// </summary>
    public int ReadBytes(long address, Span<byte> buffer)
    {
        EnsureAttached();
        return _editor!.ReadBytes(address, buffer);
    }

    /// <summary>
    /// Lit des bytes bruts depuis la mémoire.
    /// </summary>
    public byte[] ReadBytes(long address, int length)
    {
        EnsureAttached();
        return _editor!.ReadBytes(address, length);
    }

    #endregion

    #region Memory Writing

    /// <summary>
    /// Écrit une valeur Int32 dans la mémoire du jeu.
    /// </summary>
    /// <param name="baseOffset">Offset depuis la base du module</param>
    /// <param name="offsets">Chaîne de pointeurs</param>
    /// <param name="value">Valeur à écrire</param>
    public bool WriteInt32(long baseOffset, int[] offsets, int value)
    {
        EnsureAttached();
        return _editor!.WriteValue(baseOffset, offsets, value);
    }

    /// <summary>
    /// Écrit une valeur Float32 dans la mémoire du jeu.
    /// </summary>
    public bool WriteFloat(long baseOffset, int[] offsets, float value)
    {
        EnsureAttached();
        return _editor!.WriteFloatValue(baseOffset, offsets, value);
    }

    /// <summary>
    /// Écrit des bytes bruts à une adresse.
    /// Utilisé pour les patches (NOP, JMP, etc.).
    /// </summary>
    public bool WriteBytes(long address, ReadOnlySpan<byte> bytes)
    {
        EnsureAttached();
        return _editor!.WriteBytes(address, bytes);
    }

    /// <summary>
    /// Écrit des bytes bruts à une adresse.
    /// </summary>
    public bool WriteBytes(long address, byte[] bytes) => WriteBytes(address, (ReadOnlySpan<byte>)bytes);

    /// <summary>
    /// Écrit des instructions NOP à une adresse.
    /// </summary>
    /// <param name="address">Adresse relative</param>
    /// <param name="count">Nombre de NOPs (1 NOP = 1 byte)</param>
    public bool WriteNop(long address, int count)
    {
        if (count <= 256)
        {
            Span<byte> nops = stackalloc byte[count];
            nops.Fill(0x90); // x86 NOP
            return WriteBytes(address, nops);
        }
        else
        {
            byte[] nops = new byte[count];
            Array.Fill(nops, (byte)0x90);
            return WriteBytes(address, nops);
        }
    }

    #endregion

    #region Advanced Patches

    /// <summary>
    /// Active le freeze des Stars (les Stars ne diminuent plus).
    /// </summary>
    public bool FreezeStars()
    {
        EnsureAttached();
        return _editor!.FreezeStars();
    }

    /// <summary>
    /// Désactive le freeze des Stars.
    /// </summary>
    public bool UnfreezeStars()
    {
        EnsureAttached();
        return _editor!.UnfreezeStars();
    }

    /// <summary>
    /// Active l'auto-increment des Flowers.
    /// </summary>
    public bool EnableFlowersIncrement()
    {
        EnsureAttached();
        return _editor!.EnableFlowersIncrement();
    }

    /// <summary>
    /// Désactive l'auto-increment des Flowers.
    /// </summary>
    public bool DisableFlowersIncrement()
    {
        EnsureAttached();
        return _editor!.DisableFlowersIncrement();
    }

    /// <summary>
    /// Active le freeze des Spirits.
    /// </summary>
    public bool FreezeSpirits()
    {
        EnsureAttached();
        return _editor!.FreezeSpirits();
    }

    /// <summary>
    /// Désactive le freeze des Spirits.
    /// </summary>
    public bool UnfreezeSpirits()
    {
        EnsureAttached();
        return _editor!.UnfreezeSpirits();
    }

    /// <summary>
    /// Active le multiplicateur d'items en magasin (x2457).
    /// </summary>
    public bool InjectStoreItemMultiplier()
    {
        EnsureAttached();
        return _editor!.InjectStoreItemMultiplier();
    }

    /// <summary>
    /// Active le multiplicateur d'items en magasin (x2457) avec message d'erreur.
    /// </summary>
    /// <param name="errorMessage">Message d'erreur si l'injection échoue</param>
    public bool InjectStoreItemMultiplier(out string errorMessage)
    {
        EnsureAttached();
        return _editor!.InjectStoreItemMultiplier(out errorMessage);
    }

    /// <summary>
    /// Désactive le multiplicateur d'items en magasin.
    /// </summary>
    public bool RemoveStoreItemMultiplier()
    {
        EnsureAttached();
        return _editor!.RemoveStoreItemMultiplier();
    }

    /// <summary>
    /// Active l'auto-increment des Beans.
    /// </summary>
    public bool EnableBeansIncrement()
    {
        EnsureAttached();
        return _editor!.EnableBeansIncrement();
    }

    /// <summary>
    /// Désactive l'auto-increment des Beans.
    /// </summary>
    public bool DisableBeansIncrement()
    {
        EnsureAttached();
        return _editor!.DisableBeansIncrement();
    }

    /// <summary>
    /// Multiplie tous les items de l'inventaire par un facteur donné.
    /// </summary>
    /// <param name="multiplier">Facteur de multiplication (2-100)</param>
    /// <returns>Nombre d'items modifiés, ou -1 en cas d'erreur</returns>
    public int MultiplyAllItems(int multiplier = 2)
    {
        EnsureAttached();
        return _editor!.MultiplyAllItems(multiplier);
    }

    /// <summary>
    /// Met tous les items de l'inventaire à une quantité maximale.
    /// </summary>
    /// <param name="maxQuantity">Quantité à définir (défaut: 999)</param>
    /// <returns>Nombre d'items modifiés, ou -1 en cas d'erreur</returns>
    public int MaxAllItems(int maxQuantity = 999)
    {
        EnsureAttached();
        return _editor!.MaxAllItems(maxQuantity);
    }

    /// <summary>
    /// Active la fonctionnalité Unlimited Heroes (5 esprits héros dans l'équipe).
    /// Utilise un AOB scan et code injection identique à IEVR-Save-Editor.
    /// </summary>
    public bool EnableUnlimitedHeroes()
    {
        EnsureAttached();
        return _editor!.EnableUnlimitedSpirits();
    }

    /// <summary>
    /// Désactive la fonctionnalité Unlimited Heroes.
    /// </summary>
    public bool DisableUnlimitedHeroes()
    {
        EnsureAttached();
        return _editor!.DisableUnlimitedSpirits();
    }

    #endregion

    #region IEVR-Specific Values

    /// <summary>
    /// Offsets connus pour IEVR (à remplir avec les valeurs découvertes).
    /// </summary>
    public static class KnownOffsets
    {
        // Exemples - à remplir avec les vraies valeurs
        public static readonly (long baseOffset, int[] offsets) PlayerMoney = (0x0, Array.Empty<int>());
        public static readonly (long baseOffset, int[] offsets) PlayerExp = (0x0, Array.Empty<int>());

        // TODO: Ajouter les offsets découverts via analyse
    }

    #endregion

    #region Private Methods

    private void EnsureAttached()
    {
        if (!IsAttached)
        {
            throw new InvalidOperationException("Memory service is not attached to the game process. Call Attach() first.");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        _editor?.Dispose();
        _editor = null;
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    ~MemoryService()
    {
        Dispose();
    }

    #endregion
}
