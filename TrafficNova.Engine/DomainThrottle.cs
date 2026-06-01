using System.Collections.Concurrent;

namespace TrafficNova.Engine;

/// <summary>
/// Limits concurrent Playwright sessions per target domain.
/// Callers acquire a slot before launching a browser context and release it on completion.
/// </summary>
public sealed class DomainThrottle : IDisposable
{
    private readonly int _maxPerDomain;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    public DomainThrottle(int maxPerDomain = 5)
    {
        _maxPerDomain = maxPerDomain > 0 ? maxPerDomain : 5;
    }

    public async Task<IDisposable> AcquireAsync(string url, CancellationToken ct = default)
    {
        var host = GetHost(url);
        var sem  = _semaphores.GetOrAdd(host, _ => new SemaphoreSlim(_maxPerDomain, _maxPerDomain));
        await sem.WaitAsync(ct);
        return new Releaser(sem);
    }

    private static string GetHost(string url)
    {
        try { return new Uri(url).Host.ToLowerInvariant(); }
        catch { return url; }
    }

    public void Dispose()
    {
        foreach (var sem in _semaphores.Values)
            sem.Dispose();
        _semaphores.Clear();
    }

    private sealed class Releaser(SemaphoreSlim sem) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            sem.Release();
        }
    }
}
