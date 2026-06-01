using TrafficNova.Core.Interfaces;

namespace TrafficNova.Core.Models;

public enum CampaignStatus { Idle, Running, Paused, Completed, Scheduled }
public enum ReferrerMode { None, Direct, Google, Bing, Social, Custom }
public enum UserAgentMode { Desktop, Mobile, Tablet, Custom }
public enum DeviceType { Desktop, Mobile, Tablet }

/// <summary>
/// Phase 10 — controls which sub-resources are aborted to save bandwidth/proxy cost
/// and run faster. None = load everything, Media = block images/media/fonts,
/// Aggressive = also block stylesheets and tracking/analytics beacons.
/// </summary>
public enum ResourceBlockMode { None, Media, Aggressive }

public class Campaign
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // JSON-serialized list of target URLs
    public string TargetUrlsJson { get; set; } = "[]";

    public int ThreadCount { get; set; } = 5;
    public int VisitTarget { get; set; } = 100;
    public int DwellMin { get; set; } = 15000;  // ms
    public int DwellMax { get; set; } = 60000;  // ms
    public double BounceRate { get; set; } = 0.3;
    public ReferrerMode ReferrerMode { get; set; } = ReferrerMode.None;
    public string CustomReferrer { get; set; } = string.Empty;
    public string ReferrerKeywords { get; set; } = string.Empty;
    public UserAgentMode UserAgentMode { get; set; } = UserAgentMode.Desktop;
    public string CustomUserAgent { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; } = DeviceType.Desktop;
    public string WindowSize { get; set; } = "1920x1080";
    public string BrowserLanguage { get; set; } = "en-US";
    public string Timezone { get; set; } = "America/New_York";
    public bool AcceptCookies { get; set; } = true;
    public bool JavaScriptEnabled { get; set; } = true;
    // Step 97: optional geo-spoof country code (ISO-3166, empty = no geo override)
    public string GeoCountry { get; set; } = string.Empty;
    public bool UseProxy { get; set; } = false;
    public string ProxyGroupTag { get; set; } = string.Empty;
    public RotationStrategy ProxyRotation { get; set; } = RotationStrategy.RoundRobin;
    // Step 103: warm-up visits to domain root before main campaign URL
    public int WarmupVisits { get; set; } = 0;
    public bool RecordSessions { get; set; } = false;
    // Phase 10: bandwidth / speed optimisation — abort heavy sub-resources
    public ResourceBlockMode ResourceBlockMode { get; set; } = ResourceBlockMode.None;
    public string CustomHeadersJson { get; set; } = "{}";
    public CampaignStatus Status { get; set; } = CampaignStatus.Idle;
    public int TotalVisits { get; set; } = 0;
    public int SuccessVisits { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
    public string? CookiesBlob { get; set; }
}
