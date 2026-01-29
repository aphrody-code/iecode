using IECODE.Core;
using System.Runtime.Versioning;

namespace IECODE.CLI.Commands;

/// <summary>
/// Commande memory - Édition mémoire du processus en cours.
/// Windows-only: Requires P/Invoke for process memory access.
/// </summary>
[SupportedOSPlatform("windows")]
public static class MemoryCommand
{
    public static void Status(bool verbose)
    {
        try
        {
            using var game = new IEVRGame();

            bool running = game.Memory.IsProcessRunning();

            Console.WriteLine($"Game process (nie.exe): {(running ? "Running" : "Not running")}");

            if (running)
            {
                bool attached = game.Memory.Attach();

                if (attached)
                {
                    Console.WriteLine($"Status: Attached");
                    Console.WriteLine($"Module Base: 0x{game.Memory.ModuleBase.ToInt64():X}");
                }
                else
                {
                    Console.WriteLine($"Status: Failed to attach (run as Administrator?)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            Environment.ExitCode = 1;
        }
    }

    public static void Read(string addressStr, string type, int length, bool verbose)
    {
        try
        {
            using var game = new IEVRGame();

            if (!game.Memory.IsProcessRunning())
            {
                Console.Error.WriteLine("Error: Game is not running (nie.exe)");
                Environment.ExitCode = 1;
                return;
            }

            if (!game.Memory.Attach())
            {
                Console.Error.WriteLine("Error: Failed to attach to process. Run as Administrator.");
                Environment.ExitCode = 1;
                return;
            }

            long address = ParseAddress(addressStr);

            switch (type.ToLowerInvariant())
            {
                case "int32":
                case "int":
                    int intValue = game.Memory.ReadInt32(address);
                    Console.WriteLine($"[0x{address:X}] = {intValue} (0x{intValue:X8})");
                    break;

                case "float":
                    float floatValue = game.Memory.ReadFloat(address);
                    Console.WriteLine($"[0x{address:X}] = {floatValue}");
                    break;

                case "bytes":
                    byte[] bytes = game.Memory.ReadBytes(address, length);
                    Console.WriteLine($"[0x{address:X}] = {BitConverter.ToString(bytes).Replace("-", " ")}");
                    break;

                default:
                    Console.Error.WriteLine($"Unknown type: {type}. Use int32, float, or bytes.");
                    Environment.ExitCode = 1;
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            Environment.ExitCode = 1;
        }
    }

    public static void Write(string addressStr, string valueStr, string type, bool verbose)
    {
        try
        {
            using var game = new IEVRGame();

            if (!game.Memory.IsProcessRunning())
            {
                Console.Error.WriteLine("Error: Game is not running (nie.exe)");
                Environment.ExitCode = 1;
                return;
            }

            if (!game.Memory.Attach())
            {
                Console.Error.WriteLine("Error: Failed to attach to process. Run as Administrator.");
                Environment.ExitCode = 1;
                return;
            }

            long address = ParseAddress(addressStr);
            bool success = false;

            switch (type.ToLowerInvariant())
            {
                case "int32":
                case "int":
                    int intValue = int.Parse(valueStr);
                    success = game.Memory.WriteInt32(address, Array.Empty<int>(), intValue);
                    Console.WriteLine($"[0x{address:X}] <- {intValue} : {(success ? "OK" : "FAILED")}");
                    break;

                case "float":
                    float floatValue = float.Parse(valueStr);
                    success = game.Memory.WriteFloat(address, Array.Empty<int>(), floatValue);
                    Console.WriteLine($"[0x{address:X}] <- {floatValue} : {(success ? "OK" : "FAILED")}");
                    break;

                case "nop":
                    int nopCount = int.Parse(valueStr);
                    success = game.Memory.WriteNop(address, nopCount);
                    Console.WriteLine($"[0x{address:X}] <- NOP x{nopCount} : {(success ? "OK" : "FAILED")}");
                    break;

                default:
                    Console.Error.WriteLine($"Unknown type: {type}. Use int32, float, or nop.");
                    Environment.ExitCode = 1;
                    break;
            }

            if (!success)
            {
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            Environment.ExitCode = 1;
        }
    }

    private static long ParseAddress(string addressStr)
    {
        if (addressStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt64(addressStr[2..], 16);
        }
        return Convert.ToInt64(addressStr, 16);
    }
}
