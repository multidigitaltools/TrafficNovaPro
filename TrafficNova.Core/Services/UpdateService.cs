using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace TrafficNova.Core.Services;

public class UpdateService
{
    private readonly ILogger<UpdateService> _log;
    private readonly HttpClient             _http;

    private const string ReleasesApiUrl  = "https://api.github.com/repos/multidigitaltools/trafficnova/releases/latest";
    private const string ReleasesPageUrl = "https://github.com/multidigitaltools/trafficnova/releases";

    public UpdateService(ILogger<UpdateService> log)
    {
        _log  = log;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "TrafficNova-Updater/1.0");
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GitHubRelease>(ReleasesApiUrl, ct);
            if (release is null) return null;

            var tagName = release.TagName?.TrimStart('v') ?? string.Empty;
            if (!Version.TryParse(tagName, out var remote)) return null;

            var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
            if (remote <= current) return null;

            return new UpdateInfo(remote.ToString(), ReleasesPageUrl, release.Body ?? string.Empty);
        }
        catch (Exception ex)
        {
            _log.LogDebug("Update check failed: {Err}", ex.Message);
            return null;
        }
    }

    private sealed record GitHubRelease(
        [property: System.Text.Json.Serialization.JsonPropertyName("tag_name")] string? TagName,
        [property: System.Text.Json.Serialization.JsonPropertyName("body")]     string? Body
    );
}

public record UpdateInfo(string Version, string DownloadUrl, string ReleaseNotes);
