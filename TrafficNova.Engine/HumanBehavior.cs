using Microsoft.Playwright;

namespace TrafficNova.Engine;

/// <summary>Simulates human-like interaction patterns on a loaded page.</summary>
public static class HumanBehavior
{
    // Random.Shared is thread-safe; a plain `new Random()` shared static is not,
    // and this is called from up to ThreadCount parallel visit tasks at once.
    private static readonly Random _rng = Random.Shared;

    /// <summary>
    /// Waits a random dwell time in [minMs, maxMs] while scrolling
    /// the page gradually to simulate a human reader.
    /// </summary>
    public static async Task SimulateDwellAsync(
        IPage page, int minMs, int maxMs, double bounceRate,
        CancellationToken ct = default)
    {
        var totalMs = _rng.Next(minMs, maxMs + 1);

        // Bounce: just wait without scrolling
        if (_rng.NextDouble() < bounceRate)
        {
            await Task.Delay(Math.Min(totalMs, 3000), ct);
            return;
        }

        // Non-bounce: scroll in stages, pausing between each
        var stages    = _rng.Next(3, 8);
        var perStage  = totalMs / stages;

        for (int i = 0; i < stages && !ct.IsCancellationRequested; i++)
        {
            var scrollY = (i + 1) * (100 + _rng.Next(0, 150));
            await ScrollToAsync(page, scrollY);

            // Occasional mouse move
            if (_rng.NextDouble() < 0.4)
                await MoveMouseRandomAsync(page);

            await Task.Delay(perStage + _rng.Next(-200, 200), ct);
        }

        // Optional: scroll back up (like a real reader)
        if (_rng.NextDouble() < 0.3)
            await ScrollToAsync(page, 0);
    }

    private static Task ScrollToAsync(IPage page, int y) =>
        page.EvaluateAsync(
            "y => window.scrollTo({ top: y, behavior: 'smooth' })", y);

    private static async Task MoveMouseRandomAsync(IPage page)
    {
        try
        {
            var vp = page.ViewportSize;
            if (vp is null) return;
            var x = _rng.Next(50, vp.Width  - 50);
            var y = _rng.Next(50, vp.Height - 50);
            await page.Mouse.MoveAsync(x, y);
        }
        catch { /* non-critical */ }
    }
}
