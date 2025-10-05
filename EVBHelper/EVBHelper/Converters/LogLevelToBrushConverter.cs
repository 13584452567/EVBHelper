using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using EVBHelper.Services;
using System;
using System.Globalization;

namespace EVBHelper.Converters
{
    public sealed class LogLevelToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not RfelLogLevel level)
            {
                return Brushes.Black;
            }

            var theme = Application.Current?.ActualThemeVariant;

            if (theme == ThemeVariant.Dark)
            {
                return level switch
                {
                    RfelLogLevel.Info => Brushes.White,
                    RfelLogLevel.Warning => Brushes.Yellow,
                    RfelLogLevel.Error => Brushes.Red,
                    _ => Brushes.White
                };
            }
            else
            {
                return level switch
                {
                    RfelLogLevel.Info => Brushes.Black,
                    RfelLogLevel.Warning => Brushes.DarkGoldenrod,
                    RfelLogLevel.Error => Brushes.DarkRed,
                    _ => Brushes.Black
                };
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
