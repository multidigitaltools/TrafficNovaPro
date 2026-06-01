using System.Windows;
using System.Windows.Controls;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace TrafficNova.Pages;

public partial class CampaignHistoryPanel : UserControl
{
    public event EventHandler? CloseRequested;

    public CampaignHistoryPanel()
    {
        InitializeComponent();
    }

    public async Task LoadAsync(Campaign campaign)
    {
        PanelTitle.Text    = $"Session History — {campaign.Name}";
        PanelSubtitle.Text = $"Campaign ID {campaign.Id} · last 100 sessions";

        var service  = App.Services.GetRequiredService<ICampaignService>();
        var sessions = await service.GetRecentSessionsAsync(campaign.Id, 100);
        SessionGrid.ItemsSource = sessions;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
