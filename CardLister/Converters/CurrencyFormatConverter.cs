using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FlipKit.Desktop.Converters
{
    public class CurrencyFormatConverter : IValueConverter
    {
        public static readonly CurrencyFormatConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal d)
                return d.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
            return "--";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s && decimal.TryParse(s, NumberStyles.Currency, CultureInfo.GetCultureInfo("en-US"), out var d))
                return d;
            return null;
        }
    }
}
