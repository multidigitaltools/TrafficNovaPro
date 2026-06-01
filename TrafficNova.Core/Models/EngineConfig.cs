namespace TrafficNova.Core.Models;

/// <summary>Runtime configuration snapshot passed to the Engine layer.</summary>
public record EngineConfig(
    int    MaxConcurrentSessions,
    int    DefaultTimeoutMs,
    int    RetryCount,
    int    RetryDelayMs,
    string BrowserExecutablePath,
    bool   HeadlessMode,
    string StealthIntensity,
    string ScreenshotDirectory,
    string FlareSolverrUrl = "")
{
    public static EngineConfig FromSettings(AppSettings s) => new(
        MaxConcurrentSessions : s.MaxConcurrentSessions,
        DefaultTimeoutMs      : s.DefaultSessionTimeoutSeconds * 1000,
        RetryCount            : s.RetryCount,
        RetryDelayMs          : s.RetryDelayMs,
        BrowserExecutablePath : s.BrowserExecutablePath,
        HeadlessMode          : s.DefaultHeadlessMode,
        StealthIntensity      : s.StealthIntensity,
        ScreenshotDirectory   : s.ScreenshotDirectory,
        // BUG-067: FlareSolverrUrl was never forwarded to the engine layer
        FlareSolverrUrl       : s.FlareSolverrUrl);
}

/// <summary>All options for launching a single browser context (visit session).</summary>
public record BrowserConfig(
    string  UserAgent,
    int     ViewportWidth,
    int     ViewportHeight,
    string  TimezoneId,
    string  Locale,
    bool    JavaScriptEnabled,
    bool    AcceptCookies,
    ProxyEntry? Proxy,
    string? Referrer,
    bool    HeadlessOverride,
    bool    RecordTrace,
    string? TraceDirectory,
    ResourceBlockMode ResourceBlock = ResourceBlockMode.None,
    IReadOnlyDictionary<string, string>? CustomHeaders = null);

/// <summary>A single visit task queued into SessionPoolService.</summary>
public record VisitConfig(
    int          CampaignId,
    string       TargetUrl,
    BrowserConfig Browser,
    int          DwellMinMs,
    int          DwellMaxMs,
    double       BounceRate);

/// <summary>Result produced by BrowserSession.VisitAsync.</summary>
public record SessionResult(
    int       CampaignId,
    string    TargetUrl,
    bool      Success,
    int?      StatusCode,
    int       DwellMs,
    string    UserAgent,
    string    Referrer,
    int?      ProxyId,
    string?   ErrorMessage,
    string?   TracePath,
    string?   ScreenshotPath,
    DateTime  StartedAt,
    DateTime  EndedAt,
    int       BlockedRequests = 0);
