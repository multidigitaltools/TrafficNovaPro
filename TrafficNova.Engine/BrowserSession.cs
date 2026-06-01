using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Models;
using TrafficNova.Engine.Stealth;

namespace TrafficNova.Engine;

/// <summary>
/// Executes a single visit: creates context → stealth → headers → navigate
/// → dwell → record cookies → dispose context.
/// Steps 48, 54 (dialog handler), 55 (retry), 57 (trace), 101 (screenshot on error).
/// </summary>
public class BrowserSession
{
    private readonly PlaywrightService  _playwright;
    private readonly CookieManager      _cookies;
    private readonly EngineConfig       _engine;
    private readonly ILogger<BrowserSession> _log;

    public BrowserSession(
        PlaywrightService playwright,
        CookieManager cookies,
        EngineConfig engine,
        ILogger<BrowserSession> log)
    {
        _playwright = playwright;
        _cookies    = cookies;
        _engine     = engine;
        _log        = log;
    }

    public async Task<SessionResult> VisitAsync(
        VisitConfig visit,
        CancellationToken ct = default)
    {
        var startedAt    = DateTime.UtcNow;
        string?  tracePath      = null;
        string?  screenshotPath = null;
        int?     statusCode     = null;
        string?  errorMsg       = null;
        bool     success        = false;
        int      actualDwell    = 0;
        int      blockedReqs    = 0;

        IBrowser? browser = null;

        // BUG-041: page/context CloseAsync inside the per-attempt finally can throw
        // (TargetClosed, navigation timeout during teardown, …). Without an outer
        // try/finally those throws escape past `browser.CloseAsync()` and leak the
        // whole Chromium process — every failing visit otherwise orphans one.
        try
        {
        for (int attempt = 0; attempt <= _engine.RetryCount; attempt++)
        {
            if (attempt > 0)
            {
                var delay = _engine.RetryDelayMs * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delay, ct);
                _log.LogDebug("Retry {Attempt} for {Url}", attempt, visit.TargetUrl);
            }

            IBrowserContext? context = null;
            IPage?           page    = null;

            try
            {
                browser ??= await _playwright.LaunchBrowserAsync(_engine, visit.Browser.Proxy);

                var ctxOpts = ContextBuilder.Build(visit.Browser);
                context = await browser.NewContextAsync(ctxOpts);

                // Step 57 — optional trace recording
                if (visit.Browser.RecordTrace && !string.IsNullOrEmpty(visit.Browser.TraceDirectory))
                {
                    Directory.CreateDirectory(visit.Browser.TraceDirectory);
                    await context.Tracing.StartAsync(new TracingStartOptions
                    {
                        Screenshots = true,
                        Snapshots   = true,
                        Sources     = false,
                    });
                }

                await HeaderInjector.InstallAsync(context);

                if (!visit.Browser.JavaScriptEnabled)
                    await JsBlocker.InstallAsync(context);

                // Phase 10 — install resource blocker last so it matches first
                // and falls back through the header/JS route chain.
                ResourceBlocker? resourceBlocker = null;
                if (visit.Browser.ResourceBlock != ResourceBlockMode.None)
                {
                    resourceBlocker = new ResourceBlocker();
                    await resourceBlocker.InstallAsync(context, visit.Browser.ResourceBlock);
                }

                if (visit.Browser.AcceptCookies)
                    await _cookies.LoadCookiesAsync(context, visit.CampaignId);

                page = await context.NewPageAsync();

                await StealthModule.ApplyAsync(page, _engine.StealthIntensity);

                page.Dialog += (_, dlg) => _ = dlg.DismissAsync();
                context.Page += (_, popup) => _ = popup.CloseAsync();

                var navOpts = new PageGotoOptions
                {
                    Timeout   = _engine.DefaultTimeoutMs,
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Referer   = visit.Browser.Referrer,
                };

                var response = await page.GotoAsync(visit.TargetUrl, navOpts);
                statusCode   = response?.Status;
                success      = response is not null && response.Ok;

                var dwellStart = DateTime.UtcNow;
                await HumanBehavior.SimulateDwellAsync(
                    page, visit.DwellMinMs, visit.DwellMaxMs, visit.BounceRate, ct);
                actualDwell = (int)(DateTime.UtcNow - dwellStart).TotalMilliseconds;

                if (visit.Browser.AcceptCookies && success)
                    await _cookies.StoreCookiesAsync(context, visit.CampaignId);

                if (resourceBlocker is not null)
                    blockedReqs = resourceBlocker.BlockedCount;

                errorMsg = null;
                break;
            }
            catch (OperationCanceledException)
            {
                errorMsg = "Cancelled";
                break;
            }
            catch (PlaywrightException ex)
            {
                errorMsg = ex.Message;
                _log.LogWarning("Visit failed (attempt {A}): {Err}", attempt + 1, ex.Message);
                // Step 101 — screenshot on last failed attempt
                if (attempt == _engine.RetryCount)
                    screenshotPath = await TakeErrorScreenshotAsync(page);
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                _log.LogWarning("Visit exception (attempt {A}): {Err}", attempt + 1, ex.Message);
                if (attempt == _engine.RetryCount)
                    screenshotPath = await TakeErrorScreenshotAsync(page);
            }
            finally
            {
                if (visit.Browser.RecordTrace && context is not null
                    && !string.IsNullOrEmpty(visit.Browser.TraceDirectory))
                {
                    try
                    {
                        tracePath = Path.Combine(
                            visit.Browser.TraceDirectory,
                            $"trace_{visit.CampaignId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
                        await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
                    }
                    catch { /* non-critical */ }
                }

                if (page    is not null) await page.CloseAsync().ConfigureAwait(false);
                if (context is not null) await context.CloseAsync().ConfigureAwait(false);
            }
        }

        }
        finally
        {
            if (browser is not null)
            {
                try { await browser.CloseAsync(); }
                catch (Exception ex)
                {
                    _log.LogDebug("Browser close threw on cleanup: {Err}", ex.Message);
                }
            }
        }

        return new SessionResult(
            CampaignId     : visit.CampaignId,
            TargetUrl      : visit.TargetUrl,
            Success        : success,
            StatusCode     : statusCode,
            DwellMs        : actualDwell,
            UserAgent      : visit.Browser.UserAgent,
            Referrer       : visit.Browser.Referrer ?? string.Empty,
            ProxyId        : visit.Browser.Proxy?.Id,
            ErrorMessage   : errorMsg,
            TracePath      : tracePath,
            ScreenshotPath : screenshotPath,
            StartedAt      : startedAt,
            EndedAt        : DateTime.UtcNow,
            BlockedRequests: blockedReqs);
    }

    // Step 101 — save error screenshot
    private async Task<string?> TakeErrorScreenshotAsync(IPage? page)
    {
        if (page is null || string.IsNullOrEmpty(_engine.ScreenshotDirectory))
            return null;
        try
        {
            Directory.CreateDirectory(_engine.ScreenshotDirectory);
            var path = Path.Combine(
                _engine.ScreenshotDirectory,
                $"error_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = false });
            _log.LogInformation("Error screenshot: {Path}", path);
            return path;
        }
        catch (Exception ex)
        {
            _log.LogDebug("Could not take error screenshot: {Err}", ex.Message);
            return null;
        }
    }
}
