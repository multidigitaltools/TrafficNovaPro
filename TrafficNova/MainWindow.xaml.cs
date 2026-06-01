using System.Windows;
using System.Windows.Input;
using TrafficNova.Core.Interfaces;
using TrafficNova.ViewModels;

namespace TrafficNova;

public partial class MainWindow : Window
{
    private readonly IAppSettingsService _settings;
    private readonly ICampaignService    _campaigns;

    public MainWindow(MainWindowViewModel vm, IAppSettingsService settings, ICampaignService campaigns)
    {
        InitializeComponent();
        DataContext = vm;
        _settings   = settings;
        _campaigns  = campaigns;

        Loaded  += (_, _) => vm.NavigateTo("Dashboard");
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Minimize-to-tray takes precedence (app keeps running in tray)
        if (_settings.Current.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        // Phase 10 — warn if campaigns are still running before a real exit
        var running = _campaigns.GetRunning();
        if (running.Count > 0)
        {
            var names = string.Join(", ", running.Take(5).Select(c => c.Name));
            if (running.Count > 5) names += $" (+{running.Count - 5} more)";
            var r = MessageBox.Show(
                $"{running.Count} campaign(s) are still running:\n\n{names}\n\n" +
                "Stop them and exit TrafficNova Pro?",
                "Campaigns Running",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (r != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            // BUG-039: `StopAsync` awaits EF Core without ConfigureAwait(false), so
            // continuations resume on the dispatcher — calling .GetResult() on the UI
            // thread deadlocks. Detach via Task.Run so the awaits resume on the pool.
            foreach (var c in running)
            {
                try { Task.Run(() => _campaigns.StopAsync(c.Id)).Wait(5000); }
                catch { /* best-effort */ }
            }
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleMaximize();
        else
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaxRestore_Click(object sender, RoutedEventArgs e) =>
        ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) =>
        Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
