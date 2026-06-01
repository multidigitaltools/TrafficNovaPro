using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TrafficNova.Core.Helpers;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Engine;

/// <summary>
/// Manages a bounded pool of concurrent browser sessions.
/// Steps 49 (pool), 51 (URL round-robin distribution), 59 (result persistence).
/// </summary>
public class SessionPoolService : ISessionPoolService
{
    private readonly PlaywrightService  _playwright;
    private readonly CookieManager      _cookies;
    private readonly ICampaignService   _campaigns;
    private readonly IProxyService      _proxies;
    private readonly ILoggerFactory     _loggerFactory;
    private readonly RateLimiter        _rateLimiter;
    private readonly DomainThrottle     _domainThrottle;

    private readonly ConcurrentDictionary<int, CancellationTokenSource> _ctsByCampaign = new();
    private readonly ConcurrentDictionary<int, bool> _pausedByCampaign = new();

    public SessionPoolService(
        PlaywrightService playwright,
        CookieManager cookies,
        ICampaignService campaigns,
        IProxyService proxies,
        ILoggerFactory loggerFactory,
        RateLimiter rateLimiter,
        DomainThrottle domainThrottle)
    {
        _playwright     = playwright;
        _cookies        = cookies;
        _campaigns      = campaigns;
        _proxies        = proxies;
        _loggerFactory  = loggerFactory;
        _rateLimiter    = rateLimiter;
        _domainThrottle = domainThrottle;
    }

    /// <summary>
    /// Runs all visits for a campaign asynchronously (caller fire-and-forgets).
    /// </summary>
    public async Task RunCampaignAsync(Campaign campaign, EngineConfig engine)
    {
        var cts = new CancellationTokenSource();
        _ctsByCampaign[campaign.Id] = cts;

        var log = _loggerFactory.CreateLogger<SessionPoolService>();

        try
        {
            var urls = ParseUrls(campaign.TargetUrlsJson);
            if (urls.Length == 0)
            {
                log.LogWarning("Campaign {Id} has no valid URLs, aborting", campaign.Id);
                return;
            }

            var proxyPool = ResolveProxyPool(campaign, log);
            var visits   = BuildVisitConfigs(campaign, urls, engine, proxyPool);
            // Clamp to >= 1: a 0 or negative MaxConcurrentSessions setting would
            // make SemaphoreSlim's constructor throw and abort the whole run.
            var concurrency = Math.Max(1,
                Math.Min(engine.MaxConcurrentSessions, campaign.ThreadCount));
            var throttle = new SemaphoreSlim(concurrency, concurrency);

            var tasks = new List<Task>();

            foreach (var visit in visits)
            {
                if (cts.Token.IsCancellationRequested) break;

                // Phase 10 — honour pause: hold dispatch while paused
                while (_pausedByCampaign.TryGetValue(campaign.Id, out var paused) && paused)
                {
                    if (cts.Token.IsCancellationRequested) break;
                    await Task.Delay(500, cts.Token);
                }
                if (cts.Token.IsCancellationRequested) break;

                await throttle.WaitAsync(cts.Token);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _rateLimiter.AcquireAsync(visit.TargetUrl, ct: cts.Token);
                        using var domainSlot = await _domainThrottle.AcquireAsync(visit.TargetUrl, cts.Token);
                        var sessionLog = _loggerFactory.CreateLogger<BrowserSession>();
                        var session = new BrowserSession(_playwright, _cookies, engine, sessionLog);
                        var result  = await session.VisitAsync(visit, cts.Token);
                        await PersistResultAsync(result, log);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }, cts.Token));
            }

            await Task.WhenAll(tasks);
            log.LogInformation("Campaign {Id} finished all {Count} visits",
                campaign.Id, visits.Count);

            // Mark the campaign finished so it leaves the "Running" state.
            // Without this it stays Running forever and the watchdog later
            // mislabels the completed run as a stall and "force-stops" it.
            var finished = await _campaigns.GetByIdAsync(campaign.Id);
            if (finished is not null && finished.Status == CampaignStatus.Running)
            {
                finished.Status = CampaignStatus.Completed;
                // Persist the final visit counts so the grid shows real
                // progress for the completed campaign after an app restart.
                var st = _campaigns.GetStats(campaign.Id);
                if (st is not null)
                {
                    finished.TotalVisits   = st.TotalVisits;
                    finished.SuccessVisits = st.SuccessVisits;
                }
                await _campaigns.UpdateAsync(finished);
            }
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Campaign {Id} was cancelled", campaign.Id);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Campaign {Id} pool error", campaign.Id);
        }
        finally
        {
            // BUG-042: CancellationTokenSource holds an unmanaged kernel handle —
            // dropping the reference without Dispose leaks one handle per campaign
            // run. (TryRemove → Dispose is safe here; concurrent Cancel callers
            // already accept that the cts may be gone — they no-op on TryGetValue.)
            _ctsByCampaign.TryRemove(campaign.Id, out var removed);
            removed?.Dispose();
            _pausedByCampaign.TryRemove(campaign.Id, out _);
        }
    }

    public void Cancel(int campaignId)
    {
        if (_ctsByCampaign.TryGetValue(campaignId, out var cts))
            cts.Cancel();
    }

    public void SetPaused(int campaignId, bool paused)
        => _pausedByCampaign[campaignId] = paused;

    /// <summary>Phase 10 — execute exactly one visit and return its result.</summary>
    public async Task<SessionResult> RunSingleVisitAsync(
        Campaign campaign, EngineConfig engine, CancellationToken ct = default)
    {
        var log  = _loggerFactory.CreateLogger<SessionPoolService>();
        var urls = ParseUrls(campaign.TargetUrlsJson);
        if (urls.Length == 0)
            throw new InvalidOperationException("Campaign has no valid target URLs.");

        // Build a single visit config (no warm-up, target = 1)
        var probe = new Campaign
        {
            Id = campaign.Id, Name = campaign.Name,
            TargetUrlsJson = JsonConvert.SerializeObject(new[] { urls[0] }),
            ThreadCount = 1, VisitTarget = 1, WarmupVisits = 0,
            DwellMin = campaign.DwellMin, DwellMax = campaign.DwellMax,
            BounceRate = 0.0, // never bounce a test — we want to see the page
            ReferrerMode = campaign.ReferrerMode, CustomReferrer = campaign.CustomReferrer,
            ReferrerKeywords = campaign.ReferrerKeywords,
            UserAgentMode = campaign.UserAgentMode, CustomUserAgent = campaign.CustomUserAgent,
            DeviceType = campaign.DeviceType, WindowSize = campaign.WindowSize,
            BrowserLanguage = campaign.BrowserLanguage, Timezone = campaign.Timezone,
            AcceptCookies = campaign.AcceptCookies, JavaScriptEnabled = campaign.JavaScriptEnabled,
            GeoCountry = campaign.GeoCountry, ResourceBlockMode = campaign.ResourceBlockMode,
            UseProxy = campaign.UseProxy, ProxyGroupTag = campaign.ProxyGroupTag,
            ProxyRotation = campaign.ProxyRotation,
            RecordSessions = false,
        };

        var pool    = ResolveProxyPool(campaign, log);
        var visit   = BuildVisitConfigs(probe, new[] { urls[0] }, engine, pool)[0];
        var session = new BrowserSession(_playwright, _cookies, engine,
            _loggerFactory.CreateLogger<BrowserSession>());
        log.LogInformation("Test-run single visit for campaign {Id} → {Url}",
            campaign.Id, urls[0]);
        return await session.VisitAsync(visit, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string[] ParseUrls(string json)
    {
        try { return JsonConvert.DeserializeObject<string[]>(json) ?? []; }
        catch { return []; }
    }

    // Parses the campaign's custom HTTP headers (a JSON name/value object).
    private static IReadOnlyDictionary<string, string>? ParseHeaders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
        try { return JsonConvert.DeserializeObject<Dictionary<string, string>>(json); }
        catch { return null; }
    }

    // Loads the active proxy pool for a campaign that has UseProxy enabled,
    // pre-ordered so a plain round-robin honours the LeastUsed/LeastFailed
    // strategy. Returns empty when proxies are off or none are available.
    private IReadOnlyList<ProxyEntry> ResolveProxyPool(Campaign campaign, ILogger log)
    {
        if (!campaign.UseProxy) return Array.Empty<ProxyEntry>();

        var pool = _proxies.GetActiveProxies(campaign.ProxyGroupTag);
        if (pool.Count == 0)
        {
            log.LogWarning("Campaign {Id} has UseProxy enabled but no active proxies " +
                "in group '{Group}' — visits will run direct.",
                campaign.Id, campaign.ProxyGroupTag);
            return Array.Empty<ProxyEntry>();
        }

        return campaign.ProxyRotation switch
        {
            RotationStrategy.LeastUsed   => pool.OrderBy(p => p.SuccessCount + p.FailureCount).ToList(),
            RotationStrategy.LeastFailed => pool.OrderBy(p => p.FailureCount).ToList(),
            _                            => pool.ToList(),
        };
    }

    // Picks the proxy for the visit at the given index from the pre-loaded pool.
    private static ProxyEntry? PickProxy(Campaign campaign, IReadOnlyList<ProxyEntry> pool, int index)
    {
        if (pool.Count == 0) return null;
        return campaign.ProxyRotation == RotationStrategy.Random
            ? pool[Random.Shared.Next(pool.Count)]
            : pool[index % pool.Count];   // RoundRobin / LeastUsed / LeastFailed (pool pre-ordered)
    }

    private static List<VisitConfig> BuildVisitConfigs(
        Campaign campaign, string[] urls, EngineConfig engine,
        IReadOnlyList<ProxyEntry> proxyPool)
    {
        var configs = new List<VisitConfig>();
        var customHeaders = ParseHeaders(campaign.CustomHeadersJson);

        // Step 103: prepend warm-up visits to domain root before main visits
        if (campaign.WarmupVisits > 0 && urls.Length > 0)
        {
            try
            {
                var rootUri = new Uri(urls[0]);
                var rootUrl = $"{rootUri.Scheme}://{rootUri.Host}/";
                var (wua, wvw, wvh) = ResolveDevice(campaign);
                for (int w = 0; w < campaign.WarmupVisits; w++)
                {
                    var wCfg = new BrowserConfig(wua, wvw, wvh,
                        campaign.Timezone, campaign.BrowserLanguage,
                        campaign.JavaScriptEnabled, campaign.AcceptCookies,
                        PickProxy(campaign, proxyPool, w), null, engine.HeadlessMode, false, null,
                        campaign.ResourceBlockMode, customHeaders);
                    configs.Add(new VisitConfig(campaign.Id, rootUrl, wCfg,
                        5000, 15000, 0.0)); // short dwell, no bounce
                }
            }
            catch { /* ignore malformed URL */ }
        }

        var target  = campaign.VisitTarget > 0 ? campaign.VisitTarget : urls.Length;

        for (int i = 0; i < target; i++)
        {
            var url = urls[i % urls.Length]; // round-robin (Step 51)
            // Step 96: device type drives UA selection and default viewport
            var (ua, vw, vh) = ResolveDevice(campaign);

            string? host = null;
            try { host = new Uri(url).Host; } catch { }

            var referrer = ReferrerGenerator.Generate(
                campaign.ReferrerMode,
                campaign.CustomReferrer,
                campaign.ReferrerKeywords,
                host);

            var traceDir = campaign.RecordSessions
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TrafficNovaPro", "traces")
                : null;

            // Step 97: apply geo profile if campaign has a country set
            var locale   = campaign.BrowserLanguage;
            var timezone = campaign.Timezone;
            if (!string.IsNullOrEmpty(campaign.GeoCountry))
            {
                var geo  = GeoConfig.Get(campaign.GeoCountry);
                locale   = geo.Locale;
                timezone = geo.Timezone;
            }

            var browserCfg = new BrowserConfig(
                UserAgent         : ua,
                ViewportWidth     : vw,
                ViewportHeight    : vh,
                TimezoneId        : timezone,
                Locale            : locale,
                JavaScriptEnabled : campaign.JavaScriptEnabled,
                AcceptCookies     : campaign.AcceptCookies,
                Proxy             : PickProxy(campaign, proxyPool, i),
                Referrer          : referrer,
                HeadlessOverride  : engine.HeadlessMode,
                RecordTrace       : campaign.RecordSessions,
                TraceDirectory    : traceDir,
                ResourceBlock     : campaign.ResourceBlockMode,
                CustomHeaders     : customHeaders);

            configs.Add(new VisitConfig(
                CampaignId : campaign.Id,
                TargetUrl  : url,
                Browser    : browserCfg,
                DwellMinMs : campaign.DwellMin,
                DwellMaxMs : campaign.DwellMax,
                BounceRate : campaign.BounceRate));
        }

        return configs;
    }

    private static (string ua, int vw, int vh) ResolveDevice(Campaign campaign)
    {
        switch (campaign.DeviceType)
        {
            case DeviceType.Mobile:
            {
                var ua = campaign.UserAgentMode == UserAgentMode.Custom && !string.IsNullOrEmpty(campaign.CustomUserAgent)
                    ? campaign.CustomUserAgent
                    : UserAgentPool.GetRandom(UserAgentMode.Mobile);
                // Random mobile viewport from common sizes
                var sizes = new[] { (375, 812), (390, 844), (414, 896), (360, 780) };
                var s = sizes[Random.Shared.Next(sizes.Length)];
                return (ua, s.Item1, s.Item2);
            }
            case DeviceType.Tablet:
            {
                var ua = campaign.UserAgentMode == UserAgentMode.Custom && !string.IsNullOrEmpty(campaign.CustomUserAgent)
                    ? campaign.CustomUserAgent
                    : UserAgentPool.GetRandom(UserAgentMode.Tablet);
                return (ua, 768, 1024);
            }
            default:
            {
                var ua = campaign.UserAgentMode == UserAgentMode.Custom && !string.IsNullOrEmpty(campaign.CustomUserAgent)
                    ? campaign.CustomUserAgent
                    : UserAgentPool.GetRandom(UserAgentMode.Desktop);
                var (w, h) = ParseWindowSize(campaign.WindowSize);
                return (ua, w, h);
            }
        }
    }

    private static (int w, int h) ParseWindowSize(string size)
    {
        var parts = size.Split('x');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var w) &&
            int.TryParse(parts[1], out var h))
            return (w, h);
        return (1920, 1080);
    }

    private async Task PersistResultAsync(SessionResult r, ILogger log)
    {
        try
        {
            await _campaigns.RecordSessionAsync(new TrafficSession
            {
                CampaignId   = r.CampaignId,
                TargetUrl    = r.TargetUrl,
                UserAgent    = r.UserAgent,
                Referrer     = r.Referrer,
                StartedAt    = r.StartedAt,
                EndedAt      = r.EndedAt,
                Success      = r.Success,
                StatusCode   = r.StatusCode,
                ErrorMessage = r.ErrorMessage,
                DwellMs      = r.DwellMs,
                ProxyId      = r.ProxyId,
                TracePath      = r.TracePath,
                ScreenshotPath = r.ScreenshotPath,
                BlockedRequests = r.BlockedRequests,
            });
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to persist session for campaign {Id}", r.CampaignId);
        }
    }
}
