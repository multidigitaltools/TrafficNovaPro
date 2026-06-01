using Microsoft.Playwright;

namespace TrafficNova.Engine;

/// <summary>
/// Installs a route handler on a context to strip automation headers
/// and ensure realistic browser headers on every request (Step 52).
/// </summary>
public static class HeaderInjector
{
    // Headers added by Playwright/CDP that fingerprint automation
    private static readonly HashSet<string> _automationHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "x-playwright",
        "x-playwright-request",
        "x-devtools",
        "cdn-loop",
    };

    public static async Task InstallAsync(IBrowserContext context)
    {
        await context.RouteAsync("**/*", async route =>
        {
            var headers = new Dictionary<string, string>(
                route.Request.Headers, StringComparer.OrdinalIgnoreCase);

            // Remove automation-identifying headers
            foreach (var key in _automationHeaders)
                headers.Remove(key);

            // Ensure realistic sec-fetch headers
            if (!headers.ContainsKey("sec-fetch-site"))
                headers["sec-fetch-site"] = "none";
            if (!headers.ContainsKey("sec-fetch-mode"))
                headers["sec-fetch-mode"] = "navigate";
            if (!headers.ContainsKey("sec-fetch-user"))
                headers["sec-fetch-user"] = "?1";
            if (!headers.ContainsKey("sec-fetch-dest"))
                headers["sec-fetch-dest"] = "document";

            // BUG-048: ContinueAsync terminates the route chain — JsBlocker and
            // ResourceBlocker never ran. FallbackAsync passes control to the next
            // registered handler (same fix as BUG-025 for JsBlocker, now applied here).
            await route.FallbackAsync(new RouteFallbackOptions { Headers = headers });
        });
    }
}
