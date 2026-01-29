namespace IECODE.Core.Game.Saves;

/// <summary>
/// Save type identifiers discovered in nie.exe reverse engineering.
/// </summary>
/// <remarks>
/// Source: docs/nie-analysis/SAVE_SYSTEM_ANALYSIS.md
/// Line references: 2180562-2180566
/// </remarks>
public enum SaveType : byte
{
    /// <summary>
    /// Regular save slots - Standard player saves.
    /// </summary>
    Regular = 0x00,

    /// <summary>
    /// System save - Game settings, preferences, unlocks.
    /// Identifier: "SYSTEM" (6 bytes, DAT_1417d222c)
    /// </summary>
    System = 0x01,

    /// <summary>
    /// Auto-save slots - Automatic checkpoint saves.
    /// Identifier: "AUTOSAVE" (8 bytes, DAT_1417d2268/DAT_1417d2288)
    /// </summary>
    AutoSave = 0x02,

    /// <summary>
    /// Save header/metadata - Contains save list and metadata.
    /// Identifier: "HEADERSAVE" (10 bytes, DAT_1417d2258)
    /// </summary>
    Header = 0x03,

    /// <summary>
    /// Unknown save type 4 - Purpose TBD.
    /// </summary>
    Unknown4 = 0x04
}
