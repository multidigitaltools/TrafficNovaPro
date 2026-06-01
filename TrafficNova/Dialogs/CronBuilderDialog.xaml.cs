using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;
using TrafficNova.Data.Services;

namespace TrafficNova.Dialogs;

public partial class CronBuilderDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Binding properties ─────────────────────────────────────────────

    public string WindowTitle { get; }

    public ObservableCollection<Campaign> Campaigns { get; }

    private Campaign? _selectedCampaign;
    public Campaign? SelectedCampaign
    {
        get => _selectedCampaign;
        set { _selectedCampaign = value; Notify(nameof(SelectedCampaign)); }
    }

    private string _cronExpression = "0 9 * * *";
    public string CronExpression
    {
        get => _cronExpression;
        set { _cronExpression = value; Notify(nameof(CronExpression)); RefreshPreview(); }
    }

    private string _nextRunsText = string.Empty;
    public string NextRunsText
    {
        get => _nextRunsText;
        private set { _nextRunsText = value; Notify(nameof(NextRunsText)); }
    }

    private string _validationMessage = string.Empty;
    public string ValidationMessage
    {
        get => _validationMessage;
        private set { _validationMessage = value; Notify(nameof(ValidationMessage)); }
    }

    // ── Output ──────────────────────────────────────────────────────────
    public ScheduledJob? ResultJob { get; private set; }

    // Existing job being edited (null = new)
    private readonly ScheduledJob? _existing;

    // Cron schedules are interpreted by SchedulerService in the configured
    // scheduler timezone — the preview and the saved NextRunAt must use the
    // same tz, or a new job's first run (and the preview) is off by the offset.
    private readonly TimeZoneInfo _schedulerTz = ResolveSchedulerTz();

    private static TimeZoneInfo ResolveSchedulerTz()
    {
        try
        {
            var settings = App.Services.GetRequiredService<IAppSettingsService>();
            return TimeZoneInfo.FindSystemTimeZoneById(settings.Current.SchedulerTimezone);
        }
        catch { return TimeZoneInfo.Utc; }
    }

    public CronBuilderDialog(ObservableCollection<Campaign> campaigns, ScheduledJob? existing = null)
    {
        Campaigns  = campaigns;
        _existing  = existing;
        WindowTitle = existing is null ? "Add Scheduled Job" : "Edit Scheduled Job";

        InitializeComponent();
        DataContext = this;

        if (existing is not null)
        {
            SelectedCampaign = campaigns.FirstOrDefault(c => c.Id == existing.CampaignId);
            CronExpression   = existing.CronExpression;
        }
        else if (campaigns.Count > 0)
        {
            SelectedCampaign = campaigns[0];
        }

        RefreshPreview();
    }

    // ── Event handlers ─────────────────────────────────────────────────

    private void OnPreset(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cron)
            CronExpression = cron;
    }

    private void OnCronChanged(object sender, TextChangedEventArgs e)
    {
        // CronExpression is two-way bound; preview updates via setter
    }

    private void OnValidate(object sender, RoutedEventArgs e)
    {
        RefreshPreview(showResult: true);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (SelectedCampaign is null)
        {
            ValidationMessage = "Please select a campaign.";
            return;
        }

        if (!TryParseCron(CronExpression, out _, out var err))
        {
            ValidationMessage = err;
            return;
        }

        var tz       = _schedulerTz;
        var nextRun  = SchedulerService.ComputeNextRun(CronExpression, tz);

        ResultJob = _existing ?? new ScheduledJob();
        ResultJob.CampaignId     = SelectedCampaign.Id;
        ResultJob.CronExpression = CronExpression.Trim();
        ResultJob.NextRunAt      = nextRun;
        ResultJob.IsEnabled      = _existing?.IsEnabled ?? true;

        // Attach navigation property so UI shows name immediately
        ResultJob.Campaign = SelectedCampaign;

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    // ── Preview ─────────────────────────────────────────────────────────

    private void RefreshPreview(bool showResult = false)
    {
        if (!TryParseCron(CronExpression, out var expr, out var err))
        {
            NextRunsText      = "(invalid expression)";
            ValidationMessage = showResult ? err : string.Empty;
            return;
        }

        ValidationMessage = showResult ? "Valid cron expression." : string.Empty;

        var tz   = _schedulerTz;
        var next = DateTime.UtcNow;
        var sb   = new System.Text.StringBuilder();

        for (int i = 0; i < 5; i++)
        {
            var occ = expr!.GetNextOccurrence(next, tz);
            if (!occ.HasValue) break;
            sb.AppendLine(TimeZoneInfo.ConvertTimeFromUtc(occ.Value, tz).ToString("ddd, yyyy-MM-dd  HH:mm"));
            next = occ.Value;
        }

        NextRunsText = sb.Length > 0 ? sb.ToString().TrimEnd() : "(no upcoming runs)";
    }

    private static bool TryParseCron(string expr, out CronExpression? result, out string error)
    {
        result = null;
        error  = string.Empty;
        if (string.IsNullOrWhiteSpace(expr))
        {
            error = "Cron expression is empty.";
            return false;
        }
        try
        {
            result = Cronos.CronExpression.Parse(expr.Trim(), CronFormat.Standard);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Invalid: {ex.Message}";
            return false;
        }
    }
}
