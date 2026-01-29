using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace IECODE.Core.Services;

/// <summary>
/// Service to temporarily bypass Easy Anti-Cheat by replacing EACLauncher.exe.
/// This allows save editing and memory modifications while the game is running.
/// The original launcher is automatically restored when the service is disposed.
/// </summary>
/// <remarks>
/// Based on An-Average-Developer's Inazuma-Eleven-VR-Save-Editor implementation.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class EACBypassService : IDisposable
{
    private const string STEAM_APP_ID = "2799860";
    private const string GAME_NAME = "INAZUMA ELEVEN Victory Road";
    private const string EAC_LAUNCHER = "EACLauncher.exe";
    private const string EAC_BACKUP = "EACLauncher.exe.bak";
    private const string RESOURCE_NAME = "IECODE.Core.Resources.EACLauncher.zip";

    private string? _gameFolderPath;
    private string? _tempFolderPath;
    private string? _eacLauncherPath;
    private string? _eacLauncherBackupPath;
    private bool _isPatched;
    private bool _disposed;

    /// <summary>
    /// Whether EAC is currently bypassed.
    /// </summary>
    public bool IsPatched => _isPatched;

    /// <summary>
    /// The detected game folder path.
    /// </summary>
    public string? GameFolderPath => _gameFolderPath;

    /// <summary>
    /// Event raised when patch status changes.
    /// </summary>
    public event EventHandler<EACPatchEventArgs>? StatusChanged;

    /// <summary>
    /// Applies the EAC bypass by replacing EACLauncher.exe with a patched version.
    /// </summary>
    /// <param name="gamePath">Optional: explicit game path. If null, auto-detects from Steam.</param>
    /// <returns>True if bypass was applied successfully.</returns>
    public bool ApplyBypass(string? gamePath = null)
    {
        if (_isPatched)
        {
            return true; // Already patched
        }

        try
        {
            // Find game folder
            _gameFolderPath = gamePath ?? FindSteamGameFolder();
            if (string.IsNullOrEmpty(_gameFolderPath) || !Directory.Exists(_gameFolderPath))
            {
                OnStatusChanged(false, "Game folder not found");
                return false;
            }

            // Locate EACLauncher.exe
            _eacLauncherPath = Path.Combine(_gameFolderPath, EAC_LAUNCHER);
            if (!File.Exists(_eacLauncherPath))
            {
                OnStatusChanged(false, "EACLauncher.exe not found");
                return false;
            }

            // Create temp folder for extraction
            _tempFolderPath = Path.Combine(Path.GetTempPath(), $"IECODE_EAC_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempFolderPath);

            // Extract embedded resource
            string zipPath = Path.Combine(_tempFolderPath, "EACLauncher.zip");
            if (!ExtractEmbeddedResource(RESOURCE_NAME, zipPath))
            {
                OnStatusChanged(false, "Failed to extract EAC bypass resource");
                return false;
            }

            // Extract zip contents
            string extractFolder = Path.Combine(_tempFolderPath, "extracted");
            Directory.CreateDirectory(extractFolder);
            ZipFile.ExtractToDirectory(zipPath, extractFolder);

            // Backup original launcher
            _eacLauncherBackupPath = Path.Combine(_gameFolderPath, EAC_BACKUP);
            if (File.Exists(_eacLauncherBackupPath))
            {
                File.Delete(_eacLauncherBackupPath);
            }
            File.Move(_eacLauncherPath, _eacLauncherBackupPath);

            // Copy patched launcher
            string patchedLauncher = Path.Combine(extractFolder, EAC_LAUNCHER);
            if (!File.Exists(patchedLauncher))
            {
                // Restore original on failure
                File.Move(_eacLauncherBackupPath, _eacLauncherPath);
                OnStatusChanged(false, "Patched EACLauncher.exe not found in resource");
                return false;
            }

            File.Copy(patchedLauncher, _eacLauncherPath);
            _isPatched = true;

            OnStatusChanged(true, "EAC bypass applied successfully");
            return true;
        }
        catch (Exception ex)
        {
            RestoreOriginal();
            OnStatusChanged(false, $"Failed to apply bypass: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restores the original EACLauncher.exe.
    /// </summary>
    public void RestoreOriginal()
    {
        if (!_isPatched)
        {
            return;
        }

        try
        {
            // Delete patched launcher
            if (!string.IsNullOrEmpty(_eacLauncherPath) && File.Exists(_eacLauncherPath))
            {
                File.Delete(_eacLauncherPath);
            }

            // Restore backup
            if (!string.IsNullOrEmpty(_eacLauncherBackupPath) &&
                !string.IsNullOrEmpty(_eacLauncherPath) &&
                File.Exists(_eacLauncherBackupPath))
            {
                File.Move(_eacLauncherBackupPath, _eacLauncherPath);
            }

            // Cleanup temp folder
            if (!string.IsNullOrEmpty(_tempFolderPath) && Directory.Exists(_tempFolderPath))
            {
                Directory.Delete(_tempFolderPath, true);
            }

            _isPatched = false;
            OnStatusChanged(false, "Original EACLauncher.exe restored");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore EAC launcher: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds the Steam game installation folder.
    /// </summary>
    private static string? FindSteamGameFolder()
    {
        try
        {
            // Try to get Steam install path from registry
            string? steamPath = null;

            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
            {
                steamPath = key?.GetValue("InstallPath") as string;
            }

            if (string.IsNullOrEmpty(steamPath))
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    steamPath = key?.GetValue("InstallPath") as string;
                }
            }

            if (string.IsNullOrEmpty(steamPath))
            {
                return null;
            }

            // Check default Steam library
            string defaultLibrary = Path.Combine(steamPath, "steamapps", "common", GAME_NAME);
            if (Directory.Exists(defaultLibrary))
            {
                return defaultLibrary;
            }

            // Parse libraryfolders.vdf for additional libraries
            string libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraryFoldersPath))
            {
                string content = File.ReadAllText(libraryFoldersPath);

                // Match "path" entries in VDF format
                var pathRegex = new System.Text.RegularExpressions.Regex(
                    @"""path""\s*""([^""]+)""",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                var matches = pathRegex.Matches(content);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count >= 2)
                    {
                        string libraryPath = match.Groups[1].Value.Replace("\\\\", "\\");
                        string gamePath = Path.Combine(libraryPath, "steamapps", "common", GAME_NAME);

                        if (Directory.Exists(gamePath))
                        {
                            return gamePath;
                        }
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts an embedded resource to a file.
    /// </summary>
    private static bool ExtractEmbeddedResource(string resourceName, string outputPath)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            
            if (stream == null)
            {
                // Try alternative resource names
                var names = assembly.GetManifestResourceNames();
                var match = Array.Find(names, n => n.EndsWith("EACLauncher.zip", StringComparison.OrdinalIgnoreCase));
                
                if (match != null)
                {
                    using Stream? altStream = assembly.GetManifestResourceStream(match);
                    if (altStream != null)
                    {
                        using FileStream fs = File.Create(outputPath);
                        altStream.CopyTo(fs);
                        return true;
                    }
                }
                
                return false;
            }

            using (FileStream fileStream = File.Create(outputPath))
            {
                stream.CopyTo(fileStream);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OnStatusChanged(bool isPatched, string message)
    {
        StatusChanged?.Invoke(this, new EACPatchEventArgs(isPatched, message));
    }

    /// <summary>
    /// Disposes the service and restores the original launcher.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        RestoreOriginal();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }

    ~EACBypassService()
    {
        Dispose();
    }
}

/// <summary>
/// Event args for EAC patch status changes.
/// </summary>
public class EACPatchEventArgs : EventArgs
{
    public bool IsPatched { get; }
    public string Message { get; }

    public EACPatchEventArgs(bool isPatched, string message)
    {
        IsPatched = isPatched;
        Message = message;
    }
}
