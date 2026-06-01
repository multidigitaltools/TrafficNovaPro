using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Data.Services;

public class ProxyService : IProxyService
{
    private readonly AppDbContextFactory _factory;
    private readonly ILogger<ProxyService> _log;
    private int _roundRobinIndex;

    public event EventHandler? ProxiesChanged;

    public ProxyService(AppDbContextFactory factory, ILogger<ProxyService> log)
    {
        _factory = factory;
        _log = log;
    }

    public async Task<IList<ProxyEntry>> GetAllAsync()
    {
        using var db = _factory.Create();
        return await db.ProxyEntries.OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<ProxyEntry?> GetByIdAsync(int id)
    {
        using var db = _factory.Create();
        return await db.ProxyEntries.FindAsync(id);
    }

    public async Task<ProxyEntry> CreateAsync(ProxyEntry proxy)
    {
        proxy.CreatedAt = DateTime.UtcNow;
        using var db = _factory.Create();
        db.ProxyEntries.Add(proxy);
        await db.SaveChangesAsync();
        _log.LogInformation("Proxy created: {Host}:{Port}", proxy.Host, proxy.Port);
        ProxiesChanged?.Invoke(this, EventArgs.Empty);
        return proxy;
    }

    public async Task UpdateAsync(ProxyEntry proxy)
    {
        using var db = _factory.Create();
        db.ProxyEntries.Update(proxy);
        await db.SaveChangesAsync();
        _log.LogDebug("Proxy updated: id={Id}", proxy.Id);
        ProxiesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(int id)
    {
        using var db = _factory.Create();
        var proxy = await db.ProxyEntries.FindAsync(id);
        if (proxy is null) return;
        // BUG-082: clear the back-reference from traffic history so no session
        // points at a deleted proxy (referential integrity for existing DBs that
        // predate the model-level FK).
        var refs = await db.TrafficSessions.Where(s => s.ProxyId == id).ToListAsync();
        foreach (var s in refs) s.ProxyId = null;
        db.ProxyEntries.Remove(proxy);
        await db.SaveChangesAsync();
        _log.LogInformation("Proxy deleted: id={Id} ({Refs} session refs cleared)", id, refs.Count);
        ProxiesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteGroupAsync(string groupTag)
    {
        using var db = _factory.Create();
        var proxies = await db.ProxyEntries
            .Where(p => p.GroupTag == groupTag)
            .ToListAsync();
        // BUG-082: clear back-references for every proxy in the group before delete.
        var ids = proxies.Select(p => p.Id).ToList();
        var refs = await db.TrafficSessions
            .Where(s => s.ProxyId != null && ids.Contains(s.ProxyId.Value))
            .ToListAsync();
        foreach (var s in refs) s.ProxyId = null;
        db.ProxyEntries.RemoveRange(proxies);
        await db.SaveChangesAsync();
        _log.LogInformation("Deleted group '{Group}' ({Count} proxies, {Refs} session refs cleared)",
            groupTag, proxies.Count, refs.Count);
        ProxiesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<int> BulkImportAsync(IList<ProxyEntry> proxies)
    {
        if (proxies.Count == 0) return 0;
        var now = DateTime.UtcNow;
        foreach (var p in proxies) p.CreatedAt = now;

        using var db = _factory.Create();
        await db.ProxyEntries.AddRangeAsync(proxies);
        await db.SaveChangesAsync();
        _log.LogInformation("Bulk imported {Count} proxies", proxies.Count);
        ProxiesChanged?.Invoke(this, EventArgs.Empty);
        return proxies.Count;
    }

    public ProxyEntry? GetNext(RotationStrategy strategy = RotationStrategy.RoundRobin)
    {
        using var db = _factory.Create();
        var active = db.ProxyEntries.Where(p => p.IsActive).ToList();
        if (active.Count == 0) return null;

        return strategy switch
        {
            RotationStrategy.Random =>
                active[Random.Shared.Next(active.Count)],
            RotationStrategy.LeastUsed =>
                active.OrderBy(p => p.SuccessCount + p.FailureCount).First(),
            RotationStrategy.LeastFailed =>
                active.OrderBy(p => p.FailureCount).First(),
            _ => // RoundRobin — mask the sign bit so the index stays
                 // non-negative after _roundRobinIndex eventually overflows.
                active[(Interlocked.Increment(ref _roundRobinIndex) & int.MaxValue) % active.Count]
        };
    }

    public IList<ProxyEntry> GetActiveProxies(string? groupTag = null)
    {
        using var db = _factory.Create();
        var q = db.ProxyEntries.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(groupTag))
            q = q.Where(p => p.GroupTag == groupTag);
        return q.ToList();
    }

    public async Task MarkTestedAsync(int id, bool success, int responseMs)
    {
        using var db = _factory.Create();
        var proxy = await db.ProxyEntries.FindAsync(id);
        if (proxy is null) return;

        proxy.LastTestedAt = DateTime.UtcNow;
        proxy.LastTestOk = success;
        if (responseMs > 0)
            proxy.AvgResponseMs = proxy.AvgResponseMs == 0
                ? responseMs
                : (proxy.AvgResponseMs + responseMs) / 2;

        if (success)
            proxy.SuccessCount++;
        else
            proxy.FailureCount++;

        await db.SaveChangesAsync();
    }

    public ProxyStats GetStats()
    {
        using var db = _factory.Create();
        // BUG-083: previously loaded every proxy row into memory then counted.
        // Push the four counts to the database (cheap indexed COUNTs) so the
        // proxy table is never fully materialized just to compute stats.
        var q = db.ProxyEntries.AsQueryable();
        return new ProxyStats(
            Total: q.Count(),
            Active: q.Count(p => p.IsActive && p.LastTestOk == true),
            Dead: q.Count(p => p.LastTestOk == false),
            Untested: q.Count(p => p.LastTestedAt == null));
    }

    public IList<string> GetGroups()
    {
        using var db = _factory.Create();
        return db.ProxyEntries
            .Where(p => p.GroupTag != "")
            .Select(p => p.GroupTag)
            .Distinct()
            .OrderBy(g => g)
            .ToList();
    }
}
