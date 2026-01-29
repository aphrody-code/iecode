using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;
using IECODE.Core.Memory;

namespace IECODE.Core.Services;

/// <summary>
/// Service de gestion du bypass Easy Anti-Cheat (EAC).
/// Détecte le dossier Steam, sauvegarde et restaure le lanceur EAC.
/// Compatible NativeAOT.
/// </summary>
/// <remarks>
/// Référence Microsoft Learn:
/// - https://learn.microsoft.com/dotnet/api/microsoft.win32.registry
/// - https://learn.microsoft.com/dotnet/api/system.io.compression.zipfile
/// - https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1416
/// </remarks>
public static class EACLauncherService
{
  #region Constants

  /// <summary>
  /// Clé de registre pour trouver le chemin Steam.
  /// </summary>
  private const string STEAM_REGISTRY_KEY = @"SOFTWARE\WOW6432Node\Valve\Steam";

  /// <summary>
  /// Nom de la valeur contenant le chemin d'installation Steam.
  /// </summary>
  private const string STEAM_INSTALL_PATH_VALUE = "InstallPath";

  /// <summary>
  /// Nom de l'exécutable principal du jeu.
  /// </summary>
  private const string GAME_EXECUTABLE = "nie.exe";

  #endregion

  #region Steam Detection

  /// <summary>
  /// Obtient le dossier des jeux Steam via le Registry Windows ou les chemins par défaut Linux.
  /// </summary>
  /// <returns>Chemin vers steamapps/common ou null si non trouvé</returns>
  public static string? GetSteamGamesFolder()
  {
    if (OperatingSystem.IsWindows())
    {
        try
        {
          using var key = Registry.LocalMachine.OpenSubKey(STEAM_REGISTRY_KEY);

          if (key?.GetValue(STEAM_INSTALL_PATH_VALUE) is string steamPath)
          {
            var gamesFolder = Path.Combine(steamPath, "steamapps", "common");
            return Directory.Exists(gamesFolder) ? gamesFolder : null;
          }
        }
        catch
        {
          // Registry access peut échouer sans droits admin
        }
    }
    else if (OperatingSystem.IsLinux())
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var possiblePaths = new List<string>
        {
            Path.Combine(home, ".steam", "steam", "steamapps", "common"),
            Path.Combine(home, ".local", "share", "Steam", "steamapps", "common")
        };

        // WSL Support: Check default Windows Steam path
        if (Directory.Exists("/mnt/c/Program Files (x86)/Steam/steamapps/common"))
        {
            possiblePaths.Add("/mnt/c/Program Files (x86)/Steam/steamapps/common");
        }
        
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path)) return path;
        }
    }

    return null;
  }

  /// <summary>
  /// Obtient le dossier d'installation du jeu IEVR.
  /// </summary>
  /// <returns>Chemin vers le dossier du jeu ou null si non trouvé</returns>
  public static string? GetGameFolder()
  {
    var steamFolder = GetSteamGamesFolder();
    if (steamFolder == null) return null;

    var gameFolder = Path.Combine(steamFolder, MemoryAddresses.GAME_FOLDER_NAME);

    return Directory.Exists(gameFolder) ? gameFolder : null;
  }

  /// <summary>
  /// Obtient le chemin du lanceur EAC.
  /// </summary>
  public static string? GetEACLauncherPath()
  {
    var gameFolder = GetGameFolder();
    if (gameFolder == null) return null;

    var eacPath = Path.Combine(gameFolder, MemoryAddresses.EAC_LAUNCHER_NAME);
    return File.Exists(eacPath) ? eacPath : null;
  }

  /// <summary>
  /// Obtient le chemin du backup EAC.
  /// </summary>
  public static string? GetEACBackupPath()
  {
    var gameFolder = GetGameFolder();
    if (gameFolder == null) return null;

    return Path.Combine(gameFolder, MemoryAddresses.EAC_BACKUP_NAME);
  }

  #endregion

  #region Backup & Restore

  /// <summary>
  /// Sauvegarde le lanceur EAC original.
  /// </summary>
  /// <returns>True si la sauvegarde réussit ou existe déjà</returns>
  public static bool BackupOriginalLauncher()
  {
    try
    {
      var eacLauncherPath = GetEACLauncherPath();
      var backupPath = GetEACBackupPath();

      if (eacLauncherPath == null || backupPath == null)
      {
        return false;
      }

      // Si le backup existe déjà, ne pas écraser
      if (File.Exists(backupPath))
      {
        return true;
      }

      File.Copy(eacLauncherPath, backupPath);
      return true;
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Restaure le lanceur EAC original depuis le backup.
  /// </summary>
  /// <returns>True si la restauration réussit</returns>
  public static bool RestoreOriginalLauncher()
  {
    try
    {
      var eacLauncherPath = GetEACLauncherPath();
      var backupPath = GetEACBackupPath();

      if (backupPath == null || !File.Exists(backupPath))
      {
        return false;
      }

      var gameFolder = GetGameFolder();
      if (gameFolder == null) return false;

      var targetPath = Path.Combine(gameFolder, MemoryAddresses.EAC_LAUNCHER_NAME);

      File.Copy(backupPath, targetPath, overwrite: true);
      File.Delete(backupPath);
      return true;
    }
    catch
    {
      return false;
    }
  }

  #endregion

  #region EAC Patching

  /// <summary>
  /// Vérifie si EAC est actuellement patché.
  /// </summary>
  /// <returns>True si un backup existe (indique que EAC est patché)</returns>
  public static bool IsPatched()
  {
    var backupPath = GetEACBackupPath();
    return backupPath != null && File.Exists(backupPath);
  }

  /// <summary>
  /// Patche le lanceur EAC avec une version modifiée.
  /// </summary>
  /// <param name="patchedLauncherBytes">Bytes du lanceur patché (optionnel)</param>
  /// <returns>True si le patch réussit</returns>
  /// <remarks>
  /// Si patchedLauncherBytes est null, le patch doit être fourni
  /// via une ressource embarquée ou un fichier externe.
  /// </remarks>
  public static bool PatchEACLauncher(byte[]? patchedLauncherBytes = null)
  {
    try
    {
      var gameFolder = GetGameFolder();
      if (gameFolder == null) return false;

      var eacLauncherPath = Path.Combine(gameFolder, MemoryAddresses.EAC_LAUNCHER_NAME);

      if (!File.Exists(eacLauncherPath))
      {
        return false;
      }

      // Sauvegarder l'original
      if (!BackupOriginalLauncher())
      {
        return false;
      }

      // Si des bytes sont fournis, les utiliser
      if (patchedLauncherBytes != null)
      {
        File.WriteAllBytes(eacLauncherPath, patchedLauncherBytes);
        return true;
      }

      // Sinon, on attend que le patch soit fourni par Electron
      // (via IPC avec le fichier ZIP embarqué)
      return false;
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Patche le lanceur EAC depuis un fichier ZIP.
  /// </summary>
  /// <param name="zipPath">Chemin vers le ZIP contenant le lanceur patché</param>
  /// <returns>True si le patch réussit</returns>
  public static bool PatchEACFromZip(string zipPath)
  {
    try
    {
      if (!File.Exists(zipPath)) return false;

      var gameFolder = GetGameFolder();
      if (gameFolder == null) return false;

      // Sauvegarder l'original
      if (!BackupOriginalLauncher())
      {
        return false;
      }

      // Extraire le ZIP dans un dossier temporaire
      var tempDir = Path.Combine(Path.GetTempPath(), "SaveEditor_EAC_" + Guid.NewGuid().ToString("N")[..8]);
      Directory.CreateDirectory(tempDir);

      try
      {
        ZipFile.ExtractToDirectory(zipPath, tempDir);

        // Trouver le lanceur dans le ZIP
        var patchedLauncher = Path.Combine(tempDir, MemoryAddresses.EAC_LAUNCHER_NAME);
        if (!File.Exists(patchedLauncher))
        {
          // Chercher récursivement
          var files = Directory.GetFiles(tempDir, MemoryAddresses.EAC_LAUNCHER_NAME, SearchOption.AllDirectories);
          if (files.Length == 0) return false;
          patchedLauncher = files[0];
        }

        var targetPath = Path.Combine(gameFolder, MemoryAddresses.EAC_LAUNCHER_NAME);
        File.Copy(patchedLauncher, targetPath, overwrite: true);

        return true;
      }
      finally
      {
        // Nettoyer le dossier temporaire
        try
        {
          Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
          // Ignorer les erreurs de nettoyage
        }
      }
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Patche le lanceur EAC depuis la ressource embarquée EACLauncher.zip.
  /// </summary>
  /// <returns>True si le patch réussit</returns>
  public static bool PatchEACFromEmbeddedResource()
  {
    try
    {
      var gameFolder = GetGameFolder();
      if (gameFolder == null) return false;

      // Sauvegarder l'original d'abord
      if (!BackupOriginalLauncher())
      {
        return false;
      }

      // Créer un dossier temporaire
      var tempDir = Path.Combine(Path.GetTempPath(), "IECODE_EAC_" + Guid.NewGuid().ToString("N")[..8]);
      Directory.CreateDirectory(tempDir);

      try
      {
        // Extraire la ressource embarquée
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var resourceName = Array.Find(resourceNames, n => n.EndsWith("EACLauncher.zip", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
          return false;
        }

        var zipPath = Path.Combine(tempDir, "EACLauncher.zip");
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
          if (stream == null) return false;

          using var fs = File.Create(zipPath);
          stream.CopyTo(fs);
        }

        // Extraire le ZIP
        var extractDir = Path.Combine(tempDir, "extracted");
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        // Trouver le lanceur patché
        var patchedLauncher = Path.Combine(extractDir, MemoryAddresses.EAC_LAUNCHER_NAME);
        if (!File.Exists(patchedLauncher))
        {
          var files = Directory.GetFiles(extractDir, MemoryAddresses.EAC_LAUNCHER_NAME, SearchOption.AllDirectories);
          if (files.Length == 0) return false;
          patchedLauncher = files[0];
        }

        // Remplacer le lanceur
        var targetPath = Path.Combine(gameFolder, MemoryAddresses.EAC_LAUNCHER_NAME);
        File.Copy(patchedLauncher, targetPath, overwrite: true);

        return true;
      }
      finally
      {
        try
        {
          Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
          // Ignorer les erreurs de nettoyage
        }
      }
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Active ou désactive le bypass EAC.
  /// </summary>
  /// <param name="enable">True pour activer le bypass, False pour restaurer l'original</param>
  /// <returns>True si l'opération réussit</returns>
  public static bool SetBypassEnabled(bool enable)
  {
    if (enable)
    {
      if (IsPatched()) return true; // Déjà patché
      return PatchEACFromEmbeddedResource();
    }
    else
    {
      if (!IsPatched()) return true; // Déjà original
      return RestoreOriginalLauncher();
    }
  }

  #endregion

  #region Info

  /// <summary>
  /// Obtient les informations sur l'état actuel d'EAC.
  /// </summary>
  public static EACStatus GetStatus()
  {
    var gameFolder = GetGameFolder();

    return new EACStatus
    {
      GameFound = gameFolder != null,
      GameFolder = gameFolder,
      EACLauncherExists = GetEACLauncherPath() != null,
      IsPatched = IsPatched(),
      BackupExists = GetEACBackupPath() != null && File.Exists(GetEACBackupPath())
    };
  }

  #endregion

  #region Game Launch

  /// <summary>
  /// Obtient le chemin vers l'exécutable principal du jeu (nie.exe).
  /// </summary>
  /// <returns>Chemin vers nie.exe ou null si non trouvé</returns>
  public static string? GetGameExecutablePath()
  {
    var gameFolder = GetGameFolder();
    if (gameFolder == null) return null;

    var exePath = Path.Combine(gameFolder, GAME_EXECUTABLE);
    return File.Exists(exePath) ? exePath : null;
  }

  /// <summary>
  /// Lance le jeu directement en contournant EAC.
  /// </summary>
  /// <returns>Résultat du lancement</returns>
  public static GameLaunchResult LaunchGameDirectly()
  {
    try
    {
      var exePath = GetGameExecutablePath();
      if (exePath == null)
      {
        return new GameLaunchResult
        {
          Success = false,
          ErrorMessage = "nie.exe not found. Is the game installed?"
        };
      }

      var gameFolder = Path.GetDirectoryName(exePath);
      if (gameFolder == null)
      {
        return new GameLaunchResult
        {
          Success = false,
          ErrorMessage = "Could not determine game folder."
        };
      }

      // Vérifier si le jeu est déjà en cours d'exécution
      var existingProcesses = Process.GetProcessesByName("nie");
      if (existingProcesses.Length > 0)
      {
        return new GameLaunchResult
        {
          Success = false,
          ErrorMessage = "Game is already running.",
          ProcessId = existingProcesses[0].Id
        };
      }

      // Lancer le jeu directement (bypass EAC)
      var startInfo = new ProcessStartInfo
      {
        FileName = exePath,
        WorkingDirectory = gameFolder,
        UseShellExecute = true
      };

      var process = Process.Start(startInfo);
      if (process == null)
      {
        return new GameLaunchResult
        {
          Success = false,
          ErrorMessage = "Failed to start process."
        };
      }

      return new GameLaunchResult
      {
        Success = true,
        ProcessId = process.Id,
        ExecutablePath = exePath
      };
    }
    catch (Exception ex)
    {
      return new GameLaunchResult
      {
        Success = false,
        ErrorMessage = $"Launch error: {ex.Message}"
      };
    }
  }

  /// <summary>
  /// Lance le jeu via Steam (avec EAC si non patché).
  /// </summary>
  /// <returns>Résultat du lancement</returns>
  public static GameLaunchResult LaunchGameViaSteam()
  {
    try
    {
      // Lancer via protocole Steam
      var startInfo = new ProcessStartInfo
      {
        FileName = "steam://rungameid/2799860",
        UseShellExecute = true
      };

      Process.Start(startInfo);

      return new GameLaunchResult
      {
        Success = true,
        LaunchedViaSteam = true
      };
    }
    catch (Exception ex)
    {
      return new GameLaunchResult
      {
        Success = false,
        ErrorMessage = $"Steam launch error: {ex.Message}"
      };
    }
  }

  /// <summary>
  /// Vérifie si le jeu est en cours d'exécution.
  /// </summary>
  public static bool IsGameRunning()
  {
    return Process.GetProcessesByName("nie").Length > 0;
  }

  /// <summary>
  /// Termine le processus du jeu.
  /// </summary>
  /// <returns>True si le processus a été terminé</returns>
  public static bool KillGame()
  {
    try
    {
      var processes = Process.GetProcessesByName("nie");
      foreach (var process in processes)
      {
        process.Kill();
        process.WaitForExit(5000);
      }
      return true;
    }
    catch
    {
      return false;
    }
  }

  #endregion
}

/// <summary>
/// Résultat du lancement du jeu.
/// </summary>
public struct GameLaunchResult
{
  /// <summary>
  /// Le lancement a réussi.
  /// </summary>
  public bool Success { get; set; }

  /// <summary>
  /// ID du processus lancé.
  /// </summary>
  public int ProcessId { get; set; }

  /// <summary>
  /// Chemin de l'exécutable lancé.
  /// </summary>
  public string? ExecutablePath { get; set; }

  /// <summary>
  /// Message d'erreur si échec.
  /// </summary>
  public string? ErrorMessage { get; set; }

  /// <summary>
  /// Indique si lancé via Steam.
  /// </summary>
  public bool LaunchedViaSteam { get; set; }
}

/// <summary>
/// Informations sur l'état d'EAC.
/// </summary>
public struct EACStatus
{
  /// <summary>
  /// Le jeu a été trouvé sur le système.
  /// </summary>
  public bool GameFound { get; set; }

  /// <summary>
  /// Chemin du dossier du jeu.
  /// </summary>
  public string? GameFolder { get; set; }

  /// <summary>
  /// Le lanceur EAC existe.
  /// </summary>
  public bool EACLauncherExists { get; set; }

  /// <summary>
  /// EAC est actuellement patché.
  /// </summary>
  public bool IsPatched { get; set; }

  /// <summary>
  /// Un backup du lanceur original existe.
  /// </summary>
  public bool BackupExists { get; set; }
}
