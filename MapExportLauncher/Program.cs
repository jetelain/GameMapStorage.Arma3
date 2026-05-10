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
            worldName = args[++i];
            break;
        case "--mods":
            workshopMods.AddRange(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            break;
        case "--upload":
            doUpload = true;
            break;
        case "--config":
            configPath = args[++i];
            break;
        case "--help":
        case "-h":
            PrintUsage();
            return 0;
    }
}

if (string.IsNullOrEmpty(worldName))
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

// ── Launch Arma 3 and run export ──────────────────────────────────────────────
Console.WriteLine($"Starting automated export for world: {worldName}");

var launcher = new Arma3Launcher(config, worldName, workshopMods);

string? zipPath;
try
{
    zipPath = await launcher.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error during launch: {ex.Message}");
    return 2;
}

if (zipPath == null)
{
    Console.Error.WriteLine("Export did not produce a zip file. Check Arma 3 logs.");
    return 3;
}

Console.WriteLine($"Export complete: {zipPath}");

// ── Upload ────────────────────────────────────────────────────────────────────
if (doUpload)
{
    if (config.ApiUrl == null || !config.ApiKeyId.HasValue || config.ApiKey == null)
    {
        Console.Error.WriteLine("Upload requested but API credentials are not configured.");
        Console.Error.WriteLine($"Edit {configPath} and set ApiUrl, ApiKeyId, and ApiKey.");
        return 4;
    }

    Console.WriteLine($"Uploading to {config.ApiUrl} ...");
    try
    {
        var client = new GameMapStorageClient(config);
        await client.UploadAsync(zipPath);
        Console.WriteLine("Upload complete.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Upload failed: {ex.Message}");
        return 5;
    }
}

return 0;

static void PrintUsage()
{
    Console.WriteLine("Usage: MapExportLauncher --world <worldName> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --world <name>    Arma 3 world name to export (e.g. Altis, Tanoa)");
    Console.WriteLine("  --mods <ids>      Comma-separated Steam Workshop IDs for world mods");
    Console.WriteLine("  --upload          Upload the resulting zip to GameMapStorage after export");
    Console.WriteLine("  --config <path>   Path to launcher-config.json (default: ~/Arma3MapExporter/launcher-config.json)");
    Console.WriteLine("  --help            Show this help");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  MapExportLauncher --world Tanoa --mods 583496184 --upload");
    Console.WriteLine();
    Console.WriteLine("Config file (~\\Arma3MapExporter\\launcher-config.json):");
    Console.WriteLine("  {");
    Console.WriteLine("    \"ApiUrl\": \"https://maps.example.com\",");
    Console.WriteLine("    \"ApiKeyId\": 1,");
    Console.WriteLine("    \"ApiKey\": \"your-secret-key\",");
    Console.WriteLine("    \"LocalModPath\": \"C:\\\\path\\\\to\\\\@arma3MapExporter\",");
    Console.WriteLine("    \"BaseWorkshopMods\": [\"450814997\"]");
    Console.WriteLine("  }");
}
