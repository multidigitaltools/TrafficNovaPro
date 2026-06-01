using System.Collections.Concurrent;

namespace TrafficNova.Engine;

/// <summary>
/// Per-domain rate limiter: enforces max N requests per minute to the same host.
/// SessionPoolService calls TryAcquireAsync before dequeuing a visit.
/// </summary>
public class RateLimiter
{
    // domain → sliding-window timestamps
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _windows = new();
    private readonly int _globalMaxPerMinute;

    public RateLimiter(int globalMaxPerMinute = 60)
    {
        _globalMaxPerMinute = globalMaxPerMinute;
    }

    /// <summary>
    /// Returns true immediately if within the rate limit; otherwise waits until a slot is free.
    /// </summary>
    public async Task AcquireAsync(string url, int? maxPerMinuteOverride = null, CancellationToken ct = default)
    {
        var host  = ExtractHost(url);
        var limit = maxPerMinuteOverride ?? _globalMaxPerMinute;
        var window = _windows.GetOrAdd(host, _ => new Queue<DateTime>());

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            DateTime? waitUntil;
            lock (window)
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-1);
                while (window.Count > 0 && window.Peek() < cutoff)
                    window.Dequeue();

                if (window.Count < limit)
                {
                    window.Enqueue(DateTime.UtcNow);
                    return; // slot acquired
                }

                // Oldest entry defines when the next slot opens
                waitUntil = window.Peek().AddMinutes(1).AddMilliseconds(50);
            }

            var delay = waitUntil.Value - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }
    }

    private static string ExtractHost(string url)
    {
        try { return new Uri(url).Host.ToLowerInvariant(); }
        catch { return url; }
    }
}
