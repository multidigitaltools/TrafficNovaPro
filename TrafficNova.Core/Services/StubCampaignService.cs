using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Core.Services;

// Minimal stub — full implementation added in Step 28
public class StubCampaignService : ICampaignService
{
    private readonly ILogger<StubCampaignService> _log;
    private readonly List<Campaign> _campaigns = new();

    public StubCampaignService(ILogger<StubCampaignService> log) => _log = log;

    public Task<IList<Campaign>> GetAllAsync() =>
        Task.FromResult<IList<Campaign>>(_campaigns.ToList());

    public Task<Campaign?> GetByIdAsync(int id) =>
        Task.FromResult(_campaigns.FirstOrDefault(c => c.Id == id));

    public Task<Campaign> CreateAsync(Campaign campaign)
    {
        campaign.Id = _campaigns.Count + 1;
        _campaigns.Add(campaign);
        return Task.FromResult(campaign);
    }

    public Task UpdateAsync(Campaign campaign)
    {
        var idx = _campaigns.FindIndex(c => c.Id == campaign.Id);
        if (idx >= 0) _campaigns[idx] = campaign;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        _campaigns.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }

    public Task StartAsync(int id)
    {
        var c = _campaigns.FirstOrDefault(x => x.Id == id);
        if (c is not null) c.Status = CampaignStatus.Running;
        return Task.CompletedTask;
    }

    public Task StopAsync(int id)
    {
        var c = _campaigns.FirstOrDefault(x => x.Id == id);
        if (c is not null) c.Status = CampaignStatus.Idle;
        return Task.CompletedTask;
    }

    public Task PauseAsync(int id)
    {
        var c = _campaigns.FirstOrDefault(x => x.Id == id);
        if (c is not null) c.Status = CampaignStatus.Paused;
        return Task.CompletedTask;
    }

    public Task ResumeAsync(int id)
    {
        var c = _campaigns.FirstOrDefault(x => x.Id == id);
        if (c is not null) c.Status = CampaignStatus.Running;
        return Task.CompletedTask;
    }

    public IList<Campaign> GetRunning() =>
        _campaigns.Where(c => c.Status == CampaignStatus.Running).ToList();

    public CampaignStats? GetStats(int id) => null;

    public Task<IList<TrafficSession>> GetRecentSessionsAsync(int campaignId, int limit = 100) =>
        Task.FromResult<IList<TrafficSession>>(new List<TrafficSession>());

    public Task<IList<TrafficSession>> GetAllRecentSessionsAsync(int limit = 500) =>
        Task.FromResult<IList<TrafficSession>>(new List<TrafficSession>());

    public Task RecordSessionAsync(TrafficSession session) => Task.CompletedTask;

    public event EventHandler? CampaignsChanged;
}
