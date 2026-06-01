using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Core.Services;

// Minimal stub — full implementation added in Step 17
public class StubProxyService : IProxyService
{
    private readonly ILogger<StubProxyService> _log;
    private readonly List<ProxyEntry> _proxies = new();
    private int _roundRobinIndex;

    public StubProxyService(ILogger<StubProxyService> log) => _log = log;

    public Task<IList<ProxyEntry>> GetAllAsync() =>
        Task.FromResult<IList<ProxyEntry>>(_proxies.ToList());

    public Task<ProxyEntry?> GetByIdAsync(int id) =>
        Task.FromResult(_proxies.FirstOrDefault(p => p.Id == id));

    public Task<ProxyEntry> CreateAsync(ProxyEntry proxy)
    {
        proxy.Id = _proxies.Count + 1;
        _proxies.Add(proxy);
        return Task.FromResult(proxy);
    }

    public Task UpdateAsync(ProxyEntry proxy)
    {
        var idx = _proxies.FindIndex(p => p.Id == proxy.Id);
        if (idx >= 0) _proxies[idx] = proxy;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        _proxies.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }

    public Task<int> BulkImportAsync(IList<ProxyEntry> proxies)
    {
        foreach (var p in proxies)
        {
            p.Id = _proxies.Count + 1;
            _proxies.Add(p);
        }
        return Task.FromResult(proxies.Count);
    }

    public ProxyEntry? GetNext(RotationStrategy strategy = RotationStrategy.RoundRobin)
    {
        var active = _proxies.Where(p => p.IsActive).ToList();
        if (active.Count == 0) return null;
        var index = Interlocked.Increment(ref _roundRobinIndex) % active.Count;
        return active[index];
    }

    public IList<ProxyEntry> GetActiveProxies(string? groupTag = null) =>
        _proxies.Where(p => p.IsActive &&
            (string.IsNullOrWhiteSpace(groupTag) || p.GroupTag == groupTag)).ToList();

    public Task MarkTestedAsync(int id, bool success, int responseMs)
    {
        var proxy = _proxies.FirstOrDefault(p => p.Id == id);
        if (proxy is null) return Task.CompletedTask;
        proxy.LastTestedAt = DateTime.UtcNow;
        proxy.LastTestOk = success;
        proxy.AvgResponseMs = responseMs;
        if (success) proxy.SuccessCount++; else proxy.FailureCount++;
        return Task.CompletedTask;
    }

    public Task DeleteGroupAsync(string groupTag)
    {
        _proxies.RemoveAll(p => p.GroupTag == groupTag);
        return Task.CompletedTask;
    }

    public ProxyStats GetStats() => new(
        _proxies.Count,
        _proxies.Count(p => p.IsActive && p.LastTestOk == true),
        _proxies.Count(p => p.LastTestOk == false),
        _proxies.Count(p => p.LastTestedAt == null));

    public IList<string> GetGroups() =>
        _proxies.Where(p => p.GroupTag != "").Select(p => p.GroupTag).Distinct().ToList();

    public event EventHandler? ProxiesChanged;
}
