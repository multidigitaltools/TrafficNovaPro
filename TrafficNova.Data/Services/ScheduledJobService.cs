using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Data.Services;

public class ScheduledJobService : IScheduledJobService
{
    private readonly AppDbContextFactory       _factory;
    private readonly ILogger<ScheduledJobService> _log;

    public ScheduledJobService(AppDbContextFactory factory, ILogger<ScheduledJobService> log)
    {
        _factory = factory;
        _log     = log;
    }

    public async Task<IList<ScheduledJob>> GetAllAsync()
    {
        using var db = _factory.Create();
        return await db.ScheduledJobs
            .Include(j => j.Campaign)
            .OrderBy(j => j.NextRunAt)
            .ToListAsync();
    }

    public async Task<ScheduledJob> CreateAsync(ScheduledJob job)
    {
        using var db = _factory.Create();
        job.CreatedAt = DateTime.UtcNow;
        db.ScheduledJobs.Add(job);
        await db.SaveChangesAsync();
        _log.LogInformation("Created scheduled job {Id} for campaign {CampaignId}", job.Id, job.CampaignId);
        return job;
    }

    public async Task UpdateAsync(ScheduledJob job)
    {
        using var db = _factory.Create();
        db.ScheduledJobs.Update(job);
        await db.SaveChangesAsync();
        _log.LogInformation("Updated scheduled job {Id}", job.Id);
    }

    public async Task DeleteAsync(int id)
    {
        using var db = _factory.Create();
        var job = await db.ScheduledJobs.FindAsync(id);
        if (job is null) return;
        db.ScheduledJobs.Remove(job);
        await db.SaveChangesAsync();
        _log.LogInformation("Deleted scheduled job {Id}", id);
    }

    public async Task SetEnabledAsync(int id, bool enabled)
    {
        using var db = _factory.Create();
        var job = await db.ScheduledJobs.FindAsync(id);
        if (job is null) return;
        job.IsEnabled = enabled;
        await db.SaveChangesAsync();
    }
}
