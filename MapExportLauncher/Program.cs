using System.Text.Json;
using MapExportLauncher;

// ── Argument parsing ──────────────────────────────────────────────────────────
string? worldName = null;
List<string> workshopMods = [];
bool doUpload = false;
string configPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "Arma3MapExporter", "launcher-config.json");

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--world":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --world requires an argument.");
                return 1;
            }
            worldName = args[++i];
            break;
        case "--mods":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --mods requires an argument.");
                return 1;
            }
            workshopMods.AddRange(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            break;
        case "--config":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --config requires an argument.");
                return 1;
            }
            configPath = args[++i];
            break;
        case "--upload":
            doUpload = true;
            break;
        case "--help":
        case "-h":
            PrintUsage();
            return 0;
    }
}

if (string.IsNullOrEmpty(worldName) && workshopMods.Count == 0)
{
    PrintUsage();
    return 1;
}

// ── Load configuration ────────────────────────────────────────────────────────
LauncherConfig config = new();
if (File.Exists(configPath))
{
    using var stream = File.OpenRead(configPath);
    config = JsonSerializer.Deserialize<LauncherConfig>(stream,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new LauncherConfig();
    Console.WriteLine($"Config loaded from: {configPath}");
}
else
{
    Console.WriteLine($"No config found at {configPath}, using defaults.");
    Console.WriteLine("Create this file to configure API upload credentials and mod paths.");
}

// ── Determine the list of worlds to export ────────────────────────────────────
List<PboWorldDiscovery.DiscoveredWorld> worldsToExport;
if (!string.IsNullOrEmpty(worldName))
{
    // --world was specified explicitly; Steam ID is unknown in this case, assume to be the first mod if any
    worldsToExport = [new PboWorldDiscovery.DiscoveredWorld(worldName, workshopMods.Count > 0 ? workshopMods[0] : string.Empty)];
}
else
{
    // Discover worlds from PBO files inside the workshop mods
    Console.WriteLine("No --world specified; scanning mod PBOs to discover worlds...");

    var arma3PathForScan = GetArma3PathForScan();
    var workshopPathForScan = arma3PathForScan != null
        ? Path.GetFullPath(Path.Combine(arma3PathForScan, @"..\..\workshop\content\107410"))
        : null;

    var modEntries = workshopMods
        .Select(id => (SteamId: id, Directory: workshopPathForScan != null ? Path.Combine(workshopPathForScan, id) : string.Empty))
        .Where(e => Directory.Exists(e.Directory))
        .ToList();

    worldsToExport = PboWorldDiscovery.DiscoverWorlds(modEntries);

    if (worldsToExport.Count == 0)
    {
        Console.Error.WriteLine("No worlds found in the specified mods. Use --world <name> to specify one explicitly.");
        return 1;
    }

    Console.WriteLine($"Discovered {worldsToExport.Count} world(s): {string.Join(", ", worldsToExport.Select(w => $"{w.WorldName} (Steam ID: {w.SteamId})"))}");
}

var steam = !string.IsNullOrEmpty(config.SteamApiKey) ? new SteamWorkshopClient(config.SteamApiKey, config.BaseWorkshopMods) : null;

// ── Auto-discover mod dependencies via Steam Web API ─────────────────────────
if (steam != null && workshopMods.Count > 0)
{
    Console.WriteLine("Resolving Steam Workshop mod dependencies...");
    var extraIds = new List<string>();
    foreach (var modId in workshopMods.ToList())
    {
        var deps = await steam.GetTransitiveDependenciesAsync(modId);
        foreach (var dep in deps)
        {
            if (!workshopMods.Contains(dep) && !extraIds.Contains(dep))
            {
                extraIds.Add(dep);
            }
        }
    }
    if (extraIds.Count > 0)
    {
        Console.WriteLine($"Auto-adding {extraIds.Count} discovered dependenc{(extraIds.Count == 1 ? "y" : "ies")}: {string.Join(", ", extraIds)}");
        workshopMods.AddRange(extraIds);
    }
    else
    {
        Console.WriteLine("No additional dependencies found.");
    }
}
else if (workshopMods.Count > 0 && string.IsNullOrEmpty(config.SteamApiKey))
{
    Console.WriteLine("Note: set SteamApiKey in config to enable automatic dependency discovery.");
}

// ── Launch Arma 3 and run export for each world ───────────────────────────────

int overallResult = 0;
foreach (var world in worldsToExport)
{
    Console.WriteLine();
    Console.WriteLine($"=== Starting export for world: {world.WorldName} (Steam Workshop ID: {world.SteamId}) ===");

    // ── Build environment variables for the extension ─────────────────────
    var extraEnv = new Dictionary<string, string>();
    if (!string.IsNullOrEmpty(world.SteamId))
    {
        extraEnv["A3ME_WORKSHOP_ID"] = world.SteamId;

        if (steam != null)
        {
            Console.WriteLine($"Fetching mod details from Steam for {world.SteamId}...");
            var (_, author) = await steam.GetModDetailsAsync(world.SteamId);
            if (!string.IsNullOrEmpty(author))
            {
                Console.WriteLine($"Mod author: {author}");
                extraEnv["A3ME_WORKSHOP_AUTHOR"] = author;
            }
        }
    }

    var launcher = new Arma3Launcher(config, world.WorldName, workshopMods, extraEnv);

    string? zipPath;
    try
    {
        zipPath = await launcher.RunAsync();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error during launch for {world.WorldName}: {ex.Message}");
        overallResult = 2;
        continue;
    }

    if (zipPath == null)
    {
        Console.Error.WriteLine($"Export for {world.WorldName} did not produce a zip file. Check Arma 3 logs.");
        overallResult = 3;
        continue;
    }

    Console.WriteLine($"Export complete: {zipPath}");

    // ── Upload ────────────────────────────────────────────────────────────────
    if (doUpload)
    {
        if (config.ApiUrl == null || !config.ApiKeyId.HasValue || config.ApiKey == null)
        {
            Console.Error.WriteLine("Upload requested but API credentials are not configured.");
            Console.Error.WriteLine($"Edit {configPath} and set ApiUrl, ApiKeyId, and ApiKey.");
            return 4;
        }

        Console.WriteLine($"Uploading {world.WorldName} to {config.ApiUrl} ...");
        try
        {
            var client = new GameMapStorageClient(config);
            await client.UploadAsync(zipPath);
            Console.WriteLine($"Upload of {world.WorldName} complete.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Upload of {world.WorldName} failed: {ex.Message}");
            overallResult = 5;
        }
    }
}

return overallResult;

static string? GetArma3PathForScan()
{
    try
    {
        return Arma3Launcher.GetArma3PathStatic();
    }
    catch
    {
        return null;
    }
}

static void PrintUsage()
{
    Console.WriteLine("Usage: MapExportLauncher [--world <worldName>] --mods <ids> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --world <name>    Arma 3 world name to export (e.g. Altis, Tanoa).");
    Console.WriteLine("                    If omitted, all worlds found in the specified mods are exported.");
    Console.WriteLine("  --mods <ids>      Comma-separated Steam Workshop IDs for world mods");
    Console.WriteLine("  --upload          Upload the resulting zip to GameMapStorage after export");
    Console.WriteLine("  --config <path>   Path to launcher-config.json (default: ~/Arma3MapExporter/launcher-config.json)");
    Console.WriteLine("  --help            Show this help");
    Console.WriteLine();
    Console.WriteLine("Example (single world):");
    Console.WriteLine("  MapExportLauncher --world Tanoa --mods 583496184 --upload");
    Console.WriteLine();
    Console.WriteLine("Example (all worlds in a mod, auto-discovered from PBOs):");
    Console.WriteLine("  MapExportLauncher --mods 583496184 --upload");
    Console.WriteLine();
    Console.WriteLine("Config file (~\\Arma3MapExporter\\launcher-config.json):");
    Console.WriteLine("  {");
    Console.WriteLine("    \"ApiUrl\": \"https://maps.example.com\",");
    Console.WriteLine("    \"ApiKeyId\": 1,");
    Console.WriteLine("    \"ApiKey\": \"your-secret-key\",");
    Console.WriteLine("    \"SteamApiKey\": \"your-steam-api-key\",");
    Console.WriteLine("    \"LocalModPath\": \"C:\\\\path\\\\to\\\\@arma3MapExporter\",");
    Console.WriteLine("    \"BaseWorkshopMods\": [\"450814997\"]");
    Console.WriteLine("  }");
    Console.WriteLine();
    Console.WriteLine("Get a Steam API key at: https://steamcommunity.com/dev/apikey");
}
