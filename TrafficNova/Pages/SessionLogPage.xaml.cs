using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TrafficNova.Core.Models;
using TrafficNova.Dialogs;
using TrafficNova.ViewModels;

namespace TrafficNova.Pages;

public partial class SessionLogPage : UserControl
{
    private readonly SessionLogViewModel _vm;

    public SessionLogPage(SessionLogViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }

    private void OnSessionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SessionGrid.SelectedItem is not TrafficSession s) return;
        var name = _vm.CampaignNamesById.TryGetValue(s.CampaignId, out var n)
            ? n : $"Campaign #{s.CampaignId}";
        var dlg = new SessionDetailDialog(s, name) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
    }
}
