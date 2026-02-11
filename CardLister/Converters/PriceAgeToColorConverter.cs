using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlipKit.Desktop.Converters
{
    public class PriceAgeToColorConverter : IValueConverter
    {
        public static readonly PriceAgeToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not DateTime priceDate)
                return new SolidColorBrush(Colors.Gray);

            var days = (DateTime.UtcNow - priceDate).TotalDays;

            return days switch
            {
                < 14 => new SolidColorBrush(Colors.Green),
                < 30 => new SolidColorBrush(Colors.Orange),
                _ => new SolidColorBrush(Colors.Red)
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
