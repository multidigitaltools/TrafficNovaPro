using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.ViewModels;

public partial class SessionLogViewModel : ObservableObject
{
    private readonly IStatsService    _stats;
    private readonly ICampaignService _campaigns;
    private List<TrafficSession>      _all = new();

    // campaign id → name lookup used by the filter and the display converter
    public Dictionary<int, string> CampaignNamesById { get; } = new();

    [ObservableProperty] private ObservableCollection<TrafficSession> _sessions = new();
    [ObservableProperty] private string  _filterStatus   = "All";
    [ObservableProperty] private string? _filterCampaign;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _toDate   = DateTime.Today.AddDays(1);
    [ObservableProperty] private int  _pageIndex   = 0;
    [ObservableProperty] private int  _totalPages  = 1;
    [ObservableProperty] private int  _totalCount;
    [ObservableProperty] private ObservableCollection<string> _campaignNames = new();

    public string[] StatusFilters { get; } = ["All", "Success", "Failed"];
    private const int PageSize = 100;

    public SessionLogViewModel(IStatsService stats, ICampaignService campaigns)
    {
        _stats     = stats;
        _campaigns = campaigns;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            var camps = await _campaigns.GetAllAsync();

            CampaignNamesById.Clear();
            foreach (var c in camps) CampaignNamesById[c.Id] = c.Name;

            CampaignNames.Clear();
            CampaignNames.Add("All Campaigns");
            foreach (var c in camps) CampaignNames.Add(c.Name);

            // Single query across all campaigns (BUG-033 fix — no N+1).
            _all = (await _campaigns.GetAllRecentSessionsAsync(2000)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            // BUG-055: DB failure silently left session list blank with no feedback
            Serilog.Log.Error(ex, "Failed to load session log");
            MessageBox.Show("Could not load session log:\n\n" + ex.Message,
                "Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    partial void OnFilterStatusChanged(string value)    => ApplyFilter();
    partial void OnFilterCampaignChanged(string? value)  => ApplyFilter();
    partial void OnFromDateChanged(DateTime value)       => ApplyFilter();
    partial void OnToDateChanged(DateTime value)         => ApplyFilter();

    [RelayCommand] public void PreviousPage() { if (PageIndex > 0) { PageIndex--; RenderPage(); } }
    [RelayCommand] public void NextPage()     { if (PageIndex < TotalPages - 1) { PageIndex++; RenderPage(); } }

    private void ApplyFilter()
    {
        var filtered = _all.AsEnumerable();

        if (FilterStatus == "Success") filtered = filtered.Where(s => s.Success);
        if (FilterStatus == "Failed")  filtered = filtered.Where(s => !s.Success);

        // Fix: resolve selected campaign name → id, then filter by that id
        if (!string.IsNullOrEmpty(FilterCampaign) && FilterCampaign != "All Campaigns")
        {
            var matchId = CampaignNamesById
                .FirstOrDefault(kv => kv.Value == FilterCampaign).Key;
            filtered = filtered.Where(s => s.CampaignId == matchId);
        }

        filtered = filtered.Where(s =>
            s.StartedAt >= FromDate && s.StartedAt < ToDate.AddDays(1));

        var list   = filtered.ToList();
        TotalCount = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        PageIndex  = Math.Min(PageIndex, TotalPages - 1);

        Sessions = new ObservableCollection<TrafficSession>(
            list.Skip(PageIndex * PageSize).Take(PageSize));
    }

    private void RenderPage() => ApplyFilter();

    [RelayCommand]
    public async Task ExportCsvAsync()
    {
        try
        {
            var exportSvc = App.Services.GetRequiredService<TrafficNova.Data.Services.ExportService>();
            var path = await exportSvc.ExportSessionsToCsvAsync(Sessions.ToList());
            MessageBox.Show($"Exported to:\n{path}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "CSV export failed");
            MessageBox.Show("Export failed:\n\n" + ex.Message,
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
