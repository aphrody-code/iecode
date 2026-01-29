using IECODE.Core.Services;
using System.Diagnostics;

namespace IECODE.CLI.Commands;

/// <summary>
/// Commande launch - Lance le jeu en contournant EAC.
/// </summary>
public static class LaunchCommand
{
    public static void Execute(bool verbose)
    {
        try
        {
            Console.WriteLine("Checking EAC status...");
            var status = EACLauncherService.GetStatus();
            
            if (!status.GameFound)
            {
                Console.Error.WriteLine("Error: Game not found. Please check your Steam installation.");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"Game Folder: {status.GameFolder}");
            Console.WriteLine($"EAC Patched: {(status.IsPatched ? "YES" : "NO")}");

            if (!status.IsPatched)
            {
                Console.WriteLine("EAC is not patched. Attempting to patch...");
                bool patched = EACLauncherService.PatchEACFromEmbeddedResource();
                if (patched)
                {
                    Console.WriteLine("EAC patched successfully.");
                }
                else
                {
                    Console.WriteLine("Warning: Failed to patch EAC automatically. Launching anyway (might fail).");
                }
            }

            Console.WriteLine("Launching nie.exe directly...");
            var result = EACLauncherService.LaunchGameDirectly();

            if (result.Success)
            {
                Console.WriteLine($"Game launched successfully! PID: {result.ProcessId}");
                Console.WriteLine("You can now attach Frida or other tools.");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.ErrorMessage}");
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
}
