using Microsoft.Playwright;
using TrafficNova.Core.Models;

namespace TrafficNova.Engine;

/// <summary>Converts a ProxyEntry into Playwright's ProxySettings record.</summary>
public static class ProxyRouter
{
    public static Proxy? Build(ProxyEntry? proxy)
    {
        if (proxy is null) return null;

        var scheme = proxy.Protocol switch
        {
            ProxyProtocol.Socks4 => "socks4",
            ProxyProtocol.Socks5 => "socks5",
            _                    => "http",
        };

        var settings = new Proxy
        {
            Server = $"{scheme}://{proxy.Host}:{proxy.Port}",
        };

        if (!string.IsNullOrWhiteSpace(proxy.Username))
        {
            settings.Username = proxy.Username;
            settings.Password = proxy.Password ?? string.Empty;
        }

        return settings;
    }
}
