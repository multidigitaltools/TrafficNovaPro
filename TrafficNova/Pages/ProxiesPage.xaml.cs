using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TrafficNova.Dialogs;
using TrafficNova.ViewModels;

namespace TrafficNova.Pages;

public partial class ProxiesPage : UserControl
{
    private readonly ProxiesViewModel _vm;

    public ProxiesPage(ProxiesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }

    private void AddProxy_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProxyEditorDialog(null, _vm);
        dialog.Owner = Window.GetWindow(this);
        dialog.ShowDialog();
    }

    private void EditProxy_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProxy is null) return;
        var dialog = new ProxyEditorDialog(_vm.SelectedProxy, _vm);
        dialog.Owner = Window.GetWindow(this);
        dialog.ShowDialog();
    }

    private void EditRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Core.Models.ProxyEntry proxy)
        {
            var dialog = new ProxyEditorDialog(proxy, _vm);
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
        }
    }

    private async void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Core.Models.ProxyEntry proxy)
        {
            var result = MessageBox.Show($"Delete proxy {proxy.Address}?",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                await App.Services.GetRequiredService<Core.Interfaces.IProxyService>().DeleteAsync(proxy.Id);
        }
    }

    private void ImportProxy_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProxyImportDialog(_vm);
        dialog.Owner = Window.GetWindow(this);
        dialog.ShowDialog();
    }

    private void ExportProxy_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProxyExportDialog(_vm);
        dialog.Owner = Window.GetWindow(this);
        dialog.ShowDialog();
    }

    private void CopyAddress_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProxy is null) return;
        Clipboard.SetText(_vm.SelectedProxy.Address);
    }
}
