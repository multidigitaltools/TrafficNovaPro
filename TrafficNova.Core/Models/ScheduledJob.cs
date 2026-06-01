namespace TrafficNova.Core.Models;

public class ScheduledJob
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public int RunCount { get; set; }
    public int MaxRuns { get; set; } = 0; // 0 = unlimited
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Campaign? Campaign { get; set; }
}
