using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DevToolsCurator.Core;

namespace DevToolsCurator.App;

public sealed class ViewVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class IntEqualsVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return int.TryParse(value?.ToString(), out var current) &&
               int.TryParse(parameter?.ToString(), out var expected) &&
               current == expected
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ToolStatus status ? status switch
        {
            ToolStatus.Installed_Current => new SolidColorBrush(Color.FromRgb(58, 196, 125)),
            ToolStatus.Installed_Outdated => new SolidColorBrush(Color.FromRgb(245, 190, 75)),
            ToolStatus.Installed_NotOnPath => new SolidColorBrush(Color.FromRgb(245, 190, 75)),
            ToolStatus.Missing_Recommended => new SolidColorBrush(Color.FromRgb(245, 91, 91)),
            ToolStatus.Broken => new SolidColorBrush(Color.FromRgb(245, 91, 91)),
            ToolStatus.AuthNeeded => new SolidColorBrush(Color.FromRgb(88, 166, 255)),
            ToolStatus.RebootNeeded => new SolidColorBrush(Color.FromRgb(245, 190, 75)),
            _ => new SolidColorBrush(Color.FromRgb(132, 142, 158))
        } : new SolidColorBrush(Color.FromRgb(132, 142, 158));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class StatusIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ToolStatus status ? status switch
        {
            ToolStatus.Installed_Current => "✓",
            ToolStatus.Installed_Outdated => "!",
            ToolStatus.Installed_NotOnPath => "!",
            ToolStatus.Missing_Recommended => "X",
            ToolStatus.Broken => "X",
            ToolStatus.AuthNeeded => "i",
            ToolStatus.RebootNeeded => "!",
            _ => "-"
        } : "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
