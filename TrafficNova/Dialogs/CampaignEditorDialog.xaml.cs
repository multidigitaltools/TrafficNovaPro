using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using TrafficNova.Core.Helpers;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Dialogs;

public partial class CampaignEditorDialog : Window
{
    private readonly ICampaignService _service;
    private readonly Campaign _campaign;
    private readonly bool _isEdit;

    // ── Preset support ────────────────────────────────────
    public bool     IsNewCampaign  => !_isEdit;
    public string[] PresetNames    { get; } =
        ["(no preset — blank)", ..CampaignPresets.All.Select(p => p.Name)];

    // ── Bindable editor model ─────────────────────────────
    // NOTE: must NOT be called "Name" — that collides with
    // FrameworkElement.Name (a DependencyProperty), and WPF binding would
    // bind to the element name instead of this property.
    public string CampaignName       { get; set; } = string.Empty;
    public string TargetUrlsText     { get; set; } = string.Empty;
    public int    ThreadCount        { get; set; } = 5;
    public int    VisitTarget        { get; set; } = 100;
    public int    WarmupVisits       { get; set; } = 0;

    // Dwell stored as ms in model, displayed as seconds in UI
    public int    DwellMinSec        { get; set; } = 15;
    public int    DwellMaxSec        { get; set; } = 60;

    public double BounceRate         { get; set; } = 0.3;
    public string BounceRateDisplay  => $"{BounceRate:P0}";

    public string[] ReferrerModes    { get; } = Enum.GetNames<ReferrerMode>();
    public string   SelectedReferrerMode { get; set; } = nameof(ReferrerMode.None);
    public bool     IsCustomReferrer  => SelectedReferrerMode == nameof(ReferrerMode.Custom);
    public bool     IsSearchReferrer  => SelectedReferrerMode == nameof(ReferrerMode.Google)
                                      || SelectedReferrerMode == nameof(ReferrerMode.Bing);
    public string   CustomReferrer    { get; set; } = string.Empty;
    public string   ReferrerKeywords  { get; set; } = string.Empty;

    public string[] UserAgentModes   { get; } = Enum.GetNames<UserAgentMode>();
    public string   SelectedUserAgentMode { get; set; } = nameof(UserAgentMode.Desktop);
    public bool     IsCustomUA       => SelectedUserAgentMode == nameof(UserAgentMode.Custom);
    public string   CustomUserAgent  { get; set; } = string.Empty;

    public string[] WindowSizes      { get; } =
        ["1920x1080", "1680x1050", "1440x900", "1366x768", "1280x800", "390x844", "375x812"];

    public string   WindowSize       { get; set; } = "1920x1080";

    public string[] Languages        { get; } =
        ["en-US", "en-GB", "de-DE", "fr-FR", "es-ES", "it-IT", "pt-BR", "ja-JP", "zh-CN", "ru-RU"];

    public string   BrowserLanguage  { get; set; } = "en-US";

    public string[] Timezones        { get; } =
        ["America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles",
         "Europe/London", "Europe/Paris", "Europe/Berlin", "Asia/Tokyo", "Asia/Shanghai",
         "Australia/Sydney"];

    public string   Timezone         { get; set; } = "America/New_York";
    public bool     JavaScriptEnabled { get; set; } = true;
    public bool     AcceptCookies    { get; set; } = true;

    // Phase 10 — resource/bandwidth blocking
    public string[] ResourceBlockModes { get; } = Enum.GetNames<Core.Models.ResourceBlockMode>();
    public string   ResourceBlockMode  { get; set; } = nameof(Core.Models.ResourceBlockMode.None);

    // ── Advanced (Step 97, 99) ─────────────────────────────
    public string[] GeoCountries     { get; } = ["(none)", ..GeoConfig.SupportedCountries];
    public string   GeoCountry       { get; set; } = "(none)";
    public string   CustomHeadersText { get; set; } = string.Empty;

    public bool     UseProxy         { get; set; } = false;
    public List<string> ProxyGroups  { get; } = new();
    public string   ProxyGroupTag    { get; set; } = string.Empty;

    public string[] RotationStrategies { get; } = Enum.GetNames<RotationStrategy>();
    public string   SelectedRotationStrategy { get; set; } = nameof(RotationStrategy.RoundRobin);

    // ─────────────────────────────────────────────────────

    public CampaignEditorDialog(Campaign? existing = null)
    {
        _service  = App.Services.GetRequiredService<ICampaignService>();
        _isEdit   = existing is not null;
        _campaign = existing ?? new Campaign();

        LoadProxyGroups();

        if (_isEdit)
            PopulateFromModel(_campaign);

        InitializeComponent();
        DataContext = this;

        if (_isEdit)
        {
            TitleText.Text    = "Edit Campaign";
            SubtitleText.Text = _campaign.Name;
        }
    }

    private void PopulateFromModel(Campaign c)
    {
        CampaignName  = c.Name;
        ThreadCount   = c.ThreadCount;
        VisitTarget   = c.VisitTarget;
        WarmupVisits  = c.WarmupVisits;
        DwellMinSec   = c.DwellMin / 1000;
        DwellMaxSec   = c.DwellMax / 1000;
        BounceRate    = c.BounceRate;
        CustomUserAgent = c.CustomUserAgent;
        WindowSize    = c.WindowSize;
        BrowserLanguage = c.BrowserLanguage;
        Timezone      = c.Timezone;
        JavaScriptEnabled = c.JavaScriptEnabled;
        AcceptCookies = c.AcceptCookies;
        ResourceBlockMode = c.ResourceBlockMode.ToString();
        UseProxy      = c.UseProxy;
        ProxyGroupTag = c.ProxyGroupTag;
        SelectedReferrerMode   = c.ReferrerMode.ToString();
        CustomReferrer         = c.CustomReferrer;
        ReferrerKeywords       = c.ReferrerKeywords;
        SelectedUserAgentMode  = c.UserAgentMode.ToString();
        SelectedRotationStrategy = c.ProxyRotation.ToString();
        GeoCountry = string.IsNullOrEmpty(c.GeoCountry) ? "(none)" : c.GeoCountry;
        CustomHeadersText = HeadersJsonToText(c.CustomHeadersJson);

        // Convert JSON URL array to newline-delimited text
        try
        {
            var urls = JsonConvert.DeserializeObject<string[]>(c.TargetUrlsJson);
            TargetUrlsText = urls is { Length: > 0 }
                ? string.Join(Environment.NewLine, urls)
                : string.Empty;
        }
        catch
        {
            TargetUrlsText = string.Empty;
        }
    }

    private void LoadProxyGroups()
    {
        try
        {
            var proxyService = App.Services.GetRequiredService<IProxyService>();
            ProxyGroups.Clear();
            ProxyGroups.Add(string.Empty); // "all proxies"
            foreach (var g in proxyService.GetGroups())
                ProxyGroups.Add(g);
        }
        catch { /* service not ready yet — non-fatal */ }
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        var idx = cb.SelectedIndex - 1; // -1 because index 0 = "no preset"
        if (idx < 0 || idx >= CampaignPresets.All.Count) return;

        var preset = CampaignPresets.All[idx];
        var t = preset.Template;

        // Populate all fields from preset (URL list stays empty — user must fill)
        CampaignName = t.Name;
        ThreadCount  = t.ThreadCount;
        VisitTarget  = t.VisitTarget;
        WarmupVisits = t.WarmupVisits;
        DwellMinSec  = t.DwellMin / 1000;
        DwellMaxSec  = t.DwellMax / 1000;
        BounceRate   = t.BounceRate;
        UseProxy     = t.UseProxy;
        SelectedReferrerMode  = t.ReferrerMode.ToString();
        CustomReferrer        = t.CustomReferrer;
        ReferrerKeywords      = t.ReferrerKeywords;
        SelectedUserAgentMode = t.UserAgentMode.ToString();
        SelectedRotationStrategy = t.ProxyRotation.ToString();
        WindowSize   = t.WindowSize;
        BrowserLanguage = t.BrowserLanguage;
        Timezone     = t.Timezone;
        JavaScriptEnabled = t.JavaScriptEnabled;
        AcceptCookies = t.AcceptCookies;
        ResourceBlockMode = t.ResourceBlockMode.ToString();

        // Refresh bindings — DataContext is this, so refresh entire dialog
        DataContext = null;
        DataContext = this;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ValidationMsg.Visibility = Visibility.Collapsed;

        var name = CampaignName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            ShowError("Campaign name is required.");
            return;
        }

        var urls = (TargetUrlsText ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();

        if (urls.Length == 0)
        {
            ShowError("At least one valid URL is required (must start with http).");
            return;
        }

        if (DwellMinSec > DwellMaxSec)
        {
            ShowError("Dwell Min must not exceed Dwell Max.");
            return;
        }

        _campaign.Name           = name;
        _campaign.TargetUrlsJson = JsonConvert.SerializeObject(urls);
        _campaign.ThreadCount    = Math.Clamp(ThreadCount, 1, 100);
        _campaign.VisitTarget    = Math.Max(0, VisitTarget);
        _campaign.WarmupVisits   = Math.Max(0, WarmupVisits);
        _campaign.DwellMin       = DwellMinSec * 1000;
        _campaign.DwellMax       = DwellMaxSec * 1000;
        _campaign.BounceRate     = Math.Clamp(BounceRate, 0, 1);
        _campaign.CustomReferrer    = CustomReferrer?.Trim() ?? string.Empty;
        _campaign.ReferrerKeywords  = ReferrerKeywords?.Trim() ?? string.Empty;
        _campaign.CustomUserAgent = CustomUserAgent?.Trim() ?? string.Empty;
        _campaign.WindowSize     = WindowSize;
        _campaign.BrowserLanguage = BrowserLanguage;
        _campaign.Timezone       = Timezone;
        _campaign.JavaScriptEnabled = JavaScriptEnabled;
        _campaign.AcceptCookies  = AcceptCookies;
        if (Enum.TryParse<Core.Models.ResourceBlockMode>(ResourceBlockMode, out var rbm))
            _campaign.ResourceBlockMode = rbm;
        _campaign.UseProxy       = UseProxy;
        _campaign.ProxyGroupTag  = ProxyGroupTag?.Trim() ?? string.Empty;

        if (Enum.TryParse<ReferrerMode>(SelectedReferrerMode, out var rm))
            _campaign.ReferrerMode = rm;
        if (Enum.TryParse<UserAgentMode>(SelectedUserAgentMode, out var ua))
            _campaign.UserAgentMode = ua;
        // DeviceType drives the UA pool + viewport in the engine — keep it in
        // sync with the selected mode, or Mobile/Tablet campaigns run as
        // Desktop. (Custom has no DeviceType, so it is left unchanged.)
        if (Enum.TryParse<DeviceType>(SelectedUserAgentMode, out var dt))
            _campaign.DeviceType = dt;
        if (Enum.TryParse<RotationStrategy>(SelectedRotationStrategy, out var rs))
            _campaign.ProxyRotation = rs;

        _campaign.GeoCountry       = GeoCountry == "(none)" ? string.Empty : GeoCountry;
        _campaign.CustomHeadersJson = HeadersTextToJson(CustomHeadersText);

        try
        {
            if (_isEdit)
                await _service.UpdateAsync(_campaign);
            else
                await _service.CreateAsync(_campaign);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Save failed: {ex.Message}");
        }
    }

    // Phase 10 — build a transient Campaign from the form (no persistence)
    private Campaign? BuildProbeCampaign(out string? error)
    {
        error = null;
        var name = CampaignName?.Trim() ?? string.Empty;
        var urls = (TargetUrlsText ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Distinct().ToArray();
        if (urls.Length == 0)
        {
            error = "Enter at least one valid URL (must start with http) before testing.";
            return null;
        }

        var c = new Campaign
        {
            Name           = string.IsNullOrEmpty(name) ? "Test" : name,
            TargetUrlsJson = JsonConvert.SerializeObject(urls),
            DwellMin       = DwellMinSec * 1000,
            DwellMax       = Math.Max(DwellMinSec, DwellMaxSec) * 1000,
            BounceRate     = 0.0,
            CustomReferrer = CustomReferrer?.Trim() ?? string.Empty,
            ReferrerKeywords = ReferrerKeywords?.Trim() ?? string.Empty,
            CustomUserAgent = CustomUserAgent?.Trim() ?? string.Empty,
            WindowSize     = WindowSize,
            BrowserLanguage = BrowserLanguage,
            Timezone       = Timezone,
            JavaScriptEnabled = JavaScriptEnabled,
            AcceptCookies  = AcceptCookies,
            GeoCountry     = GeoCountry == "(none)" ? string.Empty : GeoCountry,
        };
        if (Enum.TryParse<ReferrerMode>(SelectedReferrerMode, out var rm)) c.ReferrerMode = rm;
        if (Enum.TryParse<UserAgentMode>(SelectedUserAgentMode, out var ua)) c.UserAgentMode = ua;
        if (Enum.TryParse<DeviceType>(SelectedUserAgentMode, out var dt))    c.DeviceType    = dt;
        if (Enum.TryParse<Core.Models.ResourceBlockMode>(ResourceBlockMode, out var rbm))
            c.ResourceBlockMode = rbm;
        return c;
    }

    private async void TestRun_Click(object sender, RoutedEventArgs e)
    {
        ValidationMsg.Visibility = Visibility.Collapsed;
        var probe = BuildProbeCampaign(out var err);
        if (probe is null) { ShowError(err ?? "Cannot test."); return; }

        TestRunButton.IsEnabled = false;
        var originalText = TestRunButton.Content;
        TestRunButton.Content = "Testing…";
        try
        {
            var pool     = App.Services.GetRequiredService<ISessionPoolService>();
            var settings = App.Services.GetRequiredService<IAppSettingsService>();
            var engine   = Core.Models.EngineConfig.FromSettings(settings.Current);

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(90));
            var r = await pool.RunSingleVisitAsync(probe, engine, cts.Token);

            var summary =
                $"Result: {(r.Success ? "✅ SUCCESS" : "❌ FAILED")}\n" +
                $"HTTP status: {(r.StatusCode?.ToString() ?? "—")}\n" +
                $"Dwell: {r.DwellMs} ms\n" +
                $"Blocked requests: {r.BlockedRequests}\n" +
                $"Duration: {(r.EndedAt - r.StartedAt).TotalSeconds:F1}s\n" +
                $"User-Agent: {Trim(r.UserAgent, 80)}\n" +
                (string.IsNullOrEmpty(r.Referrer) ? "" : $"Referrer: {Trim(r.Referrer, 80)}\n") +
                (string.IsNullOrEmpty(r.ErrorMessage) ? "" : $"\nError: {r.ErrorMessage}\n");

            if (!string.IsNullOrEmpty(r.ScreenshotPath) && File.Exists(r.ScreenshotPath))
            {
                var open = MessageBox.Show(
                    summary + "\nAn error screenshot was captured. Open it now?",
                    "Test Run Result", MessageBoxButton.YesNo,
                    r.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
                if (open == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(r.ScreenshotPath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(summary, "Test Run Result", MessageBoxButton.OK,
                    r.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            ShowError("Test run timed out after 90 seconds.");
        }
        catch (Exception ex)
        {
            ShowError($"Test run failed: {ex.Message}");
        }
        finally
        {
            TestRunButton.Content   = originalText;
            TestRunButton.IsEnabled = true;
        }
    }

    private static string Trim(string s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max] + "…");

    // Plain (non-INotifyPropertyChanged) DataContext: the conditional input
    // fields won't re-show on mode change unless we refresh them ourselves.
    // Defer to Background priority so the two-way binding writes the
    // Selected*Mode property before we read it.
    private void ConditionalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => Dispatcher.BeginInvoke(new Action(RefreshConditionalFields),
                                  DispatcherPriority.Background);

    private void RefreshConditionalFields()
    {
        var customRef = Vis(IsCustomReferrer);
        CustomReferrerLabel.Visibility = customRef;
        CustomReferrerBox.Visibility   = customRef;

        var searchRef = Vis(IsSearchReferrer);
        SearchKeywordsLabel.Visibility = searchRef;
        SearchKeywordsBox.Visibility   = searchRef;

        var customUa = Vis(IsCustomUA);
        CustomUaLabel.Visibility = customUa;
        CustomUaBox.Visibility   = customUa;

        static Visibility Vis(bool on)
            => on ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string msg)
    {
        ValidationMsg.Text = msg;
        ValidationMsg.Visibility = Visibility.Visible;
    }

    // Convert JSON {"Name":"Value"} dict → "Name: Value\nName2: Value2" text
    private static string HeadersJsonToText(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return string.Empty;
        try
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            return dict is null ? string.Empty
                : string.Join(Environment.NewLine, dict.Select(kv => $"{kv.Key}: {kv.Value}"));
        }
        catch { return string.Empty; }
    }

    // Convert "Name: Value\n..." text → JSON dict (skip invalid lines & forbidden headers)
    private static string HeadersTextToJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "content-length", "host", "transfer-encoding" };
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var name = line[..idx].Trim();
            var val  = line[(idx + 1)..].Trim();
            if (!string.IsNullOrEmpty(name) && !forbidden.Contains(name))
                dict[name] = val;
        }
        return JsonConvert.SerializeObject(dict);
    }
}
