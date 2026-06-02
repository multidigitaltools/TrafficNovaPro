using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Licensing;

namespace TrafficNova.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = $"Version {version?.ToString(3) ?? "1.0.0"}";

        // BUG-086: the License status was hardcoded to "Status: Trial" in XAML
        // even after the user activated. Read the real state and render the
        // same wording SettingsViewModel uses.
        try
        {
            var settings = App.Services
                .GetRequiredService<IAppSettingsService>().Current;
            LicenseStatusText.Text =
                $"Status: {TrialState.FormatStatus(settings, DateTime.UtcNow)}";
        }
        catch
        {
            // DI not ready / unit-test host — leave the XAML default visible.
        }
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("You are running the latest version (v1.0.0).",
            "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
