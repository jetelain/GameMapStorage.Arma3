using BIS.PBO;

namespace MapExportLauncher;

/// <summary>
/// Scans PBO files inside Steam Workshop mod directories to discover Arma 3 world names.
/// A world is identified by the presence of a <c>.wrp</c> file inside a PBO.
/// </summary>
public static class PboWorldDiscovery
{
    /// <summary>
    /// Represents a discovered world together with the Steam Workshop ID of the mod that contains it.
    /// </summary>
    public record DiscoveredWorld(string WorldName, string SteamId);

    /// <summary>
    /// Returns all worlds found in the given workshop mods, each annotated with its Steam Workshop ID.
    /// </summary>
    /// <param name="mods">Pairs of (Steam Workshop ID, absolute path to the mod root folder).</param>
    public static List<DiscoveredWorld> DiscoverWorlds(IEnumerable<(string SteamId, string Directory)> mods)
    {
        var worlds = new List<DiscoveredWorld>();

        foreach (var (steamId, modDir) in mods)
        {
            if (!Directory.Exists(modDir))
            {
                continue;
            }

            foreach (var pboPath in Directory.EnumerateFiles(modDir, "*.pbo", SearchOption.AllDirectories))
            {
                try
                {
                    foreach (var worldName in GetWorldNamesFromPbo(pboPath))
                    {
                        worlds.Add(new DiscoveredWorld(worldName, steamId));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PBO] Warning: could not read {pboPath}: {ex.Message}");
                }
            }
        }

        return worlds;
    }

    /// <summary>
    /// Returns the world names found inside a single PBO file.
    /// The world name is the stem of any <c>.wrp</c> entry (e.g. <c>worlds\Altis\Altis.wrp</c> → <c>Altis</c>).
    /// </summary>
    private static IEnumerable<string> GetWorldNamesFromPbo(string pboPath)
    {
        using var pbo = new PBO(pboPath);

        foreach (var entry in pbo.Files)
        {
            var fileName = entry.FileName;
            if (fileName.EndsWith(".wrp", StringComparison.OrdinalIgnoreCase))
            {
                var worldName = Path.GetFileNameWithoutExtension(fileName);
                if (!string.IsNullOrEmpty(worldName))
                {
                    yield return worldName;
                }
            }
        }
    }
}
