namespace MapExportLauncher;

public class LauncherConfig
{
    /// <summary>Base URL of your GameMapStorage instance, e.g. "https://maps.example.com"</summary>
    public string? ApiUrl { get; set; }

    /// <summary>API key ID from the GameMapStorage admin panel</summary>
    public int? ApiKeyId { get; set; }

    /// <summary>API key secret from the GameMapStorage admin panel</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Absolute path to your @arma3MapExporter folder.
    /// If null, the launcher tries to detect it automatically (sibling of the exe or in the workspace root).
    /// </summary>
    public string? LocalModPath { get; set; }

    /// <summary>
    /// Steam Workshop IDs of prerequisite mods (always loaded).
    /// Defaults to CBA_A3 (450814997).
    /// </summary>
    public List<string> BaseWorkshopMods { get; set; } = ["450814997"];

    /// <summary>
    /// Steam Web API key used to automatically discover mod dependencies.
    /// Get one at https://steamcommunity.com/dev/apikey
    /// If null, automatic dependency discovery is skipped.
    /// </summary>
    public string? SteamApiKey { get; set; }
}
