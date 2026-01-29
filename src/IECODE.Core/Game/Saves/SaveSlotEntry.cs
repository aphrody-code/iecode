using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace IECODE.Core.Game.Saves;

/// <summary>
/// Save slot entry structure (32 bytes each).
/// Discovered via reverse engineering of nie.exe save system.
/// </summary>
/// <remarks>
/// Source: docs/nie-analysis/SAVE_SYSTEM_ANALYSIS.md
/// Size: 0x20 (32 bytes)
/// Each save container can hold up to 8 slots.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
public struct SaveSlotEntry
{
    /// <summary>
    /// Save slot name/identifier (32 bytes).
    /// Examples: "AUTOSAVE", "HEADERSAVE", "SYSTEM"
    /// </summary>
    private unsafe fixed byte _name[32];

    /// <summary>
    /// Gets the slot name as a string.
    /// </summary>
    public readonly unsafe string Name
    {
        get
        {
            fixed (byte* ptr = _name)
            {
                int length = 0;
                while (length < 32 && ptr[length] != 0)
                    length++;
                    
                return System.Text.Encoding.UTF8.GetString(ptr, length);
            }
        }
    }

    /// <summary>
    /// Reads a SaveSlotEntry from binary data.
    /// </summary>
    public static SaveSlotEntry Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 32)
            throw new ArgumentException("Insufficient data for SaveSlotEntry (expected 32 bytes)");

        return MemoryMarshal.Read<SaveSlotEntry>(data);
    }

    /// <summary>
    /// Checks if this slot matches a save type identifier.
    /// </summary>
    /// <param name="identifier">Identifier to check (e.g., "AUTOSAVE", "SYSTEM")</param>
    public readonly unsafe bool MatchesIdentifier(ReadOnlySpan<byte> identifier)
    {
        if (identifier.Length > 32)
            return false;

        fixed (byte* ptr = _name)
        {
            return identifier.SequenceEqual(new ReadOnlySpan<byte>(ptr, identifier.Length));
        }
    }

    /// <summary>
    /// Extracts save ID from slot data at offset +8.
    /// </summary>
    /// <remarks>
    /// Based on FUN_140965678 at line 3724322.
    /// </remarks>
    public readonly unsafe uint GetSaveId()
    {
        fixed (byte* ptr = _name)
        {
            if (ptr[8] == 0)
                return 0;
                
            return BinaryPrimitives.ReadUInt32LittleEndian(
                new ReadOnlySpan<byte>(ptr + 8, 4));
        }
    }
}
