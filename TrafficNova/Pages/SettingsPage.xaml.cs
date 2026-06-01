using System.Windows.Controls;
using TrafficNova.ViewModels;

namespace TrafficNova.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
