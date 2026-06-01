using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Newtonsoft.Json;
using TrafficNova.Core.Models;

namespace TrafficNova.Converters;

// Converts CampaignStatus → pill background brush
[ValueConversion(typeof(CampaignStatus), typeof(Brush))]
public class StatusToBackgroundConverter : IValueConverter
{
    public static readonly StatusToBackgroundConverter Default = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is CampaignStatus s ? s switch
        {
            CampaignStatus.Running   => new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)),
            CampaignStatus.Paused    => new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xCD)),
            CampaignStatus.Completed => new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)),
            CampaignStatus.Scheduled => new SolidColorBrush(Color.FromRgb(0xED, 0xE9, 0xFE)),
            _                        => new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
        } : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// Converts CampaignStatus → pill text brush
[ValueConversion(typeof(CampaignStatus), typeof(Brush))]
public class StatusToForegroundConverter : IValueConverter
{
    public static readonly StatusToForegroundConverter Default = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is CampaignStatus s ? s switch
        {
            CampaignStatus.Running   => new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
            CampaignStatus.Paused    => new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)),
            CampaignStatus.Completed => new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
            CampaignStatus.Scheduled => new SolidColorBrush(Color.FromRgb(0x70, 0x4E, 0xE6)),
            _                        => new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
        } : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// Converts TargetUrlsJson → count as string
[ValueConversion(typeof(string), typeof(string))]
public class JsonToUrlCountConverter : IValueConverter
{
    public static readonly JsonToUrlCountConverter Default = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string json || string.IsNullOrWhiteSpace(json))
            return "0";
        try
        {
            var urls = JsonConvert.DeserializeObject<string[]>(json);
            return (urls?.Length ?? 0).ToString();
        }
        catch { return "1"; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// bool → "OK" / "FAIL" string
[ValueConversion(typeof(bool), typeof(string))]
public class BoolToResultConverter : IValueConverter
{
    public static readonly BoolToResultConverter Default = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "OK" : "FAIL";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// bool → green / red brush
[ValueConversion(typeof(bool), typeof(Brush))]
public class BoolToResultColorConverter : IValueConverter
{
    public static readonly BoolToResultColorConverter Default = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b
            ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
            : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// int → Visibility.Visible when 0 (empty state)
[ValueConversion(typeof(int), typeof(Visibility))]
public class ZeroToVisibleConverter : IValueConverter
{
    public static readonly ZeroToVisibleConverter Default = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// int → Visibility.Visible when > 0 (grid)
[ValueConversion(typeof(int), typeof(Visibility))]
public class NonZeroToVisibleConverter : IValueConverter
{
    public static readonly NonZeroToVisibleConverter Default = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// string → Visibility.Visible when non-empty
[ValueConversion(typeof(string), typeof(Visibility))]
public class StringNonEmptyToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// MultiValue: [int campaignId, Dictionary<int,string> lookup] → campaign name string
public class CampaignIdToNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return "";
        int id = values[0] is int i ? i : 0;
        if (values[1] is Dictionary<int, string> dict && dict.TryGetValue(id, out var name))
            return name;
        return id > 0 ? $"Campaign #{id}" : "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Phase 10 — MultiValue: [totalVisits, visitTarget] → percent 0-100
public class ProgressPercentConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        int done   = values.Length > 0 && values[0] is int d ? d : 0;
        int target = values.Length > 1 && values[1] is int t ? t : 0;
        if (target <= 0) return 0.0;
        return Math.Min(100.0, done * 100.0 / target);
    }
    public object[] ConvertBack(object value, Type[] t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

// Phase 10 — MultiValue: [totalVisits, visitTarget] → "12 / 100" or "12 (∞)"
public class ProgressTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        int done   = values.Length > 0 && values[0] is int d ? d : 0;
        int target = values.Length > 1 && values[1] is int t ? t : 0;
        return target > 0 ? $"{done} / {target}" : $"{done} (∞)";
    }
    public object[] ConvertBack(object value, Type[] t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

// object → bool (true when not null). Used for IsEnabled on selection-dependent buttons.
public class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not null && !(value is string s && s.Length == 0);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// bool → !bool. Used for IsEnabled when a busy flag is set.
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
