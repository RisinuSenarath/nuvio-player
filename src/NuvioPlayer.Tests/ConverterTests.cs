using System.Globalization;
using System.Windows;
using NuvioPlayer.Converters;
using Xunit;

namespace NuvioPlayer.Tests;

public class ConverterTests
{
    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    public void BooleanToVisibilityConverter_ShouldConvertCorrectly(bool input, Visibility expected)
    {
        var converter = new BooleanToVisibilityConverter();
        var result = converter.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, Visibility.Collapsed)]
    [InlineData(false, Visibility.Visible)]
    public void BooleanToVisibilityConverter_Invert_ShouldInvertCorrectly(bool input, Visibility expected)
    {
        var converter = new BooleanToVisibilityConverter { Invert = true };
        var result = converter.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TimeSpanToStringConverter_ShouldFormatMmSs()
    {
        var converter = new TimeSpanToStringConverter();
        var ts = new TimeSpan(0, 3, 25);
        var result = converter.Convert(ts, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal("03:25", result);
    }

    [Fact]
    public void TimeSpanToStringConverter_ShouldFormatHhMmSs()
    {
        var converter = new TimeSpanToStringConverter();
        var ts = new TimeSpan(1, 42, 17);
        var result = converter.Convert(ts, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal("1:42:17", result);
    }
}
