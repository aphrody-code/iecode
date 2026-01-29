using IECODE.Core.Formats.Level5.CfgBin.Encryption;
using IECODE.Core.Formats.Level5.CfgBin;
using IECODE.Core.Formats.Level5.CfgBin.Logic;

namespace IECODE.Core.GameData;

public class GameDataMapper
{
    private readonly string _dumpPath;
    private readonly string _language;

    public GameDataMapper(string dumpPath, string language = "fr")
    {
        _dumpPath = dumpPath;
        _language = language;
    }

    public List<T> LoadEntries<T>(string relativePath, string entryPrefix, Func<Entry, T> factory, string? filePattern = null) where T : GameDataEntry
    {
        var cfgBin = LoadCfgBin(relativePath, filePattern);
        if (cfgBin == null) 
        {
            Console.WriteLine($"[DEBUG] CfgBin is null for {relativePath}");
            return new List<T>();
        }

        Console.WriteLine($"[DEBUG] Loaded CfgBin with {cfgBin.Entries.Count} root entries");
        foreach (var entry in cfgBin.Entries)
        {
            Console.WriteLine($"  - Root Entry: {entry.Name}");
        }

        var results = new List<T>();
        var entries = FindEntriesByPrefix(cfgBin, entryPrefix);
        
        Console.WriteLine($"[DEBUG] Found {entries.Count} entries with prefix {entryPrefix}");

        foreach (var entry in entries)
        {
            var instance = factory(entry);
            if (instance != null)
            {
                results.Add(instance);
            }
        }

        return results;
    }

    private CfgBin? LoadCfgBin(string relativePath, string? filePattern = null)
    {
        string fullPath = Path.Combine(_dumpPath, relativePath);
        
        if (!File.Exists(fullPath))
        {
            if (Directory.Exists(fullPath))
            {
                var pattern = filePattern ?? "*.cfg.bin";
                var files = Directory.GetFiles(fullPath, pattern);
                
                if (files.Length == 0)
                {
                    // Try looking for .json version if .cfg.bin is missing
                    files = Directory.GetFiles(fullPath, pattern + ".json");
                }

                // Pick the one that contains the prefix or just the longest one if no pattern
                fullPath = files.OrderByDescending(f => f.Length).FirstOrDefault() ?? "";
            }
            else
            {
                // Try adding .cfg.bin
                if (File.Exists(fullPath + ".cfg.bin")) fullPath += ".cfg.bin";
                else if (File.Exists(fullPath + ".cfg.bin.json")) fullPath += ".cfg.bin.json";
            }
        }

        if (!File.Exists(fullPath)) return null;

        var cfgBin = new CfgBin();
        if (fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            cfgBin.ImportJson(fullPath);
        }
        else
        {
            byte[] data = File.ReadAllBytes(fullPath);
            if (!CfgBin.HasValidFooter(data))
            {
                data = CriwareCrypt.Decrypt(data, Path.GetFileName(fullPath));
            }

#pragma warning disable IL2026
            cfgBin.Open(data);
#pragma warning restore IL2026
        }
        
        return cfgBin;
    }

    private List<Entry> FindEntriesByPrefix(CfgBin cfgBin, string prefix)
    {
        var items = new List<Entry>();
        foreach (var entry in cfgBin.Entries)
        {
            FindEntriesRecursive(entry, prefix, items);
        }
        return items;
    }

    private void FindEntriesRecursive(Entry entry, string prefix, List<Entry> items)
    {
        if (entry.Name.StartsWith(prefix))
        {
            items.Add(entry);
        }

        foreach (var child in entry.Children)
        {
            FindEntriesRecursive(child, prefix, items);
        }
    }
}
