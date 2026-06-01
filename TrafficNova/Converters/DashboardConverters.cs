using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrafficNova.Converters;

[ValueConversion(typeof(bool), typeof(string))]
public class PauseButtonTextConverter : IValueConverter
{
    public static readonly PauseButtonTextConverter Default = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "▶  Resume Updates" : "⏸  Pause Updates";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// 0-based index → 1-based string for display
[ValueConversion(typeof(int), typeof(string))]
public class PageIndexConverter : IValueConverter
{
    public static readonly PageIndexConverter Default = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? (i + 1).ToString() : "1";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

// int run-limit → "∞" when 0 (unlimited), else the number.
// TargetNullValue can't do this — MaxRuns is a non-nullable int, never null.
[ValueConversion(typeof(int), typeof(string))]
public class MaxRunsConverter : IValueConverter
{
    public static readonly MaxRunsConverter Default = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n > 0 ? n.ToString() : "∞";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
