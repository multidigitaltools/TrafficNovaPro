using TrafficNova.Core.Models;

namespace TrafficNova.Core.Interfaces;

public interface IAppSettingsService
{
    AppSettings Current { get; }
    Task LoadAsync();
    Task SaveAsync();
    void ResetToDefaults();
}
