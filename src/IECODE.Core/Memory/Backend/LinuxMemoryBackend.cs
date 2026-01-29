using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace IECODE.Core.Memory.Backend;

/// <summary>
/// Linux implementation of IMemoryBackend using /proc/[pid]/mem.
/// Requires ptrace scope 0 or root privileges for writing.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxMemoryBackend : IMemoryBackend
{
    private int _pid;
    private IntPtr _moduleBase = IntPtr.Zero;
    private bool _isAttached;
    private bool _disposed;
    private FileStream? _memStream;

    public bool IsAttached => _isAttached;
    public IntPtr ModuleBase => _moduleBase;

    public bool Attach(string processName, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                // Try case-insensitive search if exact match fails (Linux is case-sensitive)
                var allProcesses = Process.GetProcesses();
                var target = allProcesses.FirstOrDefault(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
                
                if (target == null)
                {
                    errorMessage = $"Process '{processName}' not found.";
                    return false;
                }
                _pid = target.Id;
            }
            else
            {
                _pid = processes[0].Id;
            }

            // Open /proc/[pid]/mem
            string memPath = $"/proc/{_pid}/mem";
            if (!File.Exists(memPath))
            {
                errorMessage = $"Cannot access {memPath}.";
                return false;
            }

            try
            {
                _memStream = new FileStream(memPath, FileMode.Open, FileAccess.ReadWrite);
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage = "Access denied. Try running as root or check ptrace_scope.";
                return false;
            }

            // Find module base from /proc/[pid]/maps
            _moduleBase = FindModuleBase(_pid, processName);
            if (_moduleBase == IntPtr.Zero)
            {
                // Fallback: try to find the first executable mapping
                _moduleBase = FindFirstExecMapping(_pid);
            }

            if (_moduleBase == IntPtr.Zero)
            {
                errorMessage = "Could not determine module base address.";
                Detach();
                return false;
            }

            _isAttached = true;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            Detach();
            return false;
        }
    }

    private IntPtr FindModuleBase(int pid, string processName)
    {
        try
        {
            string mapsPath = $"/proc/{pid}/maps";
            foreach (string line in File.ReadLines(mapsPath))
            {
                // Format: 00400000-00452000 r-xp ... /path/to/executable
                if (line.Contains(processName, StringComparison.OrdinalIgnoreCase) && line.Contains("r-xp"))
                {
                    string addressPart = line.Split('-')[0];
                    return new IntPtr(Convert.ToInt64(addressPart, 16));
                }
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    private IntPtr FindFirstExecMapping(int pid)
    {
        try
        {
            string mapsPath = $"/proc/{pid}/maps";
            foreach (string line in File.ReadLines(mapsPath))
            {
                if (line.Contains("r-xp"))
                {
                    string addressPart = line.Split('-')[0];
                    return new IntPtr(Convert.ToInt64(addressPart, 16));
                }
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    public void Detach()
    {
        _memStream?.Dispose();
        _memStream = null;
        _isAttached = false;
        _moduleBase = IntPtr.Zero;
        _pid = 0;
    }

    public int Read(IntPtr address, Span<byte> buffer)
    {
        if (!_isAttached || _memStream == null) return 0;

        try
        {
            _memStream.Seek(address.ToInt64(), SeekOrigin.Begin);
            return _memStream.Read(buffer);
        }
        catch
        {
            return 0;
        }
    }

    public bool Write(IntPtr address, ReadOnlySpan<byte> buffer)
    {
        if (!_isAttached || _memStream == null) return false;

        try
        {
            _memStream.Seek(address.ToInt64(), SeekOrigin.Begin);
            _memStream.Write(buffer);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool WriteProtected(IntPtr address, ReadOnlySpan<byte> buffer)
    {
        // On Linux /proc/[pid]/mem ignores page protection (usually).
        // So we can just write directly.
        return Write(address, buffer);
    }

    public IntPtr AllocateMemory(int size, IntPtr hintAddress = default)
    {
        // Memory allocation via injection is complex on Linux without ptrace.
        // For now, we return Zero to indicate unsupported.
        // This means "Store Item Multiplier" won't work on Linux.
        return IntPtr.Zero;
    }

    public void FreeMemory(IntPtr address)
    {
        // Not supported
    }

    public void Dispose()
    {
        if (_disposed) return;
        Detach();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~LinuxMemoryBackend() => Dispose();
}
