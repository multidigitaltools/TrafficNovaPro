using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace TrafficNova.Engine;

/// <summary>
/// Stub client for FlareSolverr (https://github.com/FlareSolverr/FlareSolverr).
/// Sends a challenge-solve request and returns cookies + User-Agent.
/// Requires a FlareSolverr instance running at the configured URL (default http://localhost:8191).
/// </summary>
public class FlareSolverrClient
{
    private readonly ILogger<FlareSolverrClient> _log;
    private readonly string _baseUrl;

    public record SolveResult(IReadOnlyList<SolveCookie> Cookies, string UserAgent);
    public record SolveCookie(string Name, string Value, string Domain, string Path);

    public FlareSolverrClient(string baseUrl, ILogger<FlareSolverrClient> log)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _log     = log;
    }

    /// <summary>
    /// Asks FlareSolverr to solve a Cloudflare challenge for the given URL.
    /// Returns cookies and UA on success; throws on failure or timeout.
    /// </summary>
    public async Task<SolveResult> SolveAsync(string url, int maxTimeoutMs = 60_000)
    {
        _log.LogInformation("FlareSolverr: solving challenge for {Url}", url);

        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(maxTimeoutMs + 5000) };

        var payload = new
        {
            cmd     = "request.get",
            url     = url,
            maxTimeout = maxTimeoutMs,
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync($"{_baseUrl}/v1", payload);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FlareSolverr request failed");
            throw new InvalidOperationException($"FlareSolverr unreachable at {_baseUrl}: {ex.Message}", ex);
        }

        var body = await response.Content.ReadFromJsonAsync<FlareSolverrResponse>();

        if (body is null || body.Status != "ok")
        {
            var msg = body?.Message ?? "unknown error";
            _log.LogWarning("FlareSolverr returned status={Status} message={Msg}", body?.Status, msg);
            throw new InvalidOperationException($"FlareSolverr failed: {msg}");
        }

        var cookies = body.Solution?.Cookies?
            .Select(c => new SolveCookie(c.Name ?? "", c.Value ?? "", c.Domain ?? "", c.Path ?? "/"))
            .ToList() ?? new List<SolveCookie>();

        var ua = body.Solution?.UserAgent ?? string.Empty;

        _log.LogInformation("FlareSolverr: solved {Url} ({Count} cookies)", url, cookies.Count);
        return new SolveResult(cookies, ua);
    }

    // ── Internal JSON DTO ──────────────────────────────────────────────

    private class FlareSolverrResponse
    {
        public string? Status  { get; set; }
        public string? Message { get; set; }
        public SolutionDto? Solution { get; set; }
    }

    private class SolutionDto
    {
        public string?          UserAgent { get; set; }
        public List<CookieDto>? Cookies   { get; set; }
    }

    private class CookieDto
    {
        public string? Name   { get; set; }
        public string? Value  { get; set; }
        public string? Domain { get; set; }
        public string? Path   { get; set; }
    }
}
