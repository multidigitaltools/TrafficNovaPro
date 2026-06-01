using System.Windows.Controls;
using TrafficNova.ViewModels;

namespace TrafficNova.Pages;

public partial class DashboardPage : UserControl
{
    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        // BUG-063: timer started unconditionally in ctor ran while page was hidden
        Loaded   += (_, _) => vm.StartRefreshTimer();
        Unloaded += (_, _) => vm.StopRefreshTimer();
    }
}
