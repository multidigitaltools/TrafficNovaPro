using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace TrafficNova.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _activePageName = "Dashboard";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private int _activeSessions;

    private readonly IServiceProvider _services;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
    }

    [RelayCommand]
    public void NavigateTo(string pageName)
    {
        ActivePageName = pageName;
        CurrentPage = pageName switch
        {
            "Dashboard" => _services.GetService(typeof(Pages.DashboardPage)),
            "Campaigns" => _services.GetService(typeof(Pages.CampaignsPage)),
            "Proxies" => _services.GetService(typeof(Pages.ProxiesPage)),
            "Scheduler" => _services.GetService(typeof(Pages.SchedulerPage)),
            "Settings" => _services.GetService(typeof(Pages.SettingsPage)),
            "Logs" => _services.GetService(typeof(Pages.LogsPage)),
            "Sessions" => _services.GetService(typeof(Pages.SessionLogPage)),
            "About" => _services.GetService(typeof(Pages.AboutPage)),
            _ => CurrentPage
        };
    }
}
