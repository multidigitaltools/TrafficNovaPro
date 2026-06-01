namespace TrafficNova.Core.Models;

public class TrafficSession
{
    public long Id { get; set; }
    public int CampaignId { get; set; }
    public int? ProxyId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string Referrer { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public bool Success { get; set; }
    public int? StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int DwellMs { get; set; }
    public string? ScreenshotPath { get; set; }
    public string? TracePath { get; set; }
    // Phase 10: number of sub-resource requests aborted (bandwidth saved)
    public int BlockedRequests { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int? DurationMs => EndedAt.HasValue
        ? (int)(EndedAt.Value - StartedAt).TotalMilliseconds
        : null;
}
