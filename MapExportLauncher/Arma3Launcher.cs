using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MapExportLauncher;

[SupportedOSPlatform("windows")]
public class Arma3Launcher
{
    private readonly LauncherConfig _config;
    private readonly string _worldName;
    private readonly List<string> _workshopMods;
    private readonly string _workspace;

    public Arma3Launcher(LauncherConfig config, string worldName, List<string> workshopMods)
    {
        _config = config;
        _worldName = worldName;
        _workshopMods = workshopMods;
        _workspace = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Arma3MapExporter", "launcher-workspace");
    }

    public async Task<string?> RunAsync()
    {
        SetupWorkspace();

        var arma3Path = GetArma3Path();
        var workshopPath = GetArma3WorkshopPath(arma3Path);
        var localModPath = ResolveLocalModPath(workshopPath);

        Console.WriteLine($"Arma 3 path: {arma3Path}");
        Console.WriteLine($"Workshop path: {workshopPath}");
        Console.WriteLine($"Mod path: {localModPath}");

        var process = StartArma3(arma3Path, workshopPath, localModPath);

        // Start tailing the RPT log in the background for progress visibility
        await Task.Delay(5000); // wait for Arma to create the .rpt file
        var rptPath = FindRptFile();
        var cts = new CancellationTokenSource();
        if (rptPath != null)
        {
            _ = Task.Run(() => TailRptAsync(rptPath, cts.Token));
        }

        Console.WriteLine("Arma 3 is running... (this may take a while for large maps)");
        await process.WaitForExitAsync();
        cts.Cancel();

        Console.WriteLine($"Arma 3 exited with code {process.ExitCode}.");

        var zipPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Arma3MapExporter", "maps", _worldName.ToLowerInvariant() + ".zip");

        return File.Exists(zipPath) ? zipPath : null;
    }

    // ── Workspace setup ───────────────────────────────────────────────────────

    private void SetupWorkspace()
    {
        Directory.CreateDirectory(_workspace);

        // Mission folder: the suffix after the dot determines the world
        var missionFolder = Path.Combine(_workspace, $"a3meExport.{_worldName}");
        Directory.CreateDirectory(missionFolder);

        File.WriteAllText(Path.Combine(missionFolder, "init.sqf"),    GetEmbedded("init.sqf"));
        File.WriteAllText(Path.Combine(missionFolder, "mission.sqm"), GetEmbedded("mission.sqm"));

        // User profile (for map mode and graphics preferences)
        var profileDir = Path.Combine(_workspace, "Users", "a3me");
        Directory.CreateDirectory(profileDir);
        File.WriteAllText(Path.Combine(profileDir, "a3me.Arma3Profile"), GetEmbedded("a3me.Arma3Profile"));

        // Video / performance config
        File.WriteAllText(Path.Combine(_workspace, "arma3.cfg"), GetEmbedded("arma3.cfg"));

        // Autotest tells Arma which mission to load and auto-closes when done
        File.WriteAllText(Path.Combine(_workspace, "autotest.cfg"), BuildAutotestCfg(missionFolder));

        Console.WriteLine($"Workspace prepared: {_workspace}");
    }

    private static string BuildAutotestCfg(string missionFolder)
    {
        // Arma 3 config strings use "" to escape a quote; backslashes are literal.
        return @$"class TestMissions
{{
    class TestCase01
    {{
        campaign = """";
        mission = ""{missionFolder}"";
    }};
}};";
    }

    // ── Process management ────────────────────────────────────────────────────

    private Process StartArma3(string arma3Path, string workshopPath, string localModPath)
    {
        var mods = _config.BaseWorkshopMods
            .Concat(_workshopMods)
            .Select(id => Path.Combine(workshopPath, id))
            .Where(Directory.Exists)
            .Append(localModPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var modsArg = string.Join(";", mods);

        var arguments = string.Join(" ", [
            "-window",
            "-noSplash",
            "-noPause",
            $"-profiles=\"{_workspace}\"",
            $"-cfg=\"{_workspace}\\arma3.cfg\"",
            "-name=a3me",
            $"-autotest=\"{_workspace}\\autotest.cfg\"",
            $"\"-mod={modsArg}\""
        ]);

        Console.WriteLine($"Launching: Arma3_x64.exe {arguments}");

        var process = Process.Start(new ProcessStartInfo
        {
            UseShellExecute = false,
            FileName = Path.Combine(arma3Path, "Arma3_x64.exe"),
            WorkingDirectory = arma3Path,
            Arguments = arguments
        }) ?? throw new InvalidOperationException("Failed to start Arma 3.");

        return process;
    }

    // ── RPT tailing ───────────────────────────────────────────────────────────

    private string? FindRptFile()
    {
        // With -profiles=<workspace>, the RPT is written inside <workspace>
        return Directory.GetFiles(_workspace, "*.rpt", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static async Task TailRptAsync(string rptPath, CancellationToken ct)
    {
        try
        {
            using var fs = new FileStream(rptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var lastPos = 0L;

            while (!ct.IsCancellationRequested)
            {
                if (fs.Length != lastPos)
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync(ct)) != null)
                    {
                        // Only forward Arma log lines that are relevant to the exporter
                        if (line.Contains("a3me") || line.Contains("MapExport") || line.Contains("Error"))
                        {
                            Console.WriteLine($"[RPT] {line}");
                        }
                    }
                    lastPos = fs.Position;
                }
                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[RPT watcher] {ex.Message}");
        }
    }

    // ── Path resolution ───────────────────────────────────────────────────────

    private string ResolveLocalModPath(string workshopPath)
    {
        if (!string.IsNullOrEmpty(_config.LocalModPath) && Directory.Exists(_config.LocalModPath))
        {
            return _config.LocalModPath;
        }

        // Try to find @arma3MapExporter relative to the launcher executable
        var launcherDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(launcherDir, @"@arma3MapExporter\.hemttout\build"),
            Path.GetFullPath(Path.Combine(launcherDir, @"..\..\..\..\@arma3MapExporter\.hemttout\build")),
            Path.GetFullPath(Path.Combine(launcherDir, @"..\..\..\..\..\@arma3MapExporter\.hemttout\build")),
        };

        var found = candidates.FirstOrDefault(Directory.Exists);
        if (found != null)
        {
            return found;
        }

        var modWorkshopPath = Path.Combine(workshopPath, "3243017194");
        if (Directory.Exists(modWorkshopPath))
        {
            return modWorkshopPath;
        }

        throw new InvalidOperationException(
            "Cannot locate @arma3MapExporter. " +
            "Set LocalModPath in launcher-config.json or place @arma3MapExporter next to the launcher exe.");
    }

    private static string GetArma3Path()
    {
        var path =
            ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\bohemia interactive\arma 3", "main") ??
            ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\bohemia interactive\arma 3", "main") ??
            GetSteamAppDir(107410);

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            throw new InvalidOperationException(
                "Arma 3 installation not found. " +
                "Make sure Arma 3 is installed via Steam.");
        }
        return path;
    }

    private static string GetArma3WorkshopPath(string arma3Path)
    {
        var workshop = Path.GetFullPath(Path.Combine(arma3Path, @"..\..\workshop\content\107410"));
        if (!Directory.Exists(workshop))
        {
            throw new InvalidOperationException(
                $"Steam workshop directory not found at: {workshop}");
        }
        return workshop;
    }

    private static string? ReadRegistryString(RegistryKey root, string subKey, string valueName)
    {
        using var key = root.OpenSubKey(subKey);
        var value = key?.GetValue(valueName) as string;
        return !string.IsNullOrEmpty(value) && Directory.Exists(value) ? value : null;
    }

    private static string? GetSteamAppDir(int appId)
    {
        return ReadRegistryString(
            Registry.LocalMachine,
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}",
            "InstallLocation");
    }

    // ── Embedded resources ────────────────────────────────────────────────────

    private static string GetEmbedded(string fileName)
    {
        var resourceName = $"MapExportLauncher.data.{fileName}";
        using var stream = typeof(Arma3Launcher).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        return new StreamReader(stream).ReadToEnd();
    }
}
