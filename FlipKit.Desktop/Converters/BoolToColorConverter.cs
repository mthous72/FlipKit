using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlipKit.Desktop.Converters
{
    /// <summary>
    /// Converts a boolean value to a color (Green for true, Gray for false).
    /// Used for status indicators.
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public static readonly BoolToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue
                    ? new SolidColorBrush(Color.Parse("#4CAF50"))  // Green
                    : new SolidColorBrush(Color.Parse("#9E9E9E")); // Gray
            }

            return new SolidColorBrush(Color.Parse("#9E9E9E")); // Default to gray
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
