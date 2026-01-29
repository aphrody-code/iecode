using System.Runtime.InteropServices;

namespace IECODE.Core.Game.Saves;

/// <summary>
/// Save container structure holding slot data and metadata.
/// Maximum 8 slots per container, each 0x20 (32) bytes.
/// Total container size: 0x1040 bytes.
/// </summary>
/// <remarks>
/// Source: docs/nie-analysis/SAVE_SYSTEM_ANALYSIS.md
/// Discovered through reverse engineering of nie.exe save functions.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SaveContainer
{
    /// <summary>
    /// Unknown field at offset 0x00.
    /// </summary>
    public ulong Unknown0;

    /// <summary>
    /// Save flags/type identifier at offset 0x08.
    /// </summary>
    public uint Flags;

    /// <summary>
    /// Reserved/padding bytes (0x0C - 0x5F = 84 bytes).
    /// </summary>
    private unsafe fixed byte _reserved1[84];

    /// <summary>
    /// Pointer to slot data at offset 0x60 (8 bytes on x64).
    /// </summary>
    public long SlotPointer;

    /// <summary>
    /// Reserved/padding bytes (0x68 - 0x11F = 184 bytes).
    /// </summary>
    private unsafe fixed byte _reserved2[184];

    /// <summary>
    /// Save name at offset 0x120 (128 bytes) - used when HasLongName = false.
    /// </summary>
    private unsafe fixed byte _saveName[128];

    /// <summary>
    /// Alternative save name at offset 0x1A0 (128 bytes) - used when HasLongName = true.
    /// </summary>
    private unsafe fixed byte _longSaveName[128];

    /// <summary>
    /// Slot array at offset 0x220 (8 slots × 32 bytes = 256 bytes).
    /// </summary>
    private unsafe fixed byte _slots[256];

    /// <summary>
    /// Reserved/padding bytes (0x320 - 0x67F = 864 bytes).
    /// </summary>
    private unsafe fixed byte _reserved3[864];

    /// <summary>
    /// Number of save slots at offset 0x680.
    /// </summary>
    public uint SlotCount;

    /// <summary>
    /// Unknown metadata at offset 0x688.
    /// </summary>
    public uint Unknown688;

    /// <summary>
    /// Unknown metadata at offset 0x68C.
    /// </summary>
    public uint Unknown68C;

    /// <summary>
    /// Unknown metadata at offset 0x690.
    /// </summary>
    public uint Unknown690;

    /// <summary>
    /// Unknown metadata at offset 0x694.
    /// </summary>
    public uint Unknown694;

    /// <summary>
    /// Reserved/padding (0x698 - 0x698 = 1 byte).
    /// </summary>
    private byte _reserved4;

    /// <summary>
    /// Flag for long save name at offset 0x699.
    /// 0 = use SaveName (offset 0x120), non-zero = use LongSaveName (offset 0x1A0).
    /// </summary>
    public byte HasLongName;

    /// <summary>
    /// Reserved/padding to align struct (0x69A - 0x103F).
    /// </summary>
    private unsafe fixed byte _reserved5[2470];

    /// <summary>
    /// Maximum number of slots per container.
    /// </summary>
    public const int MaxSlots = 8;

    /// <summary>
    /// Size of each slot entry in bytes.
    /// </summary>
    public const int SlotSize = 32;

    /// <summary>
    /// Total size of container in bytes.
    /// </summary>
    public const int ContainerSize = 0x1040;

    /// <summary>
    /// Gets the save name string.
    /// </summary>
    public readonly unsafe string GetSaveName()
    {
        if (HasLongName == 0)
        {
            fixed (byte* namePtr = _saveName)
            {
                int length = 0;
                while (length < 128 && namePtr[length] != 0)
                    length++;

                return System.Text.Encoding.UTF8.GetString(namePtr, length);
            }
        }
        else
        {
            fixed (byte* namePtr = _longSaveName)
            {
                int length = 0;
                while (length < 128 && namePtr[length] != 0)
                    length++;

                return System.Text.Encoding.UTF8.GetString(namePtr, length);
            }
        }
    }

    /// <summary>
    /// Gets a save slot entry by index.
    /// </summary>
    public readonly unsafe SaveSlotEntry GetSlot(int index)
    {
        if (index < 0 || index >= MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(index));

        fixed (byte* slotsPtr = _slots)
        {
            var slotSpan = new ReadOnlySpan<byte>(slotsPtr + (index * SlotSize), SlotSize);
            return SaveSlotEntry.Read(slotSpan);
        }
    }

    /// <summary>
    /// Gets all slot entries.
    /// </summary>
    public readonly SaveSlotEntry[] GetAllSlots()
    {
        var slots = new SaveSlotEntry[SlotCount];
        for (int i = 0; i < SlotCount && i < MaxSlots; i++)
        {
            slots[i] = GetSlot(i);
        }
        return slots;
    }

    /// <summary>
    /// Finds slots matching a specific identifier (e.g., "AUTOSAVE", "SYSTEM").
    /// </summary>
    public readonly List<(int Index, SaveSlotEntry Slot)> FindSlotsByIdentifier(string identifier)
    {
        var identifierBytes = System.Text.Encoding.UTF8.GetBytes(identifier);
        var results = new List<(int Index, SaveSlotEntry Slot)>();

        for (int i = 0; i < SlotCount && i < MaxSlots; i++)
        {
            var slot = GetSlot(i);
            if (slot.MatchesIdentifier(identifierBytes))
            {
                results.Add((i, slot));
            }
        }

        return results;
    }

    /// <summary>
    /// Gets the save type from slot pointer data.
    /// </summary>
    /// <remarks>
    /// Based on FUN_14151d4a0 logic (line 3724771).
    /// Reads the first byte at slot pointer address.
    /// </remarks>
    public readonly SaveType GetSaveType()
    {
        // Note: In real implementation, would need to dereference SlotPointer
        // For now, return based on flags or slot analysis
        var autoSaves = FindSlotsByIdentifier("AUTOSAVE");
        if (autoSaves.Count > 0) return SaveType.AutoSave;

        var headerSaves = FindSlotsByIdentifier("HEADERSAVE");
        if (headerSaves.Count > 0) return SaveType.Header;

        var systemSaves = FindSlotsByIdentifier("SYSTEM");
        if (systemSaves.Count > 0) return SaveType.System;

        return SaveType.Regular;
    }
}
