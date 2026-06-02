using System.IO;

namespace TrafficNova.Core.Models;

public class AppSettings
{
    // ── Paths ──────────────────────────────────────────────────────────
    public string DbPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrafficNovaPro", "trafficnova.db");

    public string LogDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrafficNovaPro", "logs");

    public string CrashDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrafficNovaPro", "crashes");

    public string ScreenshotDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrafficNovaPro", "screenshots");

    // ── General ────────────────────────────────────────────────────────
    public string Theme { get; set; } = "Light";
    public bool LaunchOnStartup { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = false;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public bool ShowOnboardingOnFirstRun { get; set; } = true;

    // ── Browser / Engine ───────────────────────────────────────────────
    public bool DefaultHeadlessMode { get; set; } = true;
    public string BrowserExecutablePath { get; set; } = string.Empty;
    public int MaxConcurrentSessions { get; set; } = 20;
    public int DefaultSessionTimeoutSeconds { get; set; } = 30;
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 1000;
    public string StealthIntensity { get; set; } = "Medium"; // Low, Medium, High

    // ── Proxy ─────────────────────────────────────────────────────────
    public bool AutoTestProxiesOnImport { get; set; } = true;
    public int ProxyHealthCheckIntervalMinutes { get; set; } = 15;
    public int ProxyDeadThreshold { get; set; } = 5;
    public string ProxyTestUrl { get; set; } = "http://httpbin.org/ip";

    // ── Scheduler ──────────────────────────────────────────────────────
    public bool EnableScheduler { get; set; } = true;
    public int MaxSimultaneousCampaigns { get; set; } = 5;
    public string SchedulerTimezone { get; set; } = "UTC";

    // ── Dashboard ──────────────────────────────────────────────────────
    public int DashboardRefreshSeconds { get; set; } = 5;

    // ── Notifications ──────────────────────────────────────────────────
    public bool EnableDesktopNotifications { get; set; } = true;
    public int ProxyLowAlertThreshold { get; set; } = 5;
    public double ErrorRateAlertThreshold { get; set; } = 0.5;

    // ── Logging ────────────────────────────────────────────────────────
    public string LogLevel { get; set; } = "Information";
    public int LogRetentionDays { get; set; } = 30;
    public int MaxLogFileSizeMb { get; set; } = 50;

    // ── License ────────────────────────────────────────────────────────
    public string LicenseKey { get; set; } = string.Empty;
    public bool IsActivated { get; set; } = false;

    // BUG-086 / Phase 1 — 14-day trial bookkeeping.
    // TrialStartUtc is null until the very first launch sets it (in App.OnStartup);
    // null also reads as "Trial" (clock hasn't started) so display code stays sane
    // before the first SaveAsync. The two flags prevent re-nagging the milestone
    // popups on every launch. See TrafficNova.Core.Licensing.TrialState for the
    // math + the canonical status string.
    public DateTime? TrialStartUtc       { get; set; } = null;
    public bool      TrialNotifiedDay7   { get; set; } = false;
    public bool      TrialNotifiedDay14  { get; set; } = false;

    // ── Advanced / Bypass (Step 100) ────────────────────────────────────
    public string FlareSolverrUrl { get; set; } = string.Empty; // e.g. http://localhost:8191
}
