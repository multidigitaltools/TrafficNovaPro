using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Engine;

public record ProxyTestProgress(int Tested, int Total, int Passed, int Failed, ProxyEntry? LastTested);

public class ProxyTesterService
{
    private readonly IProxyService _proxyService;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<ProxyTesterService> _log;

    private const int MaxParallel = 20;

    public ProxyTesterService(
        IProxyService proxyService,
        IAppSettingsService settings,
        ILogger<ProxyTesterService> log)
    {
        _proxyService = proxyService;
        _settings = settings;
        _log = log;
    }

    public async Task<(bool ok, int responseMs)> TestProxyAsync(
        ProxyEntry proxy, CancellationToken ct = default)
    {
        var testUrl = _settings.Current.ProxyTestUrl;
        var sw = Stopwatch.StartNew();
        try
        {
            var handler = BuildHandler(proxy);
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync(testUrl, ct);
            sw.Stop();
            var ok = response.IsSuccessStatusCode;
            _log.LogDebug("Proxy {Host}:{Port} → {Status} ({Ms}ms)",
                proxy.Host, proxy.Port, ok ? "OK" : "FAIL", sw.ElapsedMilliseconds);
            return (ok, (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // Any failure (network error, timeout, or a malformed host that
            // makes BuildHandler's Uri ctor throw) just means the proxy is
            // unusable — never let it escape and fault the whole test batch
            // or the ProxyHealthMonitor background loop.
            sw.Stop();
            _log.LogDebug("Proxy {Host}:{Port} failed: {Msg}", proxy.Host, proxy.Port, ex.Message);
            return (false, 0);
        }
    }

    public async Task TestAllAsync(
        IList<ProxyEntry> proxies,
        IProgress<ProxyTestProgress>? progress = null,
        CancellationToken ct = default)
    {
        int tested = 0, passed = 0, failed = 0;
        // BUG-046: SemaphoreSlim is IDisposable and was leaked once per batch —
        // every manual "Test All" click and every ProxyHealthMonitor cycle.
        using var sem = new SemaphoreSlim(MaxParallel, MaxParallel);

        var tasks = proxies.Select(async proxy =>
        {
            await sem.WaitAsync(ct);
            try
            {
                var (ok, ms) = await TestProxyAsync(proxy, ct);
                await _proxyService.MarkTestedAsync(proxy.Id, ok, ms);
                Interlocked.Increment(ref tested);
                if (ok) Interlocked.Increment(ref passed);
                else Interlocked.Increment(ref failed);
                progress?.Report(new ProxyTestProgress(tested, proxies.Count, passed, failed, proxy));
            }
            finally { sem.Release(); }
        });

        await Task.WhenAll(tasks);
        _log.LogInformation("Proxy test complete: {Passed}/{Total} passed", passed, proxies.Count);
    }

    private static HttpClientHandler BuildHandler(ProxyEntry proxy)
    {
        var proxyUri = new Uri($"{proxy.Protocol.ToString().ToLower()}://{proxy.Host}:{proxy.Port}");
        var webProxy = new WebProxy(proxyUri);
        if (!string.IsNullOrEmpty(proxy.Username))
            webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);

        return new HttpClientHandler
        {
            Proxy = webProxy,
            UseProxy = true,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
    }
}
