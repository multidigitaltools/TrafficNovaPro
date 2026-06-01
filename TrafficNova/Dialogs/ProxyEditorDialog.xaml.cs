using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;
using TrafficNova.ViewModels;

namespace TrafficNova.Dialogs;

public partial class ProxyEditorDialog : Window
{
    private readonly IProxyService _service;
    private readonly ProxiesViewModel _vm;
    private readonly ProxyEntry _editing;
    private readonly bool _isEdit;

    public string[] ProtocolOptions { get; } = ["Http", "Socks4", "Socks5"];
    public string Protocol { get; set; } = "Http";
    public string Host { get; set; } = string.Empty;
    public double Port { get; set; } = 8080;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string Label { get; set; } = string.Empty;
    public string GroupTag { get; set; } = string.Empty;
    public bool UseChain { get; set; } = false;

    public ProxyEditorDialog(ProxyEntry? existing, ProxiesViewModel vm)
    {
        _service = App.Services.GetRequiredService<IProxyService>();
        _vm = vm;
        _isEdit = existing is not null;
        _editing = existing ?? new ProxyEntry();

        if (_isEdit)
        {
            Protocol = _editing.Protocol.ToString();
            Host = _editing.Host;
            Port = _editing.Port;
            Username = _editing.Username;
            Password = _editing.Password;
            Label = _editing.Label;
            GroupTag = _editing.GroupTag;
            UseChain = _editing.UseChain;
        }

        InitializeComponent();
        DataContext = this;

        if (_isEdit) TitleText.Text = "Edit Proxy";
        if (_isEdit && !string.IsNullOrEmpty(_editing.Password))
            PasswordField.Password = _editing.Password;
    }

    private void PasswordField_Changed(object sender, RoutedEventArgs e)
    {
        Password = PasswordField.Password;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(Host))
        {
            ShowError("Host is required.");
            return;
        }
        if (Port is < 1 or > 65535)
        {
            ShowError("Port must be between 1 and 65535.");
            return;
        }

        var proto = Enum.TryParse<ProxyProtocol>(Protocol, out var p) ? p : ProxyProtocol.Http;

        _editing.Host = Host.Trim();
        _editing.Port = (int)Port;
        _editing.Protocol = proto;
        _editing.Username = string.IsNullOrWhiteSpace(Username) ? null : Username;
        _editing.Password = string.IsNullOrWhiteSpace(Password) ? null : Password;
        _editing.Label = Label;
        _editing.GroupTag = GroupTag;
        _editing.UseChain = UseChain;

        if (_isEdit)
            await _service.UpdateAsync(_editing);
        else
            await _service.CreateAsync(_editing);

        await _vm.RefreshAsync();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string msg)
    {
        ValidationMsg.Text = msg;
        ValidationMsg.Visibility = Visibility.Visible;
    }
}
