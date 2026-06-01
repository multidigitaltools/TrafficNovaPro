using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.ViewModels;

public partial class CampaignsViewModel : ObservableObject
{
    private readonly ICampaignService _campaigns;
    private List<Campaign> _all = new();

    [ObservableProperty] private ObservableCollection<Campaign> _items = new();
    [ObservableProperty] private Campaign? _selected;
    [ObservableProperty] private string _filterStatus = "All";
    [ObservableProperty] private bool _isLoading;

    // Summary counts
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _runningCount;
    [ObservableProperty] private int _pausedCount;
    [ObservableProperty] private int _completedCount;

    public string[] StatusFilters { get; } = ["All", "Running", "Paused", "Completed", "Idle"];

    // ── Step 108: Bulk selection ──────────────────────────────────────
    public ObservableCollection<Campaign> SelectedItems { get; } = new();

    private readonly DispatcherTimer _progressTimer;

    public CampaignsViewModel(ICampaignService campaigns)
    {
        _campaigns = campaigns;
        // CampaignsChanged can fire from session-pool / scheduler / watchdog
        // background threads; LoadAsync rebuilds bound collections, so marshal
        // it onto the UI thread.
        _campaigns.CampaignsChanged += (_, _) =>
            Application.Current?.Dispatcher.Invoke(() => _ = LoadAsync());

        // Phase 10 — live progress refresh while campaigns are running.
        // BUG-056: timer was started unconditionally and never stopped, burning
        // CPU even when the Campaigns page was not visible. Start/Stop is now
        // controlled by the page via StartProgressTimer/StopProgressTimer.
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _progressTimer.Tick += (_, _) => RefreshProgress();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _all = (await _campaigns.GetAllAsync()).ToList();
            ApplyLiveStats();
            ApplyFilter();
            UpdateSummary();
        }
        catch (Exception ex)
        {
            // BUG-052: silent exception swallowed — user saw blank list with no feedback
            Serilog.Log.Error(ex, "Failed to load campaigns");
            MessageBox.Show("Could not load campaigns:\n\n" + ex.Message,
                "Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsLoading = false; }
    }

    // Phase 10 — overlay in-memory runtime counters onto the loaded campaigns
    private void ApplyLiveStats()
    {
        foreach (var c in _all)
        {
            var st = _campaigns.GetStats(c.Id);
            if (st is not null)
            {
                c.TotalVisits   = st.TotalVisits;
                c.SuccessVisits = st.SuccessVisits;
            }
        }
    }

    // Phase 10 — rebuild rows with fresh progress, preserving selection
    private void RefreshProgress()
    {
        if (_all.Count == 0 || _all.All(c => c.Status != CampaignStatus.Running))
            return;

        var selId = Selected?.Id;
        ApplyLiveStats();
        ApplyFilter();
        UpdateSummary();
        if (selId is not null)
            Selected = Items.FirstOrDefault(c => c.Id == selId.Value);
    }

    private void ApplyFilter()
    {
        var filtered = _all.AsEnumerable();
        if (FilterStatus != "All" && Enum.TryParse<CampaignStatus>(FilterStatus, out var s))
            filtered = filtered.Where(c => c.Status == s);
        Items = new ObservableCollection<Campaign>(filtered);
    }

    private void UpdateSummary()
    {
        TotalCount = _all.Count;
        RunningCount = _all.Count(c => c.Status == CampaignStatus.Running);
        PausedCount = _all.Count(c => c.Status == CampaignStatus.Paused);
        CompletedCount = _all.Count(c => c.Status == CampaignStatus.Completed);
    }

    partial void OnFilterStatusChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task StartAsync()
    {
        if (Selected is null) return;
        try { await _campaigns.StartAsync(Selected.Id); }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to start campaign {Id}", Selected.Id);
            MessageBox.Show("Could not start campaign:\n\n" + ex.Message,
                "Start Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        if (Selected is null) return;
        try { await _campaigns.StopAsync(Selected.Id); }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to stop campaign {Id}", Selected.Id);
            MessageBox.Show("Could not stop campaign:\n\n" + ex.Message,
                "Stop Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task PauseAsync()
    {
        if (Selected is null) return;
        try
        {
            if (Selected.Status == CampaignStatus.Paused)
                await _campaigns.ResumeAsync(Selected.Id);
            else
                await _campaigns.PauseAsync(Selected.Id);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to pause/resume campaign {Id}", Selected.Id);
            MessageBox.Show("Could not pause/resume campaign:\n\n" + ex.Message,
                "Pause Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (Selected is null) return;
        var msg = Selected.Status == CampaignStatus.Running
            ? $"Campaign '{Selected.Name}' is running. Stop and delete?"
            : $"Delete campaign '{Selected.Name}'?";
        var result = MessageBox.Show(msg, "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        try { await _campaigns.DeleteAsync(Selected.Id); }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to delete campaign {Id}", Selected.Id);
            MessageBox.Show("Could not delete campaign:\n\n" + ex.Message,
                "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task DuplicateAsync()
    {
        if (Selected is null) return;
        var copy = new Campaign
        {
            Name = $"Copy of {Selected.Name}",
            TargetUrlsJson = Selected.TargetUrlsJson,
            ThreadCount = Selected.ThreadCount,
            VisitTarget = Selected.VisitTarget,
            DwellMin = Selected.DwellMin,
            DwellMax = Selected.DwellMax,
            BounceRate = Selected.BounceRate,
            ReferrerMode = Selected.ReferrerMode,
            CustomReferrer = Selected.CustomReferrer,
            UserAgentMode = Selected.UserAgentMode,
            DeviceType = Selected.DeviceType,
            UseProxy = Selected.UseProxy,
            ProxyGroupTag = Selected.ProxyGroupTag
        };
        try
        {
            await _campaigns.CreateAsync(copy);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to duplicate campaign");
            MessageBox.Show("Could not duplicate campaign:\n\n" + ex.Message,
                "Duplicate Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task RefreshAsync() => await LoadAsync();

    public void StartProgressTimer() => _progressTimer.Start();
    public void StopProgressTimer()  => _progressTimer.Stop();

    // ── Step 108: Bulk actions ────────────────────────────────────────
    [RelayCommand]
    public async Task BulkStartAsync()
    {
        var targets = SelectedItems.Count > 0 ? SelectedItems.ToList() : Items.ToList();
        foreach (var c in targets.Where(c => c.Status != CampaignStatus.Running))
        {
            try { await _campaigns.StartAsync(c.Id); }
            catch (Exception ex) { Serilog.Log.Error(ex, "Bulk start failed for campaign {Id}", c.Id); }
        }
    }

    [RelayCommand]
    public async Task BulkStopAsync()
    {
        var targets = SelectedItems.Count > 0 ? SelectedItems.ToList() : Items.ToList();
        foreach (var c in targets.Where(c => c.Status == CampaignStatus.Running))
        {
            try { await _campaigns.StopAsync(c.Id); }
            catch (Exception ex) { Serilog.Log.Error(ex, "Bulk stop failed for campaign {Id}", c.Id); }
        }
    }

    [RelayCommand]
    public async Task BulkDeleteAsync()
    {
        var targets = SelectedItems.Count > 0 ? SelectedItems.ToList() : Items.ToList();
        if (targets.Count == 0) return;
        var msg = $"Delete {targets.Count} campaign(s)? This cannot be undone.";
        var r   = MessageBox.Show(msg, "Confirm Bulk Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        foreach (var c in targets)
        {
            try
            {
                if (c.Status == CampaignStatus.Running) await _campaigns.StopAsync(c.Id);
                await _campaigns.DeleteAsync(c.Id);
            }
            catch (Exception ex) { Serilog.Log.Error(ex, "Bulk delete failed for campaign {Id}", c.Id); }
        }
        SelectedItems.Clear();
    }

    // ── Step 107: Import / Export campaigns ──────────────────────────
    [RelayCommand]
    public async Task ExportCampaignsAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title    = "Export Campaigns",
            Filter   = "JSON files|*.json",
            FileName = "trafficnova_campaigns.json",
        };
        if (dlg.ShowDialog() != true) return;
        var all  = await _campaigns.GetAllAsync();
        var json = JsonConvert.SerializeObject(all, Formatting.Indented);
        await File.WriteAllTextAsync(dlg.FileName, json);
        MessageBox.Show($"Exported {all.Count} campaign(s) to:\n{dlg.FileName}",
            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public async Task ImportCampaignsAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Import Campaigns",
            Filter = "JSON files|*.json",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json     = await File.ReadAllTextAsync(dlg.FileName);
            var imported = JsonConvert.DeserializeObject<List<Campaign>>(json)
                           ?? throw new InvalidOperationException("Invalid campaigns file.");

            var existing = (await _campaigns.GetAllAsync()).Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int added = 0;
            foreach (var c in imported)
            {
                if (existing.Contains(c.Name)) continue;
                c.Id     = 0;
                c.Status = CampaignStatus.Idle;
                await _campaigns.CreateAsync(c);
                added++;
            }
            MessageBox.Show($"Imported {added} campaign(s) ({imported.Count - added} skipped — duplicate names).",
                "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
