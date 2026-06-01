namespace TrafficNova.Core.Models;

public enum ProxyProtocol { Http, Socks4, Socks5 }

public class ProxyEntry
{
    public int Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public ProxyProtocol Protocol { get; set; } = ProxyProtocol.Http;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string Label { get; set; } = string.Empty;
    public string GroupTag { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastTestedAt { get; set; }
    public bool? LastTestOk { get; set; }
    public int AvgResponseMs { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Step 102: proxy chain stub (v1 — informational only; Playwright does not natively chain)
    public bool UseChain { get; set; } = false;
    public int? ChainProxyId { get; set; }

    // BUG-060: computed properties without [NotMapped] risk EF trying to map them
    // to non-existent columns if a backing field is ever added.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Address => $"{Host}:{Port}";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double SuccessRate => (SuccessCount + FailureCount) == 0 ? 0
        : (double)SuccessCount / (SuccessCount + FailureCount);
}
