using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace IECODE.Core.Memory.Backend;

/// <summary>
/// Windows implementation of IMemoryBackend using kernel32.dll P/Invoke.
/// Implémentation identique à IEVR-Save-Editor pour l'attachement au processus nie.exe.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsMemoryBackend : IMemoryBackend
{
    #region P/Invoke

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, Span<byte> lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ReadOnlySpan<byte> lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    #endregion

    #region Constants

    private const int PROCESS_ALL_ACCESS = 0x1F0FFF;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;

    #endregion

    private IntPtr _processHandle = IntPtr.Zero;
    private IntPtr _moduleBase = IntPtr.Zero;
    private Process? _targetProcess;
    private bool _isAttached;
    private bool _disposed;

    public bool IsAttached => _isAttached;
    public IntPtr ModuleBase => _moduleBase;

    /// <summary>
    /// Attache au processus - Implémentation identique à IEVR-Save-Editor.
    /// </summary>
    public bool Attach(string processName, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                errorMessage = $"Process '{processName}' not found.";
                return false;
            }

            // EXACTLY like IEVR-Save-Editor: Keep reference to process
            _targetProcess = processes[0];
            _processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, _targetProcess.Id);

            if (_processHandle == IntPtr.Zero)
            {
                errorMessage = $"Failed to open process (Error: {Marshal.GetLastWin32Error()}). Run as Admin.";
                _targetProcess = null;
                return false;
            }

            // EXACTLY like IEVR-Save-Editor: Use MainModule.BaseAddress directly
            _moduleBase = _targetProcess.MainModule?.BaseAddress ?? IntPtr.Zero;

            if (_moduleBase == IntPtr.Zero)
            {
                errorMessage = "Failed to get module base address.";
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
                _targetProcess = null;
                return false;
            }

            _isAttached = true;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _isAttached = false;
            if (_processHandle != IntPtr.Zero)
            {
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
            }
            _targetProcess = null;
            return false;
        }
    }

    public void Detach()
    {
        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
        _isAttached = false;
        _moduleBase = IntPtr.Zero;
        _targetProcess = null;
    }

    public int Read(IntPtr address, Span<byte> buffer)
    {
        if (!_isAttached || buffer.IsEmpty) return 0;
        return ReadProcessMemory(_processHandle, address, buffer, buffer.Length, out int bytesRead) ? bytesRead : 0;
    }

    public bool Write(IntPtr address, ReadOnlySpan<byte> buffer)
    {
        if (!_isAttached || buffer.IsEmpty) return false;
        return WriteProcessMemory(_processHandle, address, buffer, buffer.Length, out _);
    }

    public bool WriteProtected(IntPtr address, ReadOnlySpan<byte> buffer)
    {
        if (!_isAttached || buffer.IsEmpty) return false;
        if (!VirtualProtectEx(_processHandle, address, (uint)buffer.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect))
            return false;

        bool success = WriteProcessMemory(_processHandle, address, buffer, buffer.Length, out _);
        VirtualProtectEx(_processHandle, address, (uint)buffer.Length, oldProtect, out _);
        return success;
    }

    public IntPtr AllocateMemory(int size, IntPtr hintAddress = default)
    {
        if (!_isAttached) return IntPtr.Zero;
        return VirtualAllocEx(_processHandle, hintAddress, (uint)size, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    }

    public void FreeMemory(IntPtr address)
    {
        if (_isAttached && address != IntPtr.Zero)
            VirtualFreeEx(_processHandle, address, 0, MEM_RELEASE);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Detach();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WindowsMemoryBackend() => Dispose();
}
