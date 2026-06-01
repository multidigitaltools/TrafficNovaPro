using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;
using TrafficNova.Engine;

namespace TrafficNova.ViewModels;

public partial class ProxiesViewModel : ObservableObject
{
    private readonly IProxyService _proxyService;
    private readonly ProxyTesterService _tester;
    private List<ProxyEntry> _allProxies = new();

    [ObservableProperty] private ObservableCollection<ProxyEntry> _proxies = new();
    [ObservableProperty] private ProxyEntry? _selectedProxy;
    [ObservableProperty] private ObservableCollection<ProxyEntry> _selectedProxies = new();
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _filterProtocol = "All";
    [ObservableProperty] private string _filterGroup = "All";

    // Stats bar
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _deadCount;
    [ObservableProperty] private int _untestedCount;

    // Testing state
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private int _testProgress;
    [ObservableProperty] private int _testTotal;

    public string[] ProtocolOptions { get; } = ["All", "Http", "Socks4", "Socks5"];
    [ObservableProperty] private ObservableCollection<string> _groupOptions = ["All"];

    public ProxiesViewModel(IProxyService proxyService, ProxyTesterService tester)
    {
        _proxyService = proxyService;
        _tester = tester;
        // ProxiesChanged can fire from the ProxyHealthMonitor background
        // thread; LoadAsync mutates bound ObservableCollections, so marshal
        // it onto the UI thread or it throws a cross-thread exception.
        _proxyService.ProxiesChanged += (_, _) =>
            Application.Current?.Dispatcher.Invoke(() => _ = LoadAsync());
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            _allProxies = (await _proxyService.GetAllAsync()).ToList();
            RefreshGroups();
            ApplyFilter();
            UpdateStats();
        }
        catch (Exception ex)
        {
            // BUG-053: unhandled DB exception left proxy list blank with no user feedback
            Serilog.Log.Error(ex, "Failed to load proxies");
            MessageBox.Show("Could not load proxies:\n\n" + ex.Message,
                "Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshGroups()
    {
        var groups = _proxyService.GetGroups();
        GroupOptions.Clear();
        GroupOptions.Add("All");
        foreach (var g in groups) GroupOptions.Add(g);
    }

    private void ApplyFilter()
    {
        var filtered = _allProxies.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(p =>
                p.Host.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Label.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        if (FilterProtocol != "All" && Enum.TryParse<ProxyProtocol>(FilterProtocol, out var protocol))
            filtered = filtered.Where(p => p.Protocol == protocol);

        if (FilterGroup != "All")
            filtered = filtered.Where(p => p.GroupTag == FilterGroup);

        Proxies = new ObservableCollection<ProxyEntry>(filtered);
    }

    private void UpdateStats()
    {
        var stats = _proxyService.GetStats();
        TotalCount = stats.Total;
        ActiveCount = stats.Active;
        DeadCount = stats.Dead;
        UntestedCount = stats.Untested;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterProtocolChanged(string value) => ApplyFilter();
    partial void OnFilterGroupChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedProxy is null) return;
        var result = MessageBox.Show(
            $"Delete proxy {SelectedProxy.Address}?",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            await _proxyService.DeleteAsync(SelectedProxy.Id);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to delete proxy");
            MessageBox.Show("Could not delete proxy:\n\n" + ex.Message,
                "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteGroupAsync()
    {
        if (FilterGroup == "All") return;
        var result = MessageBox.Show(
            $"Delete all proxies in group '{FilterGroup}'?",
            "Confirm Delete Group", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            await _proxyService.DeleteGroupAsync(FilterGroup);
            FilterGroup = "All";
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to delete proxy group");
            MessageBox.Show("Could not delete group:\n\n" + ex.Message,
                "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task TestSelectedAsync()
    {
        if (SelectedProxy is null) return;
        await RunTestsAsync([SelectedProxy]);
    }

    [RelayCommand]
    public async Task TestAllAsync()
    {
        var proxies = _allProxies.Where(p => p.IsActive).ToList();
        await RunTestsAsync(proxies);
    }

    private CancellationTokenSource? _testCts;

    private async Task RunTestsAsync(IList<ProxyEntry> proxies)
    {
        if (IsTesting) return;
        IsTesting = true;
        TestTotal = proxies.Count;
        TestProgress = 0;
        _testCts = new CancellationTokenSource();

        var progress = new Progress<ProxyTestProgress>(p =>
        {
            TestProgress = p.Tested;
            // Refresh stats every 5 tests to keep UI snappy
            if (p.Tested % 5 == 0) UpdateStats();
        });

        try
        {
            await _tester.TestAllAsync(proxies, progress, _testCts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsTesting = false;
            _testCts.Dispose();
            _testCts = null;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private void CancelTesting()
    {
        _testCts?.Cancel();
    }

    // Called from ProxyEditorDialog after save
    public async Task RefreshAsync() => await LoadAsync();
}
