using IECODE.Core.Memory;
using System.Runtime.Versioning;

namespace IECODE.CLI.Commands;

/// <summary>
/// Commande technique - Gestion des techniques (Hissatsu) des joueurs.
/// Permet de lire, écrire et assigner des techniques cachées.
/// </summary>
[SupportedOSPlatform("windows")]
public static class TechniqueCommand
{
    /// <summary>
    /// Liste toutes les techniques disponibles.
    /// </summary>
    public static void List()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              TECHNIQUES CACHÉES DISPONIBLES                        ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════════╣");

        var techniques = TechniqueConstants.GetAllTechniques()
            .GroupBy(t => t.Category)
            .OrderBy(g => g.Key);

        foreach (var category in techniques)
        {
            Console.WriteLine($"║ 【{category.Key}】");
            foreach (var (hash, name, _) in category)
            {
                Console.WriteLine($"║   {name,-30} {(uint)hash:X8}");
            }
            Console.WriteLine("║");
        }

        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
    }

    /// <summary>
    /// Lit les techniques d'un joueur.
    /// </summary>
    /// <param name="playerIndex">Index du joueur (0-15)</param>
    /// <param name="verbose">Affiche plus de détails</param>
    public static void Read(int playerIndex, bool verbose)
    {
        try
        {
            using var editor = new TechniqueEditorService();

            if (!editor.Attach(out string error))
            {
                Console.Error.WriteLine($"❌ Erreur: {error}");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"✅ Attaché à nie.exe (Base: 0x{editor.ModuleBase.ToInt64():X})");

            var techniques = editor.ReadPlayerTechniques(playerIndex);
            long playerAddr = editor.GetPlayerAddress(playerIndex);

            Console.WriteLine();
            Console.WriteLine($"╔════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║  TECHNIQUES DU JOUEUR {playerIndex} (Adresse: 0x{playerAddr:X})");
            Console.WriteLine($"╠════════════════════════════════════════════════════════════════════╣");

            for (int i = 0; i < techniques.Length; i++)
            {
                var slot = techniques[i];
                if (slot.IsEmpty)
                {
                    Console.WriteLine($"║  Slot {i}: [VIDE]");
                }
                else
                {
                    Console.WriteLine($"║  Slot {i}: {slot.Name,-25} Nv.{slot.Level,-3} [{slot.Category}]");
                    if (verbose)
                    {
                        Console.WriteLine($"║          Hash: {slot.HashHex}  Flags: 0x{slot.Flags:X4}  Addr: 0x{slot.Address:X}");
                    }
                }
            }

            Console.WriteLine($"╚════════════════════════════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ Erreur: {ex.Message}");
            if (verbose) Console.Error.WriteLine(ex.StackTrace);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Écrit une technique dans un slot de joueur.
    /// </summary>
    /// <param name="playerIndex">Index du joueur (0-15)</param>
    /// <param name="slotIndex">Index du slot (0-4)</param>
    /// <param name="techniqueName">Nom de la technique ou hash en hex</param>
    /// <param name="level">Niveau de la technique (1-99)</param>
    /// <param name="verbose">Affiche plus de détails</param>
    public static void Write(int playerIndex, int slotIndex, string techniqueName, int level, bool verbose)
    {
        try
        {
            using var editor = new TechniqueEditorService();

            if (!editor.Attach(out string error))
            {
                Console.Error.WriteLine($"❌ Erreur: {error}");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"✅ Attaché à nie.exe");

            // Try by name first
            if (editor.AssignTechniqueByName(playerIndex, slotIndex, techniqueName))
            {
                Console.WriteLine($"✨ {techniqueName} assigné au joueur {playerIndex}, slot {slotIndex} (Nv.{level})");
                return;
            }

            // Try parsing as hex hash
            int hash;
            if (techniqueName.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hash = unchecked((int)Convert.ToUInt32(techniqueName[2..], 16));
            }
            else if (int.TryParse(techniqueName, out hash))
            {
                // Already an int
            }
            else
            {
                Console.Error.WriteLine($"❌ Technique '{techniqueName}' non reconnue.");
                Console.Error.WriteLine("   Utilisez 'iecode technique list' pour voir les techniques disponibles.");
                Environment.ExitCode = 1;
                return;
            }

            if (editor.WriteTechnique(playerIndex, slotIndex, hash, (ushort)level))
            {
                string name = TechniqueConstants.GetTechniqueName(hash);
                Console.WriteLine($"✨ {name} (0x{(uint)hash:X8}) assigné au joueur {playerIndex}, slot {slotIndex} (Nv.{level})");
            }
            else
            {
                Console.Error.WriteLine($"❌ Échec de l'écriture de la technique.");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ Erreur: {ex.Message}");
            if (verbose) Console.Error.WriteLine(ex.StackTrace);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Assigne rapidement la technique Ouragan Cyclonique (arashi_tatsumaki).
    /// </summary>
    /// <param name="playerIndex">Index du joueur (0-15)</param>
    /// <param name="slotIndex">Index du slot (0-4, défaut: 0)</param>
    /// <param name="verbose">Affiche plus de détails</param>
    public static void Arashi(int playerIndex, int slotIndex, bool verbose)
    {
        try
        {
            using var editor = new TechniqueEditorService();

            if (!editor.Attach(out string error))
            {
                Console.Error.WriteLine($"❌ Erreur: {error}");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"✅ Attaché à nie.exe");

            if (editor.AssignArashiTatsumaki(playerIndex, slotIndex))
            {
                Console.WriteLine();
                Console.WriteLine("  ╔════════════════════════════════════════╗");
                Console.WriteLine("  ║   🌪️  OURAGAN CYCLONIQUE ACTIVÉ! 🌪️    ║");
                Console.WriteLine("  ║      (arashi_tatsumaki / whs0126)      ║");
                Console.WriteLine("  ╠════════════════════════════════════════╣");
                Console.WriteLine($"  ║   Joueur: {playerIndex,-5}   Slot: {slotIndex,-5}   Nv.99  ║");
                Console.WriteLine("  ╚════════════════════════════════════════╝");
                Console.WriteLine();
                Console.WriteLine("  💡 La technique sera visible dans le menu de formation.");
            }
            else
            {
                Console.Error.WriteLine($"❌ Échec de l'assignation de arashi_tatsumaki.");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ Erreur: {ex.Message}");
            if (verbose) Console.Error.WriteLine(ex.StackTrace);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Efface une technique d'un slot.
    /// </summary>
    /// <param name="playerIndex">Index du joueur (0-15)</param>
    /// <param name="slotIndex">Index du slot (0-4)</param>
    /// <param name="verbose">Affiche plus de détails</param>
    public static void Clear(int playerIndex, int slotIndex, bool verbose)
    {
        try
        {
            using var editor = new TechniqueEditorService();

            if (!editor.Attach(out string error))
            {
                Console.Error.WriteLine($"❌ Erreur: {error}");
                Environment.ExitCode = 1;
                return;
            }

            if (editor.ClearTechnique(playerIndex, slotIndex))
            {
                Console.WriteLine($"✅ Technique effacée: joueur {playerIndex}, slot {slotIndex}");
            }
            else
            {
                Console.Error.WriteLine($"❌ Échec de l'effacement.");
                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ Erreur: {ex.Message}");
            if (verbose) Console.Error.WriteLine(ex.StackTrace);
            Environment.ExitCode = 1;
        }
    }
}
