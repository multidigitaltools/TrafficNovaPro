using Microsoft.Playwright;
using TrafficNova.Core.Models;

namespace TrafficNova.Engine;

/// <summary>Builds BrowserNewContextOptions from a BrowserConfig (Step 46).</summary>
public static class ContextBuilder
{
    public static BrowserNewContextOptions Build(BrowserConfig cfg)
    {
        // Parse viewport e.g. "1920x1080" or "390x844"
        var (w, h) = ParseViewport(cfg.ViewportWidth, cfg.ViewportHeight);

        var opts = new BrowserNewContextOptions
        {
            UserAgent      = cfg.UserAgent,
            ViewportSize   = new ViewportSize { Width = w, Height = h },
            TimezoneId     = cfg.TimezoneId,
            Locale         = cfg.Locale,
            JavaScriptEnabled = cfg.JavaScriptEnabled,
            AcceptDownloads = false,
        };

        var proxy = ProxyRouter.Build(cfg.Proxy);
        if (proxy is not null)
            opts.Proxy = proxy;

        // Extra HTTP headers applied at context level
        var headers = new Dictionary<string, string>
        {
            ["Accept-Language"]             = $"{cfg.Locale},en;q=0.9",
            ["Accept-Encoding"]             = "gzip, deflate, br",
            ["DNT"]                         = "1",
            ["Upgrade-Insecure-Requests"]   = "1",
        };
        // Campaign-defined custom headers (Step 99) override the defaults.
        if (cfg.CustomHeaders is not null)
            foreach (var kv in cfg.CustomHeaders)
                headers[kv.Key] = kv.Value;
        opts.ExtraHTTPHeaders = headers;

        return opts;
    }

    private static (int w, int h) ParseViewport(int w, int h)
        => (w > 0 ? w : 1920, h > 0 ? h : 1080);
}
