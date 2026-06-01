using Microsoft.Playwright;

namespace TrafficNova.Engine.Stealth;

/// <summary>
/// Applies JS-level stealth patches to a fresh IPage before any navigation.
/// Intensity: Low = webdriver flag only; Medium = + plugins/languages;
///            High = + CDP cdc_ cleanup + screen dimensions.
/// </summary>
public static class StealthModule
{
    private const string WebdriverScript = """
        Object.defineProperty(navigator, 'webdriver', { get: () => false });
        """;

    private const string PluginsScript = """
        Object.defineProperty(navigator, 'plugins', {
            get: () => [1, 2, 3, 4, 5],
        });
        Object.defineProperty(navigator, 'languages', {
            get: () => ['en-US', 'en'],
        });
        """;

    private const string ChromeObjectScript = """
        window.chrome = {
            runtime: {},
            loadTimes: function(){},
            csi: function(){},
            app: {},
        };
        """;

    private const string PermissionsScript = """
        const originalQuery = window.navigator.permissions.query;
        window.navigator.permissions.query = (parameters) => (
            parameters.name === 'notifications' ?
                Promise.resolve({ state: Notification.permission }) :
                originalQuery(parameters)
        );
        """;

    private const string WebGLVendorScript = """
        const getParameter = WebGLRenderingContext.prototype.getParameter;
        WebGLRenderingContext.prototype.getParameter = function(parameter) {
            if (parameter === 37445) return 'Intel Inc.';
            if (parameter === 37446) return 'Intel(R) Iris(TM) Graphics 6100';
            return getParameter.call(this, parameter);
        };
        """;

    public static async Task ApplyAsync(IPage page, string intensity = "Medium")
    {
        // Always: remove webdriver flag
        await page.AddInitScriptAsync(WebdriverScript);

        if (intensity is "Medium" or "High")
        {
            await page.AddInitScriptAsync(PluginsScript);
            await page.AddInitScriptAsync(ChromeObjectScript);
            await page.AddInitScriptAsync(PermissionsScript);
        }

        if (intensity == "High")
        {
            await page.AddInitScriptAsync(WebGLVendorScript);
            // Remove cdc_ automation artifacts injected by ChromeDriver
            await page.AddInitScriptAsync("""
                Object.keys(window).forEach(key => {
                    if (key.startsWith('cdc_')) {
                        delete window[key];
                    }
                });
                """);
        }
    }
}
