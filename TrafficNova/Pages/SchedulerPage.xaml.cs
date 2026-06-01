using System.Windows;
using System.Windows.Controls;
using TrafficNova.Dialogs;
using TrafficNova.ViewModels;

namespace TrafficNova.Pages;

public partial class SchedulerPage : UserControl
{
    private readonly SchedulerViewModel _vm;

    public SchedulerPage(SchedulerViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;

        HistoryPanel.CloseRequested += (_, _) =>
        {
            HistoryPanel.Visibility = Visibility.Collapsed;
        };

        JobGrid.SelectionChanged += async (_, _) =>
        {
            var job = _vm.SelectedJob;
            if (job?.Campaign is not null)
            {
                HistoryPanel.Visibility = Visibility.Visible;
                await HistoryPanel.LoadAsync(job.Campaign);
            }
            else
            {
                HistoryPanel.Visibility = Visibility.Collapsed;
            }
        };

        Loaded += async (_, _) => await vm.LoadAsync();
    }

    private void OnAddJob(object sender, RoutedEventArgs e)
        => OpenEditor(null);

    private void OnEditJob(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedJob is null) return;
        OpenEditor(_vm.SelectedJob);
    }

    // BUG-040: `async void` in a non-event-handler — any exception (e.g. a DB
    // unique-constraint violation in SaveJobAsync) would escape to the dispatcher
    // unhandled-exception handler with no user feedback. Wrap and surface.
    private async void OpenEditor(Core.Models.ScheduledJob? existing)
    {
        try
        {
            var dlg = new CronBuilderDialog(_vm.AllCampaigns, existing);
            if (dlg.ShowDialog() == true && dlg.ResultJob is not null)
                await _vm.SaveJobAsync(dlg.ResultJob);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to save scheduled job");
            MessageBox.Show(
                "Could not save the scheduled job:\n\n" + ex.Message,
                "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
