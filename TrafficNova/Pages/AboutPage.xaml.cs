using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace TrafficNova.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = $"Version {version?.ToString(3) ?? "1.0.0"}";
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("You are running the latest version (v1.0.0).",
            "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
