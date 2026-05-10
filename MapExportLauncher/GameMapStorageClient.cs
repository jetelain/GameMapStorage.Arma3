using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapExportLauncher;

// Note: Not yet implemented on GameMapStorage side

public class GameMapStorageClient
{
    private readonly LauncherConfig _config;

    static readonly JsonSerializerOptions jsonSerializeOptions = new JsonSerializerOptions() { Converters = { new JsonStringEnumConverter() }, PropertyNameCaseInsensitive = true };

    public GameMapStorageClient(LauncherConfig config)
    {
        _config = config;
    }

    public async Task UploadAsync(string zipPath)
    {
        using var http = new HttpClient();
        http.BaseAddress = new Uri(_config.ApiUrl!.TrimEnd('/'));
        http.Timeout = TimeSpan.FromMinutes(30); // large maps can take a while to process

        // ── Authenticate ──────────────────────────────────────────────────────
        var tokenForm = new MultipartFormDataContent
        {
            { new StringContent(_config.ApiKeyId!.Value.ToString()), "apiKeyId" },
            { new StringContent(_config.ApiKey!),                    "apiKey"   }
        };

        var tokenResponse = await http.PostAsync("/api/v1/tokens", tokenForm);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Authentication failed ({(int)tokenResponse.StatusCode}). " +
                "Check ApiKeyId and ApiKey in launcher-config.json.");
        }

        var token = (await tokenResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(jsonSerializeOptions))?.AccessToken
            ?? throw new InvalidOperationException("Bearer token response did not contain 'access_token'.");

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // ── Upload package ────────────────────────────────────────────────────
        await using var fileStream = File.OpenRead(zipPath);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        var uploadForm = new MultipartFormDataContent
        {
            { fileContent, "package", Path.GetFileName(zipPath) }
        };

        Console.WriteLine($"Uploading {Path.GetFileName(zipPath)} ({new FileInfo(zipPath).Length / 1024 / 1024} MB)...");

        var uploadResponse = await http.PostAsync("/api/v1/layers", uploadForm);

        if (!uploadResponse.IsSuccessStatusCode)
        {
            var body = await uploadResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Upload failed ({(int)uploadResponse.StatusCode}): {body}");
        }
    }
}
