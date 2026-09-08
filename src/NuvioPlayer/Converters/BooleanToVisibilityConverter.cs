using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NuvioPlayer.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;
    public bool UseHidden { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = false;
        if (value is bool b)
        {
            flag = b;
        }

        if (Invert)
        {
            flag = !flag;
        }

        if (flag)
        {
            return Visibility.Visible;
        }

        return UseHidden ? Visibility.Hidden : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            bool result = vis == Visibility.Visible;
            return Invert ? !result : result;
        }
        return false;
    }
}
