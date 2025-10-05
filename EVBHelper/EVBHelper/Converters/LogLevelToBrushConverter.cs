using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using EVBHelper.Services;

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

            return level switch
            {
                RfelLogLevel.Info => Brushes.Black,
                RfelLogLevel.Warning => Brushes.DarkGoldenrod,
                RfelLogLevel.Error => Brushes.DarkRed,
                _ => Brushes.Black
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
