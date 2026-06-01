using System.Windows;

namespace TrafficNova;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string message)
    {
        Dispatcher.InvokeAsync(() => StatusText.Text = message);
    }
}
