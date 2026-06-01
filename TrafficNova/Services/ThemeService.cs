using System.Windows;
using Serilog;
using TrafficNova.Core.Interfaces;

namespace TrafficNova.Services;

/// <summary>
/// Swaps the Colors resource dictionary at runtime to switch between Light and Dark themes.
/// </summary>
public class ThemeService
{
    private readonly IAppSettingsService _settings;

    public ThemeService(IAppSettingsService settings) => _settings = settings;

    public void Apply(string theme)
    {
        _settings.Current.Theme = theme;
        bool dark = theme == "Dark";

        var dict = Application.Current.Resources.MergedDictionaries;

        // 1. Swap HandyControl's skin so its controls (DataGrid, TabControl,
        //    ComboBox, TextBox, CheckBox, DatePicker, …) follow the theme.
        //    Without this only our own brushes go dark and the controls stay
        //    light — a half-themed window.
        try
        {
            var hcSkin = dict.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("SkinDefault") == true ||
                d.Source?.OriginalString.Contains("SkinDark") == true);
            if (hcSkin is not null)
            {
                int idx = dict.IndexOf(hcSkin);
                var hcUri = new Uri(dark
                    ? "pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml"
                    : "pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml");
                dict.RemoveAt(idx);
                dict.Insert(idx, new ResourceDictionary { Source = hcUri });
                Log.Information("Theme: HandyControl skin → {Skin}", dark ? "Dark" : "Default");
            }
            else
            {
                Log.Warning("Theme: HandyControl skin dictionary not found ({Count} merged dicts: {Sources})",
                    dict.Count, string.Join(" | ", dict.Select(d => d.Source?.OriginalString ?? "(inline)")));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Theme: HandyControl skin swap failed");
        }

        // 2. Swap our own colour palette.
        var existing = dict.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Colors.xaml") == true ||
            d.Source?.OriginalString.Contains("DarkColors.xaml") == true);
        if (existing is not null) dict.Remove(existing);

        // Append LAST so our palette wins: several of our keys (BackgroundBrush,
        // BorderBrush, PrimaryBrush, …) collide with HandyControl's. A later
        // merged dictionary overrides earlier ones — inserting at index 0 let
        // HandyControl's light values win and left surfaces un-themed.
        var uri = dark
            ? new Uri("Resources/DarkColors.xaml", UriKind.Relative)
            : new Uri("Resources/Colors.xaml", UriKind.Relative);

        dict.Add(new ResourceDictionary { Source = uri });
    }
}
