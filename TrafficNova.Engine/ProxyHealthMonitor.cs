using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;

namespace TrafficNova.Engine;

public class ProxyHealthMonitor : BackgroundService
{
    private readonly IProxyService _proxyService;
    private readonly ProxyTesterService _tester;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<ProxyHealthMonitor> _log;

    public ProxyHealthMonitor(
        IProxyService proxyService,
        ProxyTesterService tester,
        IAppSettingsService settings,
        ILogger<ProxyHealthMonitor> log)
    {
        _proxyService = proxyService;
        _tester = tester;
        _settings = settings;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("ProxyHealthMonitor started");
        while (!stoppingToken.IsCancellationRequested)
        {
            // Clamp: a 0 interval would make this a tight CPU spin loop and a
            // negative one would throw out of ExecuteAsync and kill the service.
            var intervalMinutes = Math.Max(1, _settings.Current.ProxyHealthCheckIntervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;
            await RunHealthCheckAsync(stoppingToken);
        }
        _log.LogInformation("ProxyHealthMonitor stopped");
    }

    private async Task RunHealthCheckAsync(CancellationToken ct)
    {
        var proxies = await _proxyService.GetAllAsync();
        var active = proxies.Where(p => p.IsActive).ToList();
        if (active.Count == 0) return;

        _log.LogInformation("Health check: testing {Count} active proxies", active.Count);
        await _tester.TestAllAsync(active, ct: ct);

        // BUG-047: `active` holds detached *pre-test* entity snapshots —
        // FailureCount, LastTestOk, AvgResponseMs are stale (the actual updates
        // landed in the DB via MarkTestedAsync). Iterating `active` here meant
        // the threshold check used stale data (1-cycle deactivation lag) AND
        // `_proxyService.UpdateAsync(proxy)` then wrote the stale entity back —
        // rolling back the just-saved test results. Re-load and decide off the
        // fresh state.
        var refreshed = await _proxyService.GetAllAsync();
        var threshold = _settings.Current.ProxyDeadThreshold;
        foreach (var proxy in refreshed.Where(p => p.IsActive))
        {
            if (proxy.FailureCount >= threshold && proxy.LastTestOk == false)
            {
                proxy.IsActive = false;
                await _proxyService.UpdateAsync(proxy);
                _log.LogWarning("Proxy {Host}:{Port} deactivated after {N} failures",
                    proxy.Host, proxy.Port, proxy.FailureCount);
            }
        }

        // Alert if proxy count drops below threshold (re-count from current state
        // because we may have just deactivated some).
        var aliveCount = (await _proxyService.GetAllAsync()).Count(p => p.IsActive);
        var alertThreshold = _settings.Current.ProxyLowAlertThreshold;
        if (aliveCount < alertThreshold)
            _log.LogWarning("Active proxy count ({Count}) is below alert threshold ({Threshold})",
                aliveCount, alertThreshold);
    }
}
