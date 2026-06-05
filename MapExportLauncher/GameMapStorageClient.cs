using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pmad.GameMapStorage.Client;
using Pmad.GameMapStorage.Client.Models;

namespace MapExportLauncher;

public class GameMapStorageClient : IDisposable
{
    private readonly LauncherConfig _config;
    private readonly HttpClient _httpAdmin;
    private readonly HttpClient _httpPublic;

    private readonly GameMapStorageAdminClient _clientAdmin;
    private readonly Pmad.GameMapStorage.Client.GameMapStorageClient _clientPublic;
    private bool _isAuthenticated;

    public GameMapStorageClient(LauncherConfig config)
    {
        _config = config;
        _httpAdmin = new HttpClient
        {
            BaseAddress = new Uri(_config.ApiUrl!.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(5) // large maps can take a while to upload
        };
        _httpPublic = new HttpClient
        {
            BaseAddress = new Uri(_config.ApiUrl!.TrimEnd('/') + "/")
        };

        _clientAdmin = new GameMapStorageAdminClient(_httpAdmin);
        _clientPublic = new Pmad.GameMapStorage.Client.GameMapStorageClient(_httpPublic, new MemoryCache(Options.Create(new MemoryCacheOptions())));
    }

    public void Dispose()
    {
        _httpAdmin.Dispose();
        _httpPublic.Dispose();
    }

    public async Task CreateLayerAsync(string zipPath)
    {
        await EnsureAuthenticated().ConfigureAwait(false);

        await using var fileStream = File.OpenRead(zipPath);

        Console.WriteLine($"Uploading {Path.GetFileName(zipPath)} ({new FileInfo(zipPath).Length / 1024 / 1024} MB)...");

        await _clientAdmin.CreateLayerFromPackageAsync(fileStream, Path.GetFileName(zipPath)).ConfigureAwait(false);
    }

    public async Task UpdateLayerAsync(int layerId, string zipPath)
    {
        await EnsureAuthenticated().ConfigureAwait(false);

        await using var fileStream = File.OpenRead(zipPath);

        Console.WriteLine($"Uploading {Path.GetFileName(zipPath)} ({new FileInfo(zipPath).Length / 1024 / 1024} MB)...");

        await _clientAdmin.UpdateLayerFromPackageAsync(layerId, fileStream, Path.GetFileName(zipPath)).ConfigureAwait(false);
    }

    private async Task EnsureAuthenticated()
    {
        if (!_isAuthenticated)
        {
            await _clientAdmin.AuthenticateAsync(_config.ApiKeyId!.Value, _config.ApiKey!).ConfigureAwait(false);
            _isAuthenticated = true;
        }
    }

    public async Task<GameMapLayerJson?> GetExistingLayerAsync(string name, LayerType layerType)
    {
        var map = await _clientPublic.GetMapAsync("arma3", name).ConfigureAwait(false);
        if (map != null && map.Layers != null)
        {
            return map.Layers.FirstOrDefault(l => l.Type == layerType);
        }
        return null;
    }
}
