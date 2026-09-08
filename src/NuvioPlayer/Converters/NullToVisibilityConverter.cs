using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NuvioPlayer.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value == null;
        if (value is string str)
        {
            isNull = string.IsNullOrWhiteSpace(str);
        }

        bool isVisible = Invert ? isNull : !isNull;
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
