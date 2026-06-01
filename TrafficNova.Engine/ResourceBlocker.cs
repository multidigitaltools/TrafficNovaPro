using Microsoft.Playwright;
using TrafficNova.Core.Models;

namespace TrafficNova.Engine;

/// <summary>
/// Phase 10 — aborts heavy sub-resource requests to cut bandwidth/proxy cost
/// and speed up traffic generation. The document itself and (in Media mode)
/// scripts/stylesheets are always allowed so the page still "loads" for analytics.
/// </summary>
public sealed class ResourceBlocker
{
    private int _blocked;
    public int BlockedCount => _blocked;

    private static readonly string[] _trackingHosts =
    [
        "google-analytics.com", "googletagmanager.com", "doubleclick.net",
        "facebook.net", "hotjar.com", "segment.io", "mixpanel.com",
        "fullstory.com", "amplitude.com", "clarity.ms",
    ];

    public async Task InstallAsync(IBrowserContext context, ResourceBlockMode mode)
    {
        if (mode == ResourceBlockMode.None) return;

        await context.RouteAsync("**/*", async route =>
        {
            var type = route.Request.ResourceType;
            var url  = route.Request.Url;

            bool block = type switch
            {
                "image" or "media" or "font" => true,
                "stylesheet" => mode == ResourceBlockMode.Aggressive,
                _ => false,
            };

            // Aggressive also kills common analytics/tracking beacons
            if (!block && mode == ResourceBlockMode.Aggressive)
            {
                foreach (var h in _trackingHosts)
                {
                    if (url.Contains(h, StringComparison.OrdinalIgnoreCase))
                    {
                        block = true;
                        break;
                    }
                }
            }

            if (block)
            {
                Interlocked.Increment(ref _blocked);
                await route.AbortAsync();
            }
            else
            {
                // Fall through to HeaderInjector / JsBlocker route handlers
                await route.FallbackAsync();
            }
        });
    }
}
