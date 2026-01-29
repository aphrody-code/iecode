using System.Runtime.Versioning;
using IECODE.Core.Steam;

namespace IECODE.Core.Game.Saves;

/// <summary>
/// High-level save system manager integrating Steam DRM validation.
/// Implements the cloud-native save architecture discovered in nie.exe.
/// </summary>
/// <remarks>
/// Source: docs/nie-analysis/SAVE_SYSTEM_ANALYSIS.md
/// Save workflow: Game → Steam Ticket → EOS Cloud → Server Validation
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class SaveSystemManager : IDisposable
{
    private readonly uint _appId;
    private readonly byte[] _decryptionKey;
    private bool _disposed;

    /// <summary>
    /// Creates a new save system manager.
    /// </summary>
    /// <param name="appId">Steam App ID (for IEVR: 2697250)</param>
    /// <param name="decryptionKey">32-byte ticket decryption key (hardcoded in executable)</param>
    public SaveSystemManager(uint appId, byte[] decryptionKey)
    {
        ArgumentNullException.ThrowIfNull(decryptionKey);
        
        if (decryptionKey.Length != 32)
            throw new ArgumentException("Decryption key must be 32 bytes", nameof(decryptionKey));

        _appId = appId;
        _decryptionKey = decryptionKey;
    }

    /// <summary>
    /// Validates a save container using Steam encrypted ticket.
    /// </summary>
    /// <param name="container">Save container to validate</param>
    /// <param name="encryptedTicket">Steam encrypted app ticket</param>
    /// <param name="expectedSteamId">Expected Steam user ID</param>
    /// <param name="expectedUserData">Expected user metadata (save version/build)</param>
    /// <returns>True if save is valid and belongs to the expected user</returns>
    public bool ValidateSave(
        SaveContainer container,
        ReadOnlySpan<byte> encryptedTicket,
        ulong expectedSteamId,
        ReadOnlySpan<byte> expectedUserData)
    {
        return SteamEncryptedAppTicket.ValidateSaveTicket(
            encryptedTicket,
            _decryptionKey,
            _appId,
            expectedSteamId,
            expectedUserData);
    }

    /// <summary>
    /// Parses a save container from binary data.
    /// </summary>
    /// <param name="data">Raw save container data (0x1040 bytes)</param>
    /// <returns>Parsed save container</returns>
    public static SaveContainer ParseContainer(ReadOnlySpan<byte> data)
    {
        if (data.Length < SaveContainer.ContainerSize)
            throw new ArgumentException(
                $"Insufficient data for SaveContainer (expected {SaveContainer.ContainerSize} bytes, got {data.Length})");

        return System.Runtime.InteropServices.MemoryMarshal.Read<SaveContainer>(data);
    }

    /// <summary>
    /// Finds all AutoSave slots in a container.
    /// </summary>
    /// <remarks>
    /// Based on FUN_14151d1a0 logic (lines 3724305-3724350).
    /// </remarks>
    public static List<(int SlotIndex, uint SaveId)> FindAutoSaveSlots(SaveContainer container)
    {
        var results = new List<(int SlotIndex, uint SaveId)>();
        var autoSaveSlots = container.FindSlotsByIdentifier("AUTOSAVE");

        foreach (var (index, slot) in autoSaveSlots)
        {
            uint saveId = slot.GetSaveId();
            results.Add((index, saveId));
        }

        return results;
    }

    /// <summary>
    /// Checks if a save container has a header save.
    /// </summary>
    /// <remarks>
    /// Based on FUN_14151d4a0 logic (lines 3724771-3724820).
    /// </remarks>
    public static bool HasHeaderSave(SaveContainer container)
    {
        var headerSlots = container.FindSlotsByIdentifier("HEADERSAVE");
        return headerSlots.Count > 0;
    }

    /// <summary>
    /// Checks if a save container has a system save.
    /// </summary>
    public static bool HasSystemSave(SaveContainer container)
    {
        var systemSlots = container.FindSlotsByIdentifier("SYSTEM");
        return systemSlots.Count > 0;
    }

    /// <summary>
    /// Gets a summary of save container contents.
    /// </summary>
    public static SaveContainerInfo GetContainerInfo(SaveContainer container)
    {
        return new SaveContainerInfo
        {
            SaveName = container.GetSaveName(),
            SaveType = container.GetSaveType(),
            SlotCount = (int)container.SlotCount,
            HasLongName = container.HasLongName != 0,
            AutoSaveCount = container.FindSlotsByIdentifier("AUTOSAVE").Count,
            HasHeaderSave = HasHeaderSave(container),
            HasSystemSave = HasSystemSave(container),
            Slots = container.GetAllSlots()
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        Array.Clear(_decryptionKey, 0, _decryptionKey.Length);
        _disposed = true;
    }
}

/// <summary>
/// Summary information about a save container.
/// </summary>
public record SaveContainerInfo
{
    public required string SaveName { get; init; }
    public required SaveType SaveType { get; init; }
    public required int SlotCount { get; init; }
    public required bool HasLongName { get; init; }
    public required int AutoSaveCount { get; init; }
    public required bool HasHeaderSave { get; init; }
    public required bool HasSystemSave { get; init; }
    public required SaveSlotEntry[] Slots { get; init; }
}
