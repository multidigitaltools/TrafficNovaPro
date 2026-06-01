using Newtonsoft.Json;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Core.Helpers;

/// <summary>Built-in campaign configuration presets for quick-start.</summary>
public static class CampaignPresets
{
    public record Preset(string Name, string Description, Campaign Template);

    public static IReadOnlyList<Preset> All { get; } = BuildAll();

    private static IReadOnlyList<Preset> BuildAll()
    {
        return
        [
            new Preset(
                "Blog Traffic — Light",
                "Low-volume organic-looking blog visits, desktop browsers, Google referrer.",
                new Campaign
                {
                    Name           = "Blog Traffic — Light",
                    TargetUrlsJson = JsonConvert.SerializeObject(Array.Empty<string>()),
                    ThreadCount    = 3,
                    VisitTarget    = 50,
                    DwellMin       = 30_000,
                    DwellMax       = 120_000,
                    BounceRate     = 0.25,
                    ReferrerMode   = ReferrerMode.Google,
                    UserAgentMode  = UserAgentMode.Desktop,
                    JavaScriptEnabled = true,
                    UseProxy       = false,
                }),

            new Preset(
                "Blog Traffic — Medium",
                "Mid-volume mix of Google + social referrers, desktop + mobile UAs.",
                new Campaign
                {
                    Name           = "Blog Traffic — Medium",
                    TargetUrlsJson = JsonConvert.SerializeObject(Array.Empty<string>()),
                    ThreadCount    = 8,
                    VisitTarget    = 200,
                    DwellMin       = 20_000,
                    DwellMax       = 90_000,
                    BounceRate     = 0.30,
                    ReferrerMode   = ReferrerMode.Social,
                    UserAgentMode  = UserAgentMode.Desktop,
                    JavaScriptEnabled = true,
                    UseProxy       = true,
                    ProxyRotation  = RotationStrategy.LeastUsed,
                }),

            new Preset(
                "E-Commerce — Product Pages",
                "Simulates product browsing with longer dwell, Bing referrer.",
                new Campaign
                {
                    Name           = "E-Commerce — Product Pages",
                    TargetUrlsJson = JsonConvert.SerializeObject(Array.Empty<string>()),
                    ThreadCount    = 5,
                    VisitTarget    = 100,
                    DwellMin       = 45_000,
                    DwellMax       = 180_000,
                    BounceRate     = 0.15,
                    ReferrerMode   = ReferrerMode.Bing,
                    UserAgentMode  = UserAgentMode.Desktop,
                    JavaScriptEnabled = true,
                    UseProxy       = true,
                    ProxyRotation  = RotationStrategy.RoundRobin,
                }),

            new Preset(
                "Mobile Traffic Boost",
                "Mobile UA traffic from social referrers, short dwell to simulate scrollers.",
                new Campaign
                {
                    Name           = "Mobile Traffic Boost",
                    TargetUrlsJson = JsonConvert.SerializeObject(Array.Empty<string>()),
                    ThreadCount    = 6,
                    VisitTarget    = 150,
                    DwellMin       = 10_000,
                    DwellMax       = 45_000,
                    BounceRate     = 0.40,
                    ReferrerMode   = ReferrerMode.Social,
                    UserAgentMode  = UserAgentMode.Mobile,
                    DeviceType     = DeviceType.Mobile,
                    WindowSize     = "390x844",
                    JavaScriptEnabled = true,
                    UseProxy       = true,
                    ProxyRotation  = RotationStrategy.Random,
                }),

            new Preset(
                "High-Volume Stress Test",
                "Maximum threads, no proxy, direct referrer — for load testing only.",
                new Campaign
                {
                    Name           = "High-Volume Stress Test",
                    TargetUrlsJson = JsonConvert.SerializeObject(Array.Empty<string>()),
                    ThreadCount    = 30,
                    VisitTarget    = 1000,
                    DwellMin       = 5_000,
                    DwellMax       = 15_000,
                    BounceRate     = 0.60,
                    ReferrerMode   = ReferrerMode.Direct,
                    UserAgentMode  = UserAgentMode.Desktop,
                    JavaScriptEnabled = false,
                    UseProxy       = false,
                }),
        ];
    }

    /// <summary>Returns a deep copy of the preset template with a fresh ID and timestamps.</summary>
    public static Campaign ApplyPreset(Preset preset, string? customName = null)
    {
        var json = JsonConvert.SerializeObject(preset.Template);
        var copy = JsonConvert.DeserializeObject<Campaign>(json)!;
        copy.Id        = 0;
        copy.Name      = customName ?? preset.Name;
        copy.Status    = CampaignStatus.Idle;
        copy.CreatedAt = copy.UpdatedAt = DateTime.UtcNow;
        return copy;
    }
}
