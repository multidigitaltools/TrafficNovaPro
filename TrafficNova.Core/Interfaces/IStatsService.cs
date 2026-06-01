using TrafficNova.Core.Models;

namespace TrafficNova.Core.Interfaces;

public record TodayStats(int TotalVisits, int SuccessVisits, int ActiveSessions, int ProxiesOnline)
{
    public double SuccessRate => TotalVisits == 0 ? 0 : (double)SuccessVisits / TotalVisits;
}

public record HourlyBucket(DateTime Hour, int Visits, int Success);

public interface IStatsService
{
    event EventHandler? StatsUpdated;
    Task RecordVisitAsync(TrafficSession session);
    TodayStats GetTodayStats();
    IList<HourlyBucket> GetVisitsPerHour(int hours = 12);
    double GetSuccessRate(int? campaignId = null, int? lastN = null);
    Task PruneOldSessionsAsync(int retentionDays);
}
