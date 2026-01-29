using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace IECODE.CLI.Commands;

/// <summary>
/// Commande trace - Surveillance de processus en temps réel via wtrace.
/// Windows-only: Wraps wtrace (ETW-based process tracer).
/// </summary>
/// <remarks>
/// Requires wtrace installed: choco install wtrace
/// Documentation: https://github.com/lowleveldesign/wtrace
/// </remarks>
[SupportedOSPlatform("windows")]
public static class TraceCommand
{
    private const string WtracePath = "wtrace";
    
    /// <summary>
    /// Handlers wtrace disponibles.
    /// </summary>
    public static readonly string[] AvailableHandlers = 
    [
        "process",   // Process/Thread events (always enabled)
        "file",      // File I/O events
        "registry",  // Registry events (voluminous)
        "rpc",       // RPC events
        "tcp",       // TCP/IP events
        "udp",       // UDP events
        "image"      // Image (module) load/unload events
    ];

    /// <summary>
    /// Check if wtrace is installed.
    /// </summary>
    public static bool IsWtraceInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = WtracePath,
                Arguments = "--help",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(2000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Find nie.exe process.
    /// </summary>
    public static Process? FindGameProcess()
    {
        return Process.GetProcessesByName("nie").FirstOrDefault();
    }

    /// <summary>
    /// Status command - Check wtrace installation and game process.
    /// </summary>
    public static void Status(bool verbose)
    {
        Console.WriteLine("=== IECODE Trace Status ===");
        Console.WriteLine();

        // Check wtrace
        bool wtraceInstalled = IsWtraceInstalled();
        Console.WriteLine($"wtrace: {(wtraceInstalled ? "✓ Installed" : "✗ Not found")}");
        
        if (!wtraceInstalled)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Install wtrace:");
            Console.WriteLine("  choco install wtrace");
            Console.WriteLine("  # or download from: https://github.com/lowleveldesign/wtrace/releases");
            Console.ResetColor();
        }

        Console.WriteLine();

        // Check game process
        var gameProcess = FindGameProcess();
        if (gameProcess != null)
        {
            Console.WriteLine($"nie.exe: ✓ Running (PID: {gameProcess.Id})");
            if (verbose)
            {
                Console.WriteLine($"  Path: {gameProcess.MainModule?.FileName ?? "N/A"}");
                Console.WriteLine($"  Memory: {gameProcess.WorkingSet64 / 1024 / 1024} MB");
                Console.WriteLine($"  Start Time: {gameProcess.StartTime}");
            }
        }
        else
        {
            Console.WriteLine("nie.exe: Not running");
        }

        Console.WriteLine();
        Console.WriteLine("Available handlers:");
        foreach (var handler in AvailableHandlers)
        {
            string desc = handler switch
            {
                "process" => "Process/Thread events (always enabled)",
                "file" => "File I/O events",
                "registry" => "Registry events (voluminous)",
                "rpc" => "RPC events",
                "tcp" => "TCP/IP events",
                "udp" => "UDP events",
                "image" => "Image (module) load/unload",
                _ => ""
            };
            Console.WriteLine($"  {handler,-10} - {desc}");
        }
    }

    /// <summary>
    /// Attach to running nie.exe and trace events.
    /// </summary>
    public static async Task AttachAsync(
        string? handlers,
        string? filter,
        bool children,
        bool json,
        string? output,
        bool verbose)
    {
        if (!EnsureWtrace())
            return;

        var gameProcess = FindGameProcess();
        if (gameProcess == null)
        {
            Console.Error.WriteLine("Error: nie.exe is not running");
            Console.Error.WriteLine("Start the game first, then run this command.");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine($"Attaching to nie.exe (PID: {gameProcess.Id})...");
        Console.WriteLine("Press Ctrl+C to stop tracing.");
        Console.WriteLine();

        await RunWtraceAsync(
            gameProcess.Id.ToString(),
            handlers,
            filter,
            children,
            json,
            output,
            verbose);
    }

    /// <summary>
    /// Trace a specific process by PID.
    /// </summary>
    public static async Task TracePidAsync(
        int pid,
        string? handlers,
        string? filter,
        bool children,
        bool json,
        string? output,
        bool verbose)
    {
        if (!EnsureWtrace())
            return;

        try
        {
            var process = Process.GetProcessById(pid);
            Console.WriteLine($"Tracing process: {process.ProcessName} (PID: {pid})");
        }
        catch
        {
            Console.Error.WriteLine($"Error: Process with PID {pid} not found");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("Press Ctrl+C to stop tracing.");
        Console.WriteLine();

        await RunWtraceAsync(
            pid.ToString(),
            handlers,
            filter,
            children,
            json,
            output,
            verbose);
    }

    /// <summary>
    /// Trace a process by name.
    /// </summary>
    public static async Task TraceNameAsync(
        string processName,
        string? handlers,
        string? filter,
        bool children,
        bool json,
        string? output,
        bool verbose)
    {
        if (!EnsureWtrace())
            return;

        // Remove .exe if present
        processName = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

        var process = Process.GetProcessesByName(processName).FirstOrDefault();
        if (process == null)
        {
            Console.Error.WriteLine($"Error: Process '{processName}' not found");
            Console.Error.WriteLine("Make sure the process is running.");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine($"Tracing process: {process.ProcessName} (PID: {process.Id})");
        Console.WriteLine("Press Ctrl+C to stop tracing.");
        Console.WriteLine();

        await RunWtraceAsync(
            process.Id.ToString(),
            handlers,
            filter,
            children,
            json,
            output,
            verbose);
    }

    /// <summary>
    /// System-wide trace (all processes).
    /// </summary>
    public static async Task SystemAsync(
        string? handlers,
        string? filter,
        bool json,
        string? output,
        bool verbose)
    {
        if (!EnsureWtrace())
            return;

        Console.WriteLine("Starting system-wide trace...");
        Console.WriteLine("⚠️  Warning: This captures events from ALL processes!");
        Console.WriteLine("Press Ctrl+C to stop tracing.");
        Console.WriteLine();

        await RunWtraceAsync(
            null, // No PID = system-wide
            handlers,
            filter,
            false, // No children for system-wide
            json,
            output,
            verbose);
    }

    /// <summary>
    /// Run wtrace with given parameters.
    /// </summary>
    private static async Task RunWtraceAsync(
        string? pid,
        string? handlers,
        string? filter,
        bool children,
        bool json,
        string? output,
        bool verbose)
    {
        var args = new StringBuilder();

        // Handlers
        if (!string.IsNullOrWhiteSpace(handlers))
        {
            args.Append($"--handlers {handlers} ");
        }
        else
        {
            // Default: file, tcp, udp (most useful for game)
            args.Append("--handlers file,tcp,udp ");
        }

        // Children
        if (children)
        {
            args.Append("-c ");
        }

        // JSON output
        if (json)
        {
            args.Append("--output json ");
        }

        // Save to file
        if (!string.IsNullOrWhiteSpace(output))
        {
            args.Append($"--save \"{output}\" ");
        }

        // Verbose
        if (verbose)
        {
            args.Append("-v ");
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(filter))
        {
            // Support multiple filters separated by ;
            foreach (var f in filter.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                args.Append($"-f \"{f}\" ");
            }
        }

        // PID (if specified)
        if (!string.IsNullOrWhiteSpace(pid))
        {
            args.Append(pid);
        }

        if (verbose)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[CMD] wtrace {args}");
            Console.ResetColor();
        }

        var psi = new ProcessStartInfo
        {
            FileName = WtracePath,
            Arguments = args.ToString().Trim(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };

        using var process = new Process { StartInfo = psi };

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            try
            {
                if (!process.HasExited)
                {
                    // Send break signal
                    process.Kill();
                }
            }
            catch { }
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                // Color-code certain events
                if (e.Data.Contains("FileIO/"))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
                else if (e.Data.Contains("TCP") || e.Data.Contains("UDP"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else if (e.Data.Contains("Registry"))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                else if (e.Data.Contains("RPC") || e.Data.Contains("Rpc"))
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                }
                
                Console.WriteLine(e.Data);
                Console.ResetColor();
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(e.Data);
                Console.ResetColor();
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error running wtrace: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Ensure wtrace is installed.
    /// </summary>
    private static bool EnsureWtrace()
    {
        if (IsWtraceInstalled())
            return true;

        Console.Error.WriteLine("Error: wtrace is not installed");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Install with Chocolatey:");
        Console.Error.WriteLine("  choco install wtrace");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Or download from:");
        Console.Error.WriteLine("  https://github.com/lowleveldesign/wtrace/releases");
        
        Environment.ExitCode = 1;
        return false;
    }
}
