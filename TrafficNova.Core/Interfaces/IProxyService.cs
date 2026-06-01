using TrafficNova.Core.Models;

namespace TrafficNova.Core.Interfaces;

public enum RotationStrategy { RoundRobin, Random, LeastUsed, LeastFailed }

public record ProxyStats(int Total, int Active, int Dead, int Untested);

public interface IProxyService
{
    Task<IList<ProxyEntry>> GetAllAsync();
    Task<ProxyEntry?> GetByIdAsync(int id);
    Task<ProxyEntry> CreateAsync(ProxyEntry proxy);
    Task UpdateAsync(ProxyEntry proxy);
    Task DeleteAsync(int id);
    Task DeleteGroupAsync(string groupTag);
    Task<int> BulkImportAsync(IList<ProxyEntry> proxies);
    ProxyEntry? GetNext(RotationStrategy strategy = RotationStrategy.RoundRobin);
    /// <summary>Active proxies, optionally filtered to one group — the pool a campaign run rotates through.</summary>
    IList<ProxyEntry> GetActiveProxies(string? groupTag = null);
    Task MarkTestedAsync(int id, bool success, int responseMs);
    ProxyStats GetStats();
    IList<string> GetGroups();
    event EventHandler? ProxiesChanged;
}
