using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Services;

namespace TrafficNova.Engine;

/// <summary>
/// Monitors running campaigns for stalls; alerts and cancels if stuck.
/// </summary>
public class WatchdogService : BackgroundService
{
    private readonly ICampaignService        _campaigns;
    private readonly IAppSettingsService     _settings;
    private readonly NotificationService     _notify;
    private readonly ILogger<WatchdogService> _log;

    // campaign id → (last visit count, consecutive stall ticks)
    private readonly Dictionary<int, (int lastCount, int stallTicks)> _state = new();

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

    public WatchdogService(
        ICampaignService campaigns,
        IAppSettingsService settings,
        NotificationService notify,
        ILogger<WatchdogService> log)
    {
        _campaigns = campaigns;
        _settings  = settings;
        _notify    = notify;
        _log       = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("WatchdogService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(); }
            catch (Exception ex) { _log.LogError(ex, "WatchdogService tick error"); }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _log.LogInformation("WatchdogService stopped");
    }

    private async Task TickAsync()
    {
        var running = _campaigns.GetRunning();

        // Remove state for campaigns that are no longer running
        foreach (var id in _state.Keys.Except(running.Select(c => c.Id)).ToList())
            _state.Remove(id);

        int timeout = _settings.Current.DefaultSessionTimeoutSeconds;

        foreach (var campaign in running)
        {
            var stats = _campaigns.GetStats(campaign.Id);
            int current = stats?.TotalVisits ?? 0;

            if (!_state.TryGetValue(campaign.Id, out var prev))
            {
                _state[campaign.Id] = (current, 0);
                continue;
            }

            if (current > prev.lastCount)
            {
                // Progress — reset stall counter
                _state[campaign.Id] = (current, 0);
            }
            else
            {
                int ticks = prev.stallTicks + 1;
                _state[campaign.Id] = (current, ticks);

                _log.LogWarning("Campaign {Id} stall tick {Ticks} (visits unchanged at {Count})",
                    campaign.Id, ticks, current);

                if (ticks == 2)
                {
                    _notify.Notify("Campaign Stall Warning",
                        $"'{campaign.Name}' shows no progress for {ticks * 60}s.",
                        NotificationLevel.Warning);
                }
                else if (ticks >= 3)
                {
                    _log.LogError("Campaign {Id} stalled for {Sec}s — forcing stop",
                        campaign.Id, ticks * 60);
                    _notify.Notify("Campaign Force-Stopped",
                        $"'{campaign.Name}' was stopped after {ticks * 60}s with no activity.",
                        NotificationLevel.Error);
                    // StopAsync cancels the pool AND resets the campaign Status —
                    // _pool.Cancel alone left it stuck "Running" and re-alarming
                    // the same campaign every few ticks.
                    await _campaigns.StopAsync(campaign.Id);
                    _state.Remove(campaign.Id);
                }
            }
        }
    }
}
