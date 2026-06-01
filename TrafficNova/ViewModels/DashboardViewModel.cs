using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IStatsService _stats;
    private readonly DispatcherTimer _timer;

    // ── Stat cards ────────────────────────────────────────────────────
    [ObservableProperty] private int    _totalVisitsToday;
    [ObservableProperty] private int    _activeSessions;
    [ObservableProperty] private double _successRate;
    [ObservableProperty] private int    _proxiesOnline;
    [ObservableProperty] private bool   _isPaused;

    // ── Visits/hour line chart (Step 63) ─────────────────────────────
    [ObservableProperty] private ISeries[] _visitsPerHourSeries = [];
    [ObservableProperty] private Axis[]    _hourAxisX           = [];
    [ObservableProperty] private Axis[]    _hourAxisY           = [];

    // ── Success/fail pie chart (Step 64) ─────────────────────────────
    [ObservableProperty] private ISeries[] _successPieSeries = [];

    // ── Active sessions sparkline (Step 65) ──────────────────────────
    [ObservableProperty] private ISeries[] _sparklineSeries = [];
    private readonly ObservableCollection<ObservableValue> _sparklineValues = new();
    private const int SparklinePoints = 60;

    // ── Recent activity list ──────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<RecentActivityItem> _recentActivity = new();

    public int RefreshIntervalSeconds { get; set; } = 5;

    public DashboardViewModel(IStatsService stats)
    {
        _stats = stats;
        _stats.StatsUpdated += (_, _) => RefreshOnUiThread();

        // Pre-fill sparkline ring buffer
        for (int i = 0; i < SparklinePoints; i++)
            _sparklineValues.Add(new ObservableValue(0));

        _sparklineSeries = BuildSparkline();
        _successPieSeries = BuildPie(0, 0);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => Refresh();

        Refresh();
    }

    public void StartRefreshTimer()
    {
        if (!IsPaused) _timer.Start();
    }
    public void StopRefreshTimer() => _timer.Stop();

    [RelayCommand]
    public void TogglePause()
    {
        IsPaused = !IsPaused;
        if (IsPaused) _timer.Stop();
        else          _timer.Start();
    }

    public void Refresh()
    {
        var s = _stats.GetTodayStats();
        TotalVisitsToday = s.TotalVisits;
        ActiveSessions   = s.ActiveSessions;
        SuccessRate      = s.SuccessRate;
        ProxiesOnline    = s.ProxiesOnline;

        // Update sparkline (push active-session count as latest point)
        _sparklineValues.RemoveAt(0);
        _sparklineValues.Add(new ObservableValue(s.ActiveSessions));

        // Refresh visits/hour chart
        var buckets = _stats.GetVisitsPerHour(12);
        VisitsPerHourSeries = BuildVisitsChart(buckets);
        HourAxisX           = BuildHourXAxis(buckets);
        HourAxisY           = BuildHourYAxis();

        // Refresh pie
        SuccessPieSeries = BuildPie(s.SuccessVisits, s.TotalVisits - s.SuccessVisits);
    }

    private void RefreshOnUiThread()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(Refresh);
    }

    // ── Chart builders ────────────────────────────────────────────────

    private ISeries[] BuildVisitsChart(IList<HourlyBucket> buckets)
    {
        var values = buckets.Select(b => (double)b.Visits).ToArray();
        return
        [
            new LineSeries<double>
            {
                Values          = values,
                Name            = "Visits",
                Stroke          = new SolidColorPaint(SKColor.Parse("#2563EB")) { StrokeThickness = 2 },
                GeometrySize    = 0,
                Fill            = new SolidColorPaint(SKColor.Parse("#2563EB").WithAlpha(40)),
                LineSmoothness  = 0.5,
            }
        ];
    }

    private static Axis[] BuildHourXAxis(IList<HourlyBucket> buckets) =>
    [
        new Axis
        {
            Labels    = buckets.Select(b => b.Hour.ToString("HH:mm")).ToArray(),
            TextSize  = 10,
            LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
        }
    ];

    private static Axis[] BuildHourYAxis() =>
    [
        new Axis
        {
            MinLimit  = 0,
            TextSize  = 10,
            LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
        }
    ];

    private ISeries[] BuildSparkline() =>
    [
        new LineSeries<ObservableValue>
        {
            Values         = _sparklineValues,
            GeometrySize   = 0,
            Stroke         = new SolidColorPaint(SKColor.Parse("#22C55E")) { StrokeThickness = 1.5f },
            Fill           = new SolidColorPaint(SKColor.Parse("#22C55E").WithAlpha(30)),
            LineSmoothness = 0.8,
        }
    ];

    private static ISeries[] BuildPie(int success, int fail) =>
    [
        new PieSeries<int>
        {
            Values = [success],
            Name   = "Success",
            Fill   = new SolidColorPaint(SKColor.Parse("#22C55E")),
        },
        new PieSeries<int>
        {
            Values = [Math.Max(0, fail)],
            Name   = "Failed",
            Fill   = new SolidColorPaint(SKColor.Parse("#EF4444")),
        }
    ];
}

public record RecentActivityItem(string Campaign, string Url, bool Success, DateTime When);
