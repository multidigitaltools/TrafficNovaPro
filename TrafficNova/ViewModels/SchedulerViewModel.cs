using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;
using TrafficNova.Data.Services;

namespace TrafficNova.ViewModels;

public partial class SchedulerViewModel : ObservableObject
{
    private readonly IScheduledJobService _jobService;
    private readonly ICampaignService     _campaigns;
    private readonly SchedulerService     _scheduler;

    public ObservableCollection<ScheduledJob> Jobs { get; } = new();
    public ObservableCollection<Campaign>     AllCampaigns { get; } = new();

    [ObservableProperty] private ScheduledJob? _selectedJob;
    [ObservableProperty] private string        _statusMessage = string.Empty;

    public SchedulerViewModel(
        IScheduledJobService jobService,
        ICampaignService campaigns,
        SchedulerService scheduler)
    {
        _jobService = jobService;
        _campaigns  = campaigns;
        _scheduler  = scheduler;
    }

    public async Task LoadAsync()
    {
        try
        {
            var jobs  = await _jobService.GetAllAsync();
            var camps = await _campaigns.GetAllAsync();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                Jobs.Clear();
                foreach (var j in jobs) Jobs.Add(j);

                AllCampaigns.Clear();
                foreach (var c in camps) AllCampaigns.Add(c);
            });
        }
        catch (Exception ex)
        {
            // BUG-054: DB failure silently left scheduler list blank with no feedback
            Serilog.Log.Error(ex, "Failed to load scheduler data");
            Application.Current?.Dispatcher.Invoke(() =>
                MessageBox.Show("Could not load scheduled jobs:\n\n" + ex.Message,
                    "Load Failed", MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }

    [RelayCommand]
    public async Task ToggleEnabledAsync(ScheduledJob? job)
    {
        if (job is null) return;
        try
        {
            job.IsEnabled = !job.IsEnabled;
            await _jobService.SetEnabledAsync(job.Id, job.IsEnabled);
            OnPropertyChanged(nameof(Jobs));
            StatusMessage = job.IsEnabled
                ? $"Job #{job.Id} enabled."
                : $"Job #{job.Id} disabled.";
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to toggle job {Id}", job.Id);
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteJobAsync(ScheduledJob? job)
    {
        if (job is null) return;
        var r = MessageBox.Show($"Delete scheduled job for '{job.Campaign?.Name ?? "Campaign " + job.CampaignId}'?",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try
        {
            await _jobService.DeleteAsync(job.Id);
            // BUG-068: Jobs.Remove after await ran on ThreadPool thread — must use Dispatcher
            Application.Current?.Dispatcher.Invoke(() => Jobs.Remove(job));
            StatusMessage = "Job deleted.";
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to delete job {Id}", job.Id);
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RunNowAsync(ScheduledJob? job)
    {
        if (job is null) return;
        try
        {
            await _campaigns.StartAsync(job.CampaignId);
            StatusMessage = $"Campaign '{job.Campaign?.Name ?? job.CampaignId.ToString()}' started.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not start: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadAsync();

    // Called after CronBuilderDialog saves a new/edited job
    public async Task SaveJobAsync(ScheduledJob job)
    {
        try
        {
            if (job.Id == 0)
            {
                var created = await _jobService.CreateAsync(job);
                // BUG-069: Jobs.Add after await ran on ThreadPool thread — must use Dispatcher
                Application.Current?.Dispatcher.Invoke(() => Jobs.Add(created));
                StatusMessage = "Scheduled job created.";
            }
            else
            {
                await _jobService.UpdateAsync(job);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var idx = Jobs.IndexOf(Jobs.FirstOrDefault(j => j.Id == job.Id)!);
                    if (idx >= 0)
                    {
                        Jobs.RemoveAt(idx);
                        Jobs.Insert(idx, job);
                    }
                });
                StatusMessage = "Scheduled job updated.";
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to save scheduled job");
            MessageBox.Show("Could not save scheduled job:\n\n" + ex.Message,
                "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
