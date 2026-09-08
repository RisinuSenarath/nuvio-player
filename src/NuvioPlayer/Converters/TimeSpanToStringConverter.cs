using System.Globalization;
using System.Windows.Data;

namespace NuvioPlayer.Converters;

public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            return FormatTime(ts);
        }
        if (value is long ms)
        {
            return FormatTime(TimeSpan.FromMilliseconds(ms));
        }
        if (value is double sec)
        {
            return FormatTime(TimeSpan.FromSeconds(sec));
        }

        return "00:00";
    }

    private static string FormatTime(TimeSpan span)
    {
        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
        return $"{span.Minutes:D2}:{span.Seconds:D2}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
