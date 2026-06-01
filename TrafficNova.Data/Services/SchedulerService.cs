using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Data.Services;

/// <summary>
/// Background service that fires scheduled campaign jobs using Cronos cron parsing.
/// </summary>
public class SchedulerService : BackgroundService
{
    private readonly AppDbContextFactory    _factory;
    private readonly ICampaignService       _campaigns;
    private readonly IAppSettingsService    _settings;
    private readonly ILogger<SchedulerService> _log;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    public SchedulerService(
        AppDbContextFactory factory,
        ICampaignService campaigns,
        IAppSettingsService settings,
        ILogger<SchedulerService> log)
    {
        _factory   = factory;
        _campaigns = campaigns;
        _settings  = settings;
        _log       = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("SchedulerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_settings.Current.EnableScheduler)
            {
                try { await TickAsync(stoppingToken); }
                catch (Exception ex) { _log.LogError(ex, "SchedulerService tick error"); }
            }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _log.LogInformation("SchedulerService stopped");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var tz  = GetTimezone();
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        using var db = _factory.Create();
        var dueJobs = db.ScheduledJobs
            .Where(j => j.IsEnabled && j.NextRunAt.HasValue && j.NextRunAt.Value <= now)
            .ToList();

        if (dueJobs.Count == 0) return;

        var running = _campaigns.GetRunning();
        int maxSimul = _settings.Current.MaxSimultaneousCampaigns;

        foreach (var job in dueJobs)
        {
            if (ct.IsCancellationRequested) break;

            // Skip if campaign already running
            if (running.Any(c => c.Id == job.CampaignId))
            {
                _log.LogDebug("Scheduler skipping job {JobId}: campaign {CampaignId} already running",
                    job.Id, job.CampaignId);
                AdvanceNextRun(job, tz);
                continue;
            }

            // Enforce max simultaneous limit
            if (running.Count >= maxSimul)
            {
                _log.LogDebug("Scheduler: max simultaneous campaigns ({Max}) reached — queuing deferred", maxSimul);
                break;
            }

            _log.LogInformation("Scheduler firing job {JobId} for campaign {CampaignId}",
                job.Id, job.CampaignId);

            try
            {
                await _campaigns.StartAsync(job.CampaignId);
                // Store LastRunAt in the same frame as NextRunAt (scheduler-tz
                // wall-clock) so the two columns in the grid agree; `now` is
                // already DateTime.UtcNow converted into the scheduler tz.
                job.LastRunAt = now;
                job.RunCount++;

                if (job.MaxRuns > 0 && job.RunCount >= job.MaxRuns)
                {
                    job.IsEnabled  = false;
                    job.NextRunAt  = null;
                    _log.LogInformation("Job {JobId} reached MaxRuns ({Max}) — disabled", job.Id, job.MaxRuns);
                }
                else
                {
                    AdvanceNextRun(job, tz);
                }

                // Refresh running list after start
                running = _campaigns.GetRunning();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Scheduler could not start campaign {CampaignId}", job.CampaignId);
                AdvanceNextRun(job, tz);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static void AdvanceNextRun(ScheduledJob job, TimeZoneInfo tz)
    {
        try
        {
            var expr = CronExpression.Parse(job.CronExpression, CronFormat.Standard);
            var next = expr.GetNextOccurrence(DateTime.UtcNow, tz);
            job.NextRunAt = next.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(next.Value, tz)
                : null;
        }
        catch
        {
            job.IsEnabled = false;
        }
    }

    private TimeZoneInfo GetTimezone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(_settings.Current.SchedulerTimezone); }
        catch  { return TimeZoneInfo.Utc; }
    }

    // Called externally (e.g. when a new job is saved) to compute its first NextRunAt.
    public static DateTime? ComputeNextRun(string cronExpression, TimeZoneInfo tz)
    {
        try
        {
            var expr = CronExpression.Parse(cronExpression, CronFormat.Standard);
            var next = expr.GetNextOccurrence(DateTime.UtcNow, tz);
            return next.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(next.Value, tz) : null;
        }
        catch { return null; }
    }
}
