using System.Diagnostics;
using System.IO;
using System.Windows;
using TrafficNova.Core.Models;

namespace TrafficNova.Dialogs;

public partial class SessionDetailDialog : Window
{
    private readonly string? _screenshotPath;
    private readonly string? _tracePath;

    public SessionDetailDialog(TrafficSession s, string campaignName)
    {
        InitializeComponent();

        _screenshotPath = s.ScreenshotPath;
        _tracePath      = s.TracePath;

        TitleText.Text    = $"Session #{s.Id}";
        SubtitleText.Text = $"{campaignName} · {s.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

        ResultText.Text   = s.Success ? "✅ Success" : "❌ Failed";
        ResultText.Foreground = s.Success
            ? System.Windows.Media.Brushes.SeaGreen
            : System.Windows.Media.Brushes.IndianRed;

        UrlText.Text      = s.TargetUrl;
        StatusText.Text   = s.StatusCode?.ToString() ?? "—";
        StartedText.Text  = s.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        DurationText.Text = s.DurationMs.HasValue ? $"{s.DurationMs} ms" : "—";
        DwellText.Text    = $"{s.DwellMs} ms";
        BlockedText.Text  = s.BlockedRequests > 0
            ? $"{s.BlockedRequests} requests aborted (bandwidth saved)"
            : "0";
        ProxyText.Text    = s.ProxyId?.ToString() ?? "(no proxy)";
        UaText.Text       = string.IsNullOrEmpty(s.UserAgent) ? "—" : s.UserAgent;
        ReferrerText.Text = string.IsNullOrEmpty(s.Referrer) ? "(none)" : s.Referrer;
        ErrorText.Text    = string.IsNullOrEmpty(s.ErrorMessage) ? "—" : s.ErrorMessage;

        if (!string.IsNullOrEmpty(_screenshotPath) && File.Exists(_screenshotPath))
            OpenScreenshotBtn.Visibility = Visibility.Visible;
        if (!string.IsNullOrEmpty(_tracePath) && File.Exists(_tracePath))
            OpenTraceBtn.Visibility = Visibility.Visible;
    }

    private void OpenScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_screenshotPath) && File.Exists(_screenshotPath))
            Process.Start(new ProcessStartInfo(_screenshotPath) { UseShellExecute = true });
    }

    private void OpenTrace_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_tracePath) || !File.Exists(_tracePath)) return;
        // Open the containing folder and select the trace zip
        var dir = Path.GetDirectoryName(_tracePath);
        if (dir is not null && Directory.Exists(dir))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_tracePath}\""));
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
