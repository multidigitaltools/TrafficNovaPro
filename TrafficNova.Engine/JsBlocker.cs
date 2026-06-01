using Microsoft.Playwright;

namespace TrafficNova.Engine;

/// <summary>
/// When JavaScriptEnabled=false, blocks all script resources via Playwright
/// routing (Step 53). Playwright has no native per-page JS disable API.
/// </summary>
public static class JsBlocker
{
    private static readonly HashSet<string> _scriptTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "other"
    };

    public static async Task InstallAsync(IBrowserContext context)
    {
        await context.RouteAsync("**/*.js", route => route.AbortAsync());
        await context.RouteAsync("**/*.mjs", route => route.AbortAsync());

        // Also block inline script execution via CSP header injection isn't
        // possible in Playwright context-level routing alone. We rely on
        // resource type blocking as the primary mechanism.
        await context.RouteAsync("**/*", async route =>
        {
            if (_scriptTypes.Contains(route.Request.ResourceType))
                await route.AbortAsync();
            else
                // Fall back (not Continue) so the request still reaches the
                // HeaderInjector route handler — Continue would end the chain
                // and silently disable header injection on JS-off campaigns.
                await route.FallbackAsync();
        });
    }
}
