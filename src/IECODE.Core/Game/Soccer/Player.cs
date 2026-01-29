using System.Runtime.InteropServices;

namespace IECODE.Core.Game.Soccer
{
    /// <summary>
    /// Represents a player entity in the soccer match.
    /// Size: 0x540 (1344 bytes)
    /// Base Address in Memory: DAT_141fe6850 (Start of array)
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 0x540)]
    public unsafe struct Player
    {
        // TODO: Map fields based on further analysis
        
        [FieldOffset(0x00)]
        public int VTable; // Likely a vtable pointer

        [FieldOffset(0x94)]
        public int StateFlags; // Inferred from update loop checks

        // Placeholder for position (Vector3 is usually 12 bytes)
        // [FieldOffset(0x??)]
        // public Vector3 Position;
    }
}
