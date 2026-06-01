using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Models;

namespace TrafficNova.Engine;

/// <summary>
/// Owns the IPlaywright singleton and creates IBrowser instances.
/// Disposed once on app shutdown — browsers are closed individually per session.
/// </summary>
public sealed class PlaywrightService : IAsyncDisposable
{
    private readonly ILogger<PlaywrightService> _log;
    private IPlaywright? _playwright;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public PlaywrightService(ILogger<PlaywrightService> log) => _log = log;

    /// <summary>Ensures Playwright is initialised (idempotent).</summary>
    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            _playwright   = await Playwright.CreateAsync();
            _initialized  = true;
            _log.LogInformation("Playwright initialised");
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>Launches a Chromium browser with stealth-friendly args.</summary>
    public async Task<IBrowser> LaunchBrowserAsync(EngineConfig config, ProxyEntry? proxy = null)
    {
        await EnsureInitializedAsync();

        var args = new List<string>
        {
            "--disable-blink-features=AutomationControlled",
            "--disable-infobars",
            "--disable-dev-shm-usage",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-background-timer-throttling",
            "--disable-backgrounding-occluded-windows",
            "--disable-renderer-backgrounding",
        };

        var opts = new BrowserTypeLaunchOptions
        {
            Headless = config.HeadlessMode,
            Args     = args,
            SlowMo   = 0,
        };

        if (!string.IsNullOrWhiteSpace(config.BrowserExecutablePath))
            opts.ExecutablePath = config.BrowserExecutablePath;

        // Chromium only honours a per-context proxy if the browser itself was
        // launched with one — set it here so the campaign's proxy is applied.
        if (proxy is not null)
            opts.Proxy = ProxyRouter.Build(proxy);

        var browser = await _playwright!.Chromium.LaunchAsync(opts);
        _log.LogDebug("Browser launched (headless={Headless}, proxy={Proxy})",
            config.HeadlessMode, proxy?.Address ?? "none");
        return browser;
    }

    public async ValueTask DisposeAsync()
    {
        if (_playwright is not null)
        {
            _playwright.Dispose();
            _playwright = null;
            _log.LogInformation("Playwright disposed");
        }
        await ValueTask.CompletedTask;
    }
}
