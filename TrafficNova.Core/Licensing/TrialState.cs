using TrafficNova.Core.Models;

namespace TrafficNova.Core.Licensing;

/// <summary>
/// Pure helpers for the 14-day TrafficNova Pro trial. State lives on
/// <see cref="AppSettings"/> (TrialStartUtc + flags) so it round-trips through
/// settings.json without an EF migration; this class only does the math + the
/// status string and is fully unit-testable.
///
/// Trial model (BUG-086 / Phase 1 — License):
///   - The clock starts on the first launch (App.OnStartup sets TrialStartUtc).
///   - Expiry is start + TrialDays.
///   - DaysRemaining rounds UP so "23h 59m left" reads as "1 day left".
///   - Negative remaining clamps to 0.
///   - We notify once at the day-7 milestone and once at expiry (day 14);
///     persisted flags prevent re-nagging on every subsequent launch.
///   - We do NOT hard-gate the engine in v1.0.0 — the trial is honor-system
///     (settings.json is trivially editable; anti-tamper is out of scope here).
///     This matches the website copy: "if it's not for you, simply let it expire."
/// </summary>
public static class TrialState
{
    public const int TrialDays = 14;

    public static DateTime ExpiryUtc(DateTime startUtc) => startUtc.AddDays(TrialDays);

    /// <summary>
    /// Whole days left until expiry, rounded up, clamped at zero.
    /// </summary>
    public static int DaysRemaining(DateTime startUtc, DateTime nowUtc)
    {
        var remaining = ExpiryUtc(startUtc) - nowUtc;
        if (remaining <= TimeSpan.Zero) return 0;
        return (int)Math.Ceiling(remaining.TotalDays);
    }

    public static bool IsExpired(DateTime startUtc, DateTime nowUtc) =>
        nowUtc >= ExpiryUtc(startUtc);

    /// <summary>
    /// One-line label for the About / Settings → License status field. Centralised
    /// so SettingsViewModel + AboutPage render the same wording.
    /// </summary>
    public static string FormatStatus(AppSettings s, DateTime nowUtc)
    {
        if (s.IsActivated)              return "Activated";
        if (s.TrialStartUtc is null)    return "Trial";

        var start = s.TrialStartUtc.Value;
        if (IsExpired(start, nowUtc))   return "Trial expired";

        var daysLeft = DaysRemaining(start, nowUtc);
        return daysLeft == 1
            ? "Trial — 1 day left"
            : $"Trial — {daysLeft} days left";
    }
}
