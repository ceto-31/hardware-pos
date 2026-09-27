using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HardwarePOS.Helpers;

namespace HardwarePOS.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

public class StockStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "OutOfStock" => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
            "LowStock" => new SolidColorBrush(Color.FromRgb(254, 249, 195)),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StatusToBadgeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Protected" or "Archived" or "Inactive" => new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            "Out of Stock" or "OutOfStock" => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            "Low Stock" or "LowStock" => new SolidColorBrush(Color.FromRgb(202, 138, 4)),
            _ => new SolidColorBrush(Color.FromRgb(22, 163, 74))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EqualityMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        return string.Equals(values[0]?.ToString(), values[1]?.ToString(), StringComparison.Ordinal);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Picks a column count for a card grid from the available width so cards never get
/// squeezed below a readable minimum. Parameter = maximum columns. Steps 4 → 2 → 1, 3 → 2 → 1.
/// </summary>
public class WidthToColumnsConverter : IValueConverter
{
    // Compact cards (KPIs, alert lists) stay readable at ~260px; chart/summary cards need more room.
    private static double MinCardWidth(int columns) => columns <= 2 ? 340 : 260;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var width = value is double d && !double.IsNaN(d) ? d : 0;
        var columns = int.TryParse(parameter?.ToString(), out var max) && max > 0 ? max : 1;

        while (columns > 1 && width / columns < MinCardWidth(columns))
            columns = columns % 2 == 0 ? columns / 2 : columns - 1;

        return columns;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ProductImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => ProductImageStore.Load(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
