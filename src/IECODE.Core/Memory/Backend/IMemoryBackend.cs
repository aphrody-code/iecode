using System;

namespace IECODE.Core.Memory.Backend;

/// <summary>
/// Interface for platform-specific memory operations.
/// Abstracts the differences between Windows (kernel32.dll) and Linux (/proc/mem).
/// </summary>
public interface IMemoryBackend : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the backend is attached to a process.
    /// </summary>
    bool IsAttached { get; }

    /// <summary>
    /// Gets the base address of the main module.
    /// </summary>
    IntPtr ModuleBase { get; }

    /// <summary>
    /// Attaches to the specified process.
    /// </summary>
    /// <param name="processName">Name of the process (without extension).</param>
    /// <param name="errorMessage">Error message if attachment fails.</param>
    /// <returns>True if attached successfully.</returns>
    bool Attach(string processName, out string errorMessage);

    /// <summary>
    /// Detaches from the process.
    /// </summary>
    void Detach();

    /// <summary>
    /// Reads bytes from the attached process memory into a buffer.
    /// </summary>
    /// <param name="address">The absolute address to read from.</param>
    /// <param name="buffer">The buffer to write the read data into.</param>
    /// <returns>The number of bytes read.</returns>
    int Read(IntPtr address, Span<byte> buffer);

    /// <summary>
    /// Writes bytes to the attached process memory from a buffer.
    /// </summary>
    /// <param name="address">The absolute address to write to.</param>
    /// <param name="buffer">The data to write.</param>
    /// <returns>True if written successfully.</returns>
    bool Write(IntPtr address, ReadOnlySpan<byte> buffer);

    /// <summary>
    /// Writes bytes to memory, handling protection changes (VirtualProtect/mprotect).
    /// </summary>
    /// <param name="address">The absolute address to write to.</param>
    /// <param name="buffer">The data to write.</param>
    /// <returns>True if written successfully.</returns>
    bool WriteProtected(IntPtr address, ReadOnlySpan<byte> buffer);

    /// <summary>
    /// Allocates memory in the target process (VirtualAllocEx/mmap).
    /// </summary>
    /// <param name="size">Size to allocate.</param>
    /// <param name="hintAddress">Optional preferred address.</param>
    /// <returns>Address of allocated memory, or IntPtr.Zero.</returns>
    IntPtr AllocateMemory(int size, IntPtr hintAddress = default);

    /// <summary>
    /// Frees allocated memory in the target process.
    /// </summary>
    /// <param name="address">Address to free.</param>
    void FreeMemory(IntPtr address);
}
