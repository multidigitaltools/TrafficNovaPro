using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;
using TrafficNova.ViewModels;

namespace TrafficNova.Dialogs;

public partial class ProxyImportDialog : Window
{
    private readonly IProxyService _service;
    private readonly ProxiesViewModel _vm;
    private List<ProxyEntry> _parsed = new();

    public ProxyImportDialog(ProxiesViewModel vm)
    {
        _service = App.Services.GetRequiredService<IProxyService>();
        _vm = vm;
        InitializeComponent();
    }

    private void LoadFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select proxy file",
            Filter = "Text files (*.txt;*.csv)|*.txt;*.csv|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            ProxyTextBox.Text = File.ReadAllText(dlg.FileName);
    }

    private void ProxyTextBox_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ParseText(ProxyTextBox.Text);
    }

    private void ParseText(string text)
    {
        _parsed.Clear();
        int invalid = 0;

        foreach (var rawLine in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;
            var entry = ParseLine(line);
            if (entry is not null)
                _parsed.Add(entry);
            else
                invalid++;
        }

        ValidCount.Text = _parsed.Count.ToString();
        InvalidCount.Text = invalid.ToString();
        ImportBtn.IsEnabled = _parsed.Count > 0;
    }

    private static ProxyEntry? ParseLine(string line)
    {
        try
        {
            var protocol = ProxyProtocol.Http;
            string? user = null, pass = null;

            // protocol://user:pass@host:port
            if (line.Contains("://"))
            {
                var schemeSep = line.IndexOf("://");
                var scheme = line[..schemeSep].ToLower();
                protocol = scheme switch
                {
                    "socks4" => ProxyProtocol.Socks4,
                    "socks5" => ProxyProtocol.Socks5,
                    _ => ProxyProtocol.Http
                };
                line = line[(schemeSep + 3)..];

                // user:pass@host:port
                if (line.Contains('@'))
                {
                    var atIdx = line.LastIndexOf('@');
                    var credentials = line[..atIdx].Split(':', 2);
                    user = credentials.Length > 0 ? credentials[0] : null;
                    pass = credentials.Length > 1 ? credentials[1] : null;
                    line = line[(atIdx + 1)..];
                }
            }

            // host:port or host:port:user:pass
            var parts = line.Split(':');
            if (parts.Length < 2) return null;
            var host = parts[0].Trim();
            // Reject an empty/whitespace host — it would later make the proxy
            // tester's Uri ctor throw on an unparseable "scheme://:port".
            if (string.IsNullOrWhiteSpace(host)) return null;
            if (!int.TryParse(parts[1].Trim(), out var port) || port < 1 || port > 65535) return null;
            if (parts.Length >= 4)
            {
                user = parts[2].Trim();
                pass = parts[3].Trim();
            }
            else if (parts.Length == 3 && user is null)
            {
                // host:port:user — treat as label
            }

            return new ProxyEntry
            {
                Host = host,
                Port = port,
                Protocol = protocol,
                Username = string.IsNullOrEmpty(user) ? null : user,
                Password = string.IsNullOrEmpty(pass) ? null : pass,
                IsActive = true
            };
        }
        catch { return null; }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var group = GroupTagBox.Text.Trim();
        if (!string.IsNullOrEmpty(group))
            foreach (var p in _parsed) p.GroupTag = group;

        var count = await _service.BulkImportAsync(_parsed);
        await _vm.RefreshAsync();
        MessageBox.Show($"Successfully imported {count} proxies.",
            "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
