using System.Net;
using System.Net.Sockets;

namespace TrafficNova.Core.Net;

/// <summary>
/// SSRF guard for user-configurable URLs (ProxyTestUrl, FlareSolverrUrl). Any URL
/// that resolves to a private, loopback, or link-local address must be rejected
/// before it is handed to an outbound HttpClient — otherwise a "Test" click (or a
/// malicious settings import) becomes a probe of the internal network.
/// Extracted from SettingsViewModel (Pass 9, BUG-050/065/072) so it is reusable
/// and unit-testable from TrafficNova.Tests.
/// </summary>
public static class UrlSafety
{
    /// <summary>
    /// Returns true when <paramref name="url"/> targets a private, loopback, or
    /// link-local host (IPv4 or IPv6) or the literal "localhost". A blank/invalid
    /// URL returns false (callers treat blank as "feature disabled").
    /// </summary>
    public static bool IsPrivateOrLoopback(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (!IPAddress.TryParse(host, out var ip)) return false;

        // IPv6: loopback (::1), ULA (fc00::/7), link-local (fe80::/10)
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(ip)) return true;
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) return true;                       // fc00::/7 ULA
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true;   // fe80::/10 link-local
            return false;
        }

        // IPv4: 10/8, 172.16-31/12, 192.168/16, 127/8, 169.254/16
        var b = ip.GetAddressBytes();
        return ip.AddressFamily == AddressFamily.InterNetwork &&
               (b[0] == 10 ||
                (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                (b[0] == 192 && b[1] == 168) ||
                b[0] == 127 ||
                (b[0] == 169 && b[1] == 254));
    }
}
