using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;
using TrafficNova.Services;

namespace TrafficNova.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _service;
    private readonly ThemeService        _themeService;

    // ── Theme ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _theme = "Light";
    public string[] ThemeOptions { get; } = ["Light", "Dark"];

    partial void OnThemeChanged(string value)
    {
        _themeService.Apply(value);
        // BUG-049: Theme was applied to the UI but never written to disk until
        // the user explicitly clicked Save. App restart loaded the old theme.
        // Persist immediately so the chosen theme survives a restart.
        _service.Current.Theme = value;
        _ = _service.SaveAsync();
    }

    // ── General ───────────────────────────────────────────────────────
    [ObservableProperty] private bool   _launchOnStartup;
    [ObservableProperty] private bool   _minimizeToTrayOnClose;
    [ObservableProperty] private bool   _checkForUpdatesOnStartup;

    // ── Browser ───────────────────────────────────────────────────────
    [ObservableProperty] private bool   _defaultHeadlessMode;
    [ObservableProperty] private string _browserExecutablePath = string.Empty;
    [ObservableProperty] private int    _maxConcurrentSessions;
    [ObservableProperty] private int    _defaultSessionTimeoutSeconds;
    [ObservableProperty] private string _stealthIntensity = "Medium";
    public string[] StealthOptions { get; } = ["Low", "Medium", "High"];

    // ── Proxy ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _autoTestProxiesOnImport;
    [ObservableProperty] private int    _proxyHealthCheckIntervalMinutes;
    [ObservableProperty] private int    _proxyDeadThreshold;
    [ObservableProperty] private string _proxyTestUrl = string.Empty;

    // ── Scheduler ─────────────────────────────────────────────────────
    [ObservableProperty] private bool   _enableScheduler;
    [ObservableProperty] private int    _maxSimultaneousCampaigns;
    [ObservableProperty] private string _schedulerTimezone = "UTC";

    // ── Notifications ─────────────────────────────────────────────────
    [ObservableProperty] private bool   _enableDesktopNotifications;
    [ObservableProperty] private int    _proxyLowAlertThreshold;
    [ObservableProperty] private double _errorRateAlertThreshold;

    // ── Logging ───────────────────────────────────────────────────────
    [ObservableProperty] private string _logLevel = "Information";
    [ObservableProperty] private int    _logRetentionDays;
    [ObservableProperty] private int    _maxLogFileSizeMb;
    public string[] LogLevels { get; } = ["Debug", "Information", "Warning", "Error"];

    // ── Dashboard ─────────────────────────────────────────────────────
    [ObservableProperty] private int    _dashboardRefreshSeconds;

    // ── License ───────────────────────────────────────────────────────
    [ObservableProperty] private string _licenseKey    = string.Empty;
    [ObservableProperty] private string _licenseStatus = "Trial";
    [ObservableProperty] private string _machineId     = string.Empty;

    // ── Advanced bypass (Step 100) ────────────────────────────────────
    [ObservableProperty] private string _flareSolverrUrl = string.Empty;

    // ── Status bar ────────────────────────────────────────────────────
    [ObservableProperty] private string _statusMessage = string.Empty;

    public SettingsViewModel(IAppSettingsService service, ThemeService themeService)
    {
        _service      = service;
        _themeService = themeService;
        Load();
    }

    private void Load()
    {
        var s = _service.Current;
        Theme                            = s.Theme;
        LaunchOnStartup                  = s.LaunchOnStartup;
        MinimizeToTrayOnClose            = s.MinimizeToTrayOnClose;
        CheckForUpdatesOnStartup         = s.CheckForUpdatesOnStartup;
        DefaultHeadlessMode              = s.DefaultHeadlessMode;
        BrowserExecutablePath            = s.BrowserExecutablePath;
        MaxConcurrentSessions            = s.MaxConcurrentSessions;
        DefaultSessionTimeoutSeconds     = s.DefaultSessionTimeoutSeconds;
        StealthIntensity                 = s.StealthIntensity;
        AutoTestProxiesOnImport          = s.AutoTestProxiesOnImport;
        ProxyHealthCheckIntervalMinutes  = s.ProxyHealthCheckIntervalMinutes;
        ProxyDeadThreshold               = s.ProxyDeadThreshold;
        ProxyTestUrl                     = s.ProxyTestUrl;
        EnableScheduler                  = s.EnableScheduler;
        MaxSimultaneousCampaigns         = s.MaxSimultaneousCampaigns;
        SchedulerTimezone                = s.SchedulerTimezone;
        EnableDesktopNotifications       = s.EnableDesktopNotifications;
        ProxyLowAlertThreshold           = s.ProxyLowAlertThreshold;
        ErrorRateAlertThreshold          = s.ErrorRateAlertThreshold;
        LogLevel                         = s.LogLevel;
        LogRetentionDays                 = s.LogRetentionDays;
        MaxLogFileSizeMb                 = s.MaxLogFileSizeMb;
        DashboardRefreshSeconds          = s.DashboardRefreshSeconds;
        LicenseKey                       = s.LicenseKey;
        // BUG-086: was a binary "Activated" / "Trial" label; now shows the
        // real trial countdown (e.g. "Trial — 9 days left") or "Trial expired".
        LicenseStatus                    = TrafficNova.Core.Licensing.TrialState
                                              .FormatStatus(s, DateTime.UtcNow);
        MachineId                        = ComputeMachineId();
        FlareSolverrUrl                  = s.FlareSolverrUrl;
    }

    // BUG-050 / BUG-065 / BUG-072: User-editable URLs (ProxyTestUrl, FlareSolverrUrl)
    // are passed directly to HttpClient. Private/loopback/link-local values allow SSRF
    // probing of the local network. Logic lives in Core.Net.UrlSafety so it is shared
    // and unit-tested; this thin wrapper keeps the call sites readable.
    private static bool IsPrivateOrLoopback(string url) =>
        TrafficNova.Core.Net.UrlSafety.IsPrivateOrLoopback(url);

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (IsPrivateOrLoopback(ProxyTestUrl))
        {
            MessageBox.Show(
                "Proxy Test URL must not point to a private or loopback address.",
                "Invalid Setting", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // BUG-065: FlareSolverrUrl was saved without SSRF validation
        if (!string.IsNullOrWhiteSpace(FlareSolverrUrl) && IsPrivateOrLoopback(FlareSolverrUrl))
        {
            MessageBox.Show(
                "FlareSolverr URL must not point to a private or loopback address.",
                "Invalid Setting", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var s = _service.Current;
        s.Theme                            = Theme;
        s.LaunchOnStartup                  = LaunchOnStartup;
        s.MinimizeToTrayOnClose            = MinimizeToTrayOnClose;
        s.CheckForUpdatesOnStartup         = CheckForUpdatesOnStartup;
        s.DefaultHeadlessMode              = DefaultHeadlessMode;
        s.BrowserExecutablePath            = BrowserExecutablePath;
        s.MaxConcurrentSessions            = MaxConcurrentSessions;
        s.DefaultSessionTimeoutSeconds     = DefaultSessionTimeoutSeconds;
        s.StealthIntensity                 = StealthIntensity;
        s.AutoTestProxiesOnImport          = AutoTestProxiesOnImport;
        s.ProxyHealthCheckIntervalMinutes  = ProxyHealthCheckIntervalMinutes;
        s.ProxyDeadThreshold               = ProxyDeadThreshold;
        s.ProxyTestUrl                     = ProxyTestUrl;
        s.EnableScheduler                  = EnableScheduler;
        s.MaxSimultaneousCampaigns         = MaxSimultaneousCampaigns;
        s.SchedulerTimezone                = SchedulerTimezone;
        s.EnableDesktopNotifications       = EnableDesktopNotifications;
        s.ProxyLowAlertThreshold           = ProxyLowAlertThreshold;
        s.ErrorRateAlertThreshold          = ErrorRateAlertThreshold;
        s.LogLevel                         = LogLevel;
        s.LogRetentionDays                 = LogRetentionDays;
        s.MaxLogFileSizeMb                 = MaxLogFileSizeMb;
        s.DashboardRefreshSeconds          = DashboardRefreshSeconds;
        s.LicenseKey                       = LicenseKey;
        s.IsActivated                      = LicenseStatus == "Activated";
        s.FlareSolverrUrl                  = FlareSolverrUrl;
        await _service.SaveAsync();
        StatusMessage = $"Saved at {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    public async Task ResetDefaultsAsync()
    {
        var r = MessageBox.Show("Reset all settings to factory defaults?",
            "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        _service.ResetToDefaults();
        await _service.SaveAsync();
        Load();
        StatusMessage = "Settings reset to defaults.";
    }

    [RelayCommand]
    public async Task ExportSettingsAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title    = "Export Settings",
            Filter   = "JSON files|*.json",
            FileName = "trafficnova_settings.json",
        };
        if (dlg.ShowDialog() != true) return;
        var json = JsonConvert.SerializeObject(_service.Current, Formatting.Indented);
        await File.WriteAllTextAsync(dlg.FileName, json);
        StatusMessage = $"Exported to {dlg.FileName}";
    }

    [RelayCommand]
    public async Task ImportSettingsAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Import Settings",
            Filter = "JSON files|*.json",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json     = await File.ReadAllTextAsync(dlg.FileName);
            var imported = JsonConvert.DeserializeObject<AppSettings>(json)
                           ?? throw new InvalidOperationException("Invalid settings file.");
            // BUG-066: reflection copy bypassed all URL validation — validate before applying
            if (IsPrivateOrLoopback(imported.ProxyTestUrl))
                throw new InvalidOperationException("Imported ProxyTestUrl points to a private or loopback address.");
            if (!string.IsNullOrWhiteSpace(imported.FlareSolverrUrl) && IsPrivateOrLoopback(imported.FlareSolverrUrl))
                throw new InvalidOperationException("Imported FlareSolverrUrl points to a private or loopback address.");
            foreach (var prop in typeof(AppSettings).GetProperties().Where(p => p.CanWrite))
                prop.SetValue(_service.Current, prop.GetValue(imported));
            await _service.SaveAsync();
            Load();
            StatusMessage = "Settings imported.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void OpenLogFolder()
    {
        var dir = _service.Current.LogDirectory;
        if (Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", dir);
    }

    [RelayCommand]
    public async Task ActivateLicenseAsync()
    {
        if (string.IsNullOrWhiteSpace(LicenseKey)) { StatusMessage = "Enter a license key first."; return; }
        // BUG-086: previously this only set the in-memory label, so the
        // trial-status code path (which reads AppSettings.IsActivated) still
        // showed "Trial — N days left" even after the user clicked Activate.
        // Persist immediately so About/Settings/notifications all agree.
        _service.Current.LicenseKey  = LicenseKey;
        _service.Current.IsActivated = true;
        await _service.SaveAsync();
        LicenseStatus = TrafficNova.Core.Licensing.TrialState
                            .FormatStatus(_service.Current, DateTime.UtcNow);
        StatusMessage = "License activated.";
    }

    [RelayCommand]
    public void SendSummaryEmail() =>
        MessageBox.Show("Email summary feature coming in a future update.",
            "Not Available", MessageBoxButton.OK, MessageBoxImage.Information);

    [RelayCommand]
    public void BrowseBrowserPath()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Chrome Executable",
            Filter = "Executables|chrome.exe;chromium.exe|All files|*.*",
        };
        if (dlg.ShowDialog() == true)
            BrowserExecutablePath = dlg.FileName;
    }

    private static string ComputeMachineId()
    {
        try
        {
            var raw   = Environment.MachineName + Environment.UserName + Environment.OSVersion.VersionString;
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes)[..16];
        }
        catch { return "UNKNOWN"; }
    }
}
