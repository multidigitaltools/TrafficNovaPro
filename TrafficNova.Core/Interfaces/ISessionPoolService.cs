using TrafficNova.Core.Models;

namespace TrafficNova.Core.Interfaces;

/// <summary>Abstraction over SessionPoolService so CampaignService doesn't reference the Engine assembly.</summary>
public interface ISessionPoolService
{
    Task RunCampaignAsync(Campaign campaign, EngineConfig engine);
    void Cancel(int campaignId);

    /// <summary>
    /// Phase 10 — pause/resume new visit dispatch for a campaign. In-flight
    /// visits finish naturally; no new sessions start while paused.
    /// </summary>
    void SetPaused(int campaignId, bool paused);

    /// <summary>
    /// Phase 10 — runs exactly one visit using the campaign config and
    /// returns the result (used by the Test-Run preview).
    /// </summary>
    Task<SessionResult> RunSingleVisitAsync(Campaign campaign, EngineConfig engine, CancellationToken ct = default);
}
