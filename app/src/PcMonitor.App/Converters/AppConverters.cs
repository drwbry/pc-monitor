using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PcMonitor.Core.Models;

namespace PcMonitor.App.Converters;

public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => !(bool)(v ?? false);
    public object ConvertBack(object v, Type t, object? p, CultureInfo c) => !(bool)v;
}

public sealed class BoolToVisibleConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c)
        => (bool)(v ?? false) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v switch
    {
        IssueSeverity.Red => (Brush)new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49)),
        IssueSeverity.Yellow => new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22)),
        _ => Brushes.Gray,
    };
    public object ConvertBack(object v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
