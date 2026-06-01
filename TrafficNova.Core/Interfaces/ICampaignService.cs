using TrafficNova.Core.Models;

namespace TrafficNova.Core.Interfaces;

public record CampaignStats(
    int CampaignId, int TotalVisits, int SuccessVisits, int ThreadCount,
    TimeSpan Elapsed, int QueuedVisits);

public interface ICampaignService
{
    Task<IList<Campaign>> GetAllAsync();
    Task<Campaign?> GetByIdAsync(int id);
    Task<Campaign> CreateAsync(Campaign campaign);
    Task UpdateAsync(Campaign campaign);
    Task DeleteAsync(int id);
    Task StartAsync(int id);
    Task StopAsync(int id);
    Task PauseAsync(int id);
    Task ResumeAsync(int id);
    IList<Campaign> GetRunning();
    CampaignStats? GetStats(int id);
    Task<IList<TrafficSession>> GetRecentSessionsAsync(int campaignId, int limit = 100);
    Task<IList<TrafficSession>> GetAllRecentSessionsAsync(int limit = 500);
    Task RecordSessionAsync(TrafficSession session);
    event EventHandler? CampaignsChanged;
}
