using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Core.Services;

// Minimal stub — full implementation added in Step 61
public class StubStatsService : IStatsService
{
    private int _totalToday;
    private int _successToday;

    public event EventHandler? StatsUpdated;

    public Task RecordVisitAsync(TrafficSession session)
    {
        _totalToday++;
        if (session.Success) _successToday++;
        StatsUpdated?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public TodayStats GetTodayStats() =>
        new(_totalToday, _successToday, 0, 0);

    public IList<HourlyBucket> GetVisitsPerHour(int hours = 12) =>
        Enumerable.Range(0, hours)
            .Select(i => new HourlyBucket(DateTime.UtcNow.AddHours(-i), 0, 0))
            .ToList();

    public double GetSuccessRate(int? campaignId = null, int? lastN = null) =>
        _totalToday == 0 ? 0 : (double)_successToday / _totalToday;

    public Task PruneOldSessionsAsync(int retentionDays) => Task.CompletedTask;
}
