using System.Collections.Concurrent;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Data.Services;

public class CampaignService : ICampaignService
{
    private readonly AppDbContextFactory _factory;
    private readonly ILogger<CampaignService> _log;
    // ConcurrentDictionary: accessed from the UI thread, the scheduler thread,
    // the watchdog thread, and session-pool worker threads simultaneously.
    private readonly ConcurrentDictionary<int, CampaignRuntime> _runtimes = new();

    // Injected lazily to break the circular registration order:
    // CampaignService is registered before SessionPoolService in DI
    private ISessionPoolService? _sessionPool;
    private IAppSettingsService? _settings;

    public event EventHandler? CampaignsChanged;

    public CampaignService(AppDbContextFactory factory, ILogger<CampaignService> log)
    {
        _factory = factory;
        _log = log;
    }

    /// <summary>Called by App.xaml.cs after the full DI container is built.</summary>
    public void SetEngineServices(ISessionPoolService pool, IAppSettingsService settings)
    {
        _sessionPool = pool;
        _settings    = settings;
    }

    public async Task<IList<Campaign>> GetAllAsync()
    {
        using var db = _factory.Create();
        return await db.Campaigns.OrderByDescending(c => c.UpdatedAt).ToListAsync();
    }

    public async Task<Campaign?> GetByIdAsync(int id)
    {
        using var db = _factory.Create();
        return await db.Campaigns.FindAsync(id);
    }

    public async Task<Campaign> CreateAsync(Campaign campaign)
    {
        campaign.CreatedAt = campaign.UpdatedAt = DateTime.UtcNow;
        using var db = _factory.Create();
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        _log.LogInformation("Campaign created: {Name}", campaign.Name);
        CampaignsChanged?.Invoke(this, EventArgs.Empty);
        return campaign;
    }

    public async Task UpdateAsync(Campaign campaign)
    {
        campaign.UpdatedAt = DateTime.UtcNow;
        using var db = _factory.Create();
        db.Campaigns.Update(campaign);
        await db.SaveChangesAsync();
        _log.LogDebug("Campaign updated: id={Id}", campaign.Id);
        CampaignsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(int id)
    {
        // Stop if running
        if (_runtimes.ContainsKey(id)) await StopAsync(id);

        using var db = _factory.Create();
        var campaign = await db.Campaigns.FindAsync(id);
        if (campaign is null) return;
        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync();
        _log.LogInformation("Campaign deleted: id={Id}", id);
        CampaignsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StartAsync(int id)
    {
        var campaign = await GetByIdAsync(id);
        if (campaign is null) return;
        if (campaign.Status == CampaignStatus.Running) return;

        campaign.Status    = CampaignStatus.Running;
        campaign.LastRunAt = DateTime.UtcNow;
        await UpdateAsync(campaign);

        var runtime = new CampaignRuntime(id, campaign.ThreadCount);
        _runtimes[id] = runtime;
        _log.LogInformation("Campaign started: {Name} (id={Id})", campaign.Name, id);

        // Step 59 — enqueue into browser session pool
        if (_sessionPool is not null && _settings is not null)
        {
            var engine = EngineConfig.FromSettings(_settings.Current);
            _ = _sessionPool.RunCampaignAsync(campaign, engine);
        }
    }

    public async Task StopAsync(int id)
    {
        _runtimes.TryRemove(id, out var runtime);
        runtime?.Cancel();

        // Step 59 — cancel pool if running
        _sessionPool?.Cancel(id);

        var campaign = await GetByIdAsync(id);
        if (campaign is null) return;
        campaign.Status = CampaignStatus.Idle;
        // Persist the final visit counts so the campaigns grid still shows
        // real progress after the campaign stops — and across an app restart.
        if (runtime is not null)
        {
            campaign.TotalVisits   = runtime.VisitsDone;
            campaign.SuccessVisits = runtime.SuccessCount;
        }
        await UpdateAsync(campaign);
        _log.LogInformation("Campaign stopped: id={Id}", id);
    }

    public async Task PauseAsync(int id)
    {
        if (_runtimes.TryGetValue(id, out var runtime)) runtime.Pause();
        // Phase 10 — actually halt new visit dispatch in the session pool
        _sessionPool?.SetPaused(id, true);
        var campaign = await GetByIdAsync(id);
        if (campaign is null) return;
        campaign.Status = CampaignStatus.Paused;
        await UpdateAsync(campaign);
        _log.LogInformation("Campaign paused: id={Id}", id);
    }

    public async Task ResumeAsync(int id)
    {
        if (_runtimes.TryGetValue(id, out var runtime)) runtime.Resume();
        // Phase 10 — release dispatch hold in the session pool
        _sessionPool?.SetPaused(id, false);
        var campaign = await GetByIdAsync(id);
        if (campaign is null) return;
        campaign.Status = CampaignStatus.Running;
        await UpdateAsync(campaign);
        _log.LogInformation("Campaign resumed: id={Id}", id);
    }

    public IList<Campaign> GetRunning()
    {
        using var db = _factory.Create();
        return db.Campaigns.Where(c => c.Status == CampaignStatus.Running).ToList();
    }

    public CampaignStats? GetStats(int id)
    {
        if (!_runtimes.TryGetValue(id, out var rt)) return null;
        return new CampaignStats(id, rt.VisitsDone, rt.SuccessCount,
            rt.ThreadCount, rt.Elapsed, rt.Queued);
    }

    public async Task<IList<TrafficSession>> GetRecentSessionsAsync(int campaignId, int limit = 100)
    {
        using var db = _factory.Create();
        return await db.TrafficSessions
            .Where(s => s.CampaignId == campaignId)
            .OrderByDescending(s => s.StartedAt)
            .Take(limit)
            .ToListAsync();
    }

    // Single query for the most-recent sessions across every campaign — the
    // session-log page previously ran one query per campaign (N+1).
    public async Task<IList<TrafficSession>> GetAllRecentSessionsAsync(int limit = 500)
    {
        using var db = _factory.Create();
        return await db.TrafficSessions
            .OrderByDescending(s => s.StartedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task RecordSessionAsync(TrafficSession session)
    {
        session.StartedAt = session.StartedAt == default ? DateTime.UtcNow : session.StartedAt;
        using var db = _factory.Create();
        db.TrafficSessions.Add(session);
        await db.SaveChangesAsync();

        // Update runtime counters
        if (_runtimes.TryGetValue(session.CampaignId, out var rt))
        {
            Interlocked.Increment(ref rt.VisitsDone);
            if (session.Success) Interlocked.Increment(ref rt.SuccessCount);
        }
    }
}

// Tracks live runtime state (not persisted)
internal class CampaignRuntime
{
    public int CampaignId { get; }
    public int ThreadCount { get; }
    public int VisitsDone;
    public int SuccessCount;
    public int Queued;
    public bool IsPaused { get; private set; }
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken CancellationToken => _cts.Token;
    public TimeSpan Elapsed => DateTime.UtcNow - _startedAt;

    public CampaignRuntime(int id, int threadCount)
    {
        CampaignId = id;
        ThreadCount = threadCount;
    }

    public void Cancel() => _cts.Cancel();
    public void Pause() => IsPaused = true;
    public void Resume() => IsPaused = false;
}
