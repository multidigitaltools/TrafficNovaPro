using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;
using TrafficNova.ViewModels;

namespace TrafficNova.Dialogs;

public partial class ProxyExportDialog : Window
{
    private readonly IProxyService _service;
    private readonly ProxiesViewModel _vm;

    public ProxyExportDialog(ProxiesViewModel vm)
    {
        _service = App.Services.GetRequiredService<IProxyService>();
        _vm = vm;
        InitializeComponent();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export Proxies",
            Filter = "Text file (*.txt)|*.txt|CSV file (*.csv)|*.csv",
            FileName = "proxies"
        };
        if (dlg.ShowDialog() != true) return;

        IEnumerable<ProxyEntry> proxies = ScopeVisible.IsChecked == true
            ? _vm.Proxies
            : await _service.GetAllAsync();

        var sb = new StringBuilder();
        foreach (var p in proxies)
        {
            string line;
            if (FormatFull.IsChecked == true)
            {
                var auth = (p.Username, p.Password) is ({ Length: > 0 }, { Length: > 0 })
                    ? $"{p.Username}:{p.Password}@"
                    : "";
                line = $"{p.Protocol.ToString().ToLower()}://{auth}{p.Host}:{p.Port}";
            }
            else if (FormatColons.IsChecked == true)
            {
                line = string.IsNullOrEmpty(p.Username)
                    ? $"{p.Host}:{p.Port}"
                    : $"{p.Host}:{p.Port}:{p.Username}:{p.Password}";
            }
            else
            {
                line = $"{p.Host}:{p.Port}";
            }
            sb.AppendLine(line);
        }

        await File.WriteAllTextAsync(dlg.FileName, sb.ToString());
        MessageBox.Show($"Exported {proxies.Count()} proxies to:\n{dlg.FileName}",
            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
