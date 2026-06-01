using TrafficNova.Core.Models;

namespace TrafficNova.Core.Interfaces;

public interface IScheduledJobService
{
    Task<IList<ScheduledJob>> GetAllAsync();
    Task<ScheduledJob> CreateAsync(ScheduledJob job);
    Task UpdateAsync(ScheduledJob job);
    Task DeleteAsync(int id);
    Task SetEnabledAsync(int id, bool enabled);
}
