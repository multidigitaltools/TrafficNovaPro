using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Data.Services;

public class StatsService : IStatsService
{
    private readonly AppDbContextFactory _factory;
    private readonly IProxyService _proxies;
    private readonly ILogger<StatsService> _log;

    public event EventHandler? StatsUpdated;

    public StatsService(
        AppDbContextFactory factory,
        IProxyService proxies,
        ILogger<StatsService> log)
    {
        _factory = factory;
        _proxies = proxies;
        _log     = log;
    }

    public async Task RecordVisitAsync(TrafficSession session)
    {
        // Session is already persisted by CampaignService.RecordSessionAsync;
        // fire the event so dashboard updates live
        StatsUpdated?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask;
    }

    public TodayStats GetTodayStats()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            using var db = _factory.Create();

            var total   = db.TrafficSessions.Count(s => s.StartedAt >= today);
            var success = db.TrafficSessions.Count(s => s.StartedAt >= today && s.Success);
            var active  = db.Campaigns.Count(c => c.Status == CampaignStatus.Running);
            var online  = _proxies.GetStats().Active;

            return new TodayStats(total, success, active, online);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "GetTodayStats failed");
            return new TodayStats(0, 0, 0, 0);
        }
    }

    public IList<HourlyBucket> GetVisitsPerHour(int hours = 12)
    {
        try
        {
            var since = DateTime.UtcNow.AddHours(-hours);
            using var db = _factory.Create();

            var sessions = db.TrafficSessions
                .Where(s => s.StartedAt >= since)
                .Select(s => new { s.StartedAt, s.Success })
                .ToList();

            return Enumerable.Range(0, hours)
                .Select(i =>
                {
                    var hour  = DateTime.UtcNow.AddHours(-(hours - 1 - i)).Date
                                + TimeSpan.FromHours(DateTime.UtcNow.AddHours(-(hours - 1 - i)).Hour);
                    var inHour = sessions.Where(s =>
                        s.StartedAt >= hour && s.StartedAt < hour.AddHours(1)).ToList();
                    return new HourlyBucket(hour, inHour.Count, inHour.Count(s => s.Success));
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "GetVisitsPerHour failed");
            return Enumerable.Range(0, hours)
                .Select(i => new HourlyBucket(DateTime.UtcNow.AddHours(-i), 0, 0))
                .ToList();
        }
    }

    public double GetSuccessRate(int? campaignId = null, int? lastN = null)
    {
        try
        {
            using var db = _factory.Create();
            var q = db.TrafficSessions.AsQueryable();
            if (campaignId.HasValue) q = q.Where(s => s.CampaignId == campaignId.Value);
            if (lastN.HasValue)      q = q.OrderByDescending(s => s.StartedAt).Take(lastN.Value);
            var total   = q.Count();
            var success = q.Count(s => s.Success);
            return total == 0 ? 0 : (double)success / total;
        }
        catch { return 0; }
    }

    public async Task PruneOldSessionsAsync(int retentionDays)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            using var db = _factory.Create();

            // BUG-051: Original code deleted ALL sessions older than the cutoff,
            // including those belonging to currently-running campaigns, causing
            // data loss mid-run and broken analytics. Exclude running campaigns.
            var runningIds = await db.Campaigns
                .Where(c => c.Status == CampaignStatus.Running || c.Status == CampaignStatus.Paused)
                .Select(c => c.Id)
                .ToListAsync();

            var old = await db.TrafficSessions
                .Where(s => s.StartedAt < cutoff && !runningIds.Contains(s.CampaignId))
                .ToListAsync();

            if (old.Count > 0)
            {
                db.TrafficSessions.RemoveRange(old);
                await db.SaveChangesAsync();
                _log.LogInformation("Pruned {Count} sessions older than {Days} days", old.Count, retentionDays);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "PruneOldSessionsAsync failed");
        }
    }

    /// <summary>Called by background timer to fire StatsUpdated periodically.</summary>
    public void FireUpdate() => StatsUpdated?.Invoke(this, EventArgs.Empty);
}
