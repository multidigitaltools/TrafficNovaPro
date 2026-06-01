using System.Windows.Controls;
using TrafficNova.ViewModels;

namespace TrafficNova.Pages;

public partial class LogsPage : UserControl
{
    public LogsPage(LogsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        // BUG-064: timer started unconditionally in ctor kept doing file I/O off-page
        Loaded   += (_, _) => vm.StartRefreshTimer();
        Unloaded += (_, _) => vm.StopRefreshTimer();
    }
}
