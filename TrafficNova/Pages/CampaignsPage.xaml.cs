using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TrafficNova.Dialogs;
using TrafficNova.ViewModels;

namespace TrafficNova.Pages;

public partial class CampaignsPage : UserControl
{
    private CampaignsViewModel Vm => (CampaignsViewModel)DataContext;

    public CampaignsPage(CampaignsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded   += async (_, _) => { await vm.LoadAsync(); vm.StartProgressTimer(); };
        Unloaded += (_, _) => vm.StopProgressTimer();

        HistoryPanel.CloseRequested += (_, _) => HistoryPanel.Visibility = Visibility.Collapsed;
        CampaignGrid.SelectionChanged += OnGridSelectionChanged;
    }

    private async void OnGridSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Sync multi-select to VM (Step 108)
        Vm.SelectedItems.Clear();
        foreach (var item in CampaignGrid.SelectedItems.OfType<TrafficNova.Core.Models.Campaign>())
            Vm.SelectedItems.Add(item);

        if (Vm.Selected is null)
        {
            HistoryPanel.Visibility = Visibility.Collapsed;
            return;
        }
        HistoryPanel.Visibility = Visibility.Visible;
        await HistoryPanel.LoadAsync(Vm.Selected);
    }

    private void OnNewCampaign(object sender, RoutedEventArgs e)
    {
        var dlg = new CampaignEditorDialog { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
            _ = Vm.LoadAsync();
    }

    private void OnEditCampaign(object sender, RoutedEventArgs e)
    {
        if (Vm.Selected is null) return;
        var dlg = new CampaignEditorDialog(Vm.Selected) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
            _ = Vm.LoadAsync();
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement fe &&
            fe.DataContext is TrafficNova.Core.Models.Campaign)
            OnEditCampaign(sender, e);
    }
}
