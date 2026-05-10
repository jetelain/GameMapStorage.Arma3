using System.Net.Http.Json;
using System.Web;

namespace MapExportLauncher;

/// <summary>
/// Queries the Steam Web API to resolve a Workshop mod's transitive dependencies.
/// </summary>
internal sealed class SteamWorkshopClient
{
    private readonly HttpClient _http = new();
    private readonly string _apiKey;
    private readonly Dictionary<string, List<string>> _cache = new();

    // CBA_A3 workshop ID – it reports itself as a dependency of almost everything
    // but it is already in BaseWorkshopMods, so we skip it to avoid duplicates.
    private static readonly HashSet<string> SkippedIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "385314016", // Remove from required
    };

    public SteamWorkshopClient(string apiKey)
    {
        _apiKey = apiKey;
    }

    /// <summary>
    /// Returns a flat, de-duplicated list of all transitive Workshop dependency IDs
    /// for <paramref name="workshopId"/> (the root ID itself is NOT included).
    /// </summary>
    public async Task<List<string>> GetTransitiveDependenciesAsync(string workshopId)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result  = new List<string>();
        await CollectAsync(workshopId, visited, result);
        return result;
    }

    private async Task CollectAsync(string id, HashSet<string> visited, List<string> result)
    {
        if (!visited.Add(id))
        {
            return;
        }

        var directDeps = await GetDirectDependenciesAsync(id);

        foreach (var dep in directDeps)
        {
            if (SkippedIds.Contains(dep))
            {
                continue;
            }

            if (!result.Contains(dep))
            {
                result.Add(dep);
            }

            await CollectAsync(dep, visited, result);
        }
    }

    private async Task<List<string>> GetDirectDependenciesAsync(string workshopId)
    {
        if (_cache.TryGetValue(workshopId, out var cached))
        {
            return cached;
        }

        var deps = new List<string>();

        try
        {
            // Step 1 – get the mod title so we can search QueryFiles (which returns children)
            await Task.Delay(200);
            using var detailsResp = await _http.PostAsync(
                "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/?format=json",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "key",                  _apiKey    },
                    { "itemcount",            "1"        },
                    { "publishedfileids[0]",  workshopId },
                }));

            var details = await detailsResp.Content.ReadFromJsonAsync<SteamApiRoot>();
            var detail  = details?.response?.publishedfiledetails?.FirstOrDefault();

            if (detail == null || string.IsNullOrEmpty(detail.title))
            {
                _cache[workshopId] = deps;
                return deps;
            }

            // Step 2 – QueryFiles returns child relationships
            await Task.Delay(200);
            var query = await _http.GetFromJsonAsync<SteamApiQueryRoot>(
                $"https://api.steampowered.com/IPublishedFileService/QueryFiles/v1/" +
                $"?key={_apiKey}&format=json&query_type=12&page=1&numperpage=100" +
                $"&appid=107410&return_children=1&return_metadata=1" +
                $"&search_text={HttpUtility.UrlEncode(detail.title)}");

            var match = query?.response?.publishedfiledetails?
                .FirstOrDefault(f => string.Equals(f.publishedfileid, workshopId, StringComparison.OrdinalIgnoreCase));

            if (match?.children != null)
            {
                foreach (var child in match.children)
                {
                    deps.Add(child.publishedfileid);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam API] Warning: could not resolve dependencies for {workshopId}: {ex.Message}");
        }

        _cache[workshopId] = deps;
        return deps;
    }

    /// <summary>
    /// Returns the Steam Workshop ID, mod title, and author display name for the given mod.
    /// Author name is resolved by looking up the creator's Steam profile via GetPlayerSummaries.
    /// Returns null values for fields that cannot be retrieved.
    /// </summary>
    public async Task<(string? Title, string? Author)> GetModDetailsAsync(string workshopId)
    {
        try
        {
            await Task.Delay(200);
            using var detailsResp = await _http.PostAsync(
                "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/?format=json",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "key",                  _apiKey    },
                    { "itemcount",            "1"        },
                    { "publishedfileids[0]",  workshopId },
                }));

            var details = await detailsResp.Content.ReadFromJsonAsync<SteamApiRoot>();
            var detail  = details?.response?.publishedfiledetails?.FirstOrDefault();

            if (detail == null)
            {
                return (null, null);
            }

            string? author = null;
            if (!string.IsNullOrEmpty(detail.creator))
            {
                await Task.Delay(200);
                var summaries = await _http.GetFromJsonAsync<SteamApiPlayerSummariesRoot>(
                    $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/" +
                    $"?key={_apiKey}&steamids={detail.creator}");
                author = summaries?.response?.players?.FirstOrDefault()?.personaname;
            }

            return (detail.title, author);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Steam API] Warning: could not retrieve mod details for {workshopId}: {ex.Message}");
            return (null, null);
        }
    }
}
