using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using Openix.Logging;
using System;
using System.Globalization;

namespace EVBHelper.Converters;

public sealed class OpenixLogLevelToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not OpenixLogLevel level)
        {
            return Brushes.Gray;
        }

        var theme = Application.Current?.ActualThemeVariant;
        var isDark = theme == ThemeVariant.Dark;

        return level switch
        {
            OpenixLogLevel.Info => isDark ? Brushes.White : Brushes.Black,
            OpenixLogLevel.Data => isDark ? Brushes.LightGreen : Brushes.DarkGreen,
            OpenixLogLevel.Warning => isDark ? Brushes.LightGoldenrodYellow : Brushes.DarkGoldenrod,
            OpenixLogLevel.Error => isDark ? Brushes.Salmon : Brushes.DarkRed,
            OpenixLogLevel.Debug => isDark ? Brushes.LightGray : Brushes.Gray,
            _ => Brushes.Gray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
