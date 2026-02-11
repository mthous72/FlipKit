using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FlipKit.Desktop.Converters
{
    /// <summary>
    /// Converts between nullable decimal and string, handling empty strings gracefully.
    /// Empty string or whitespace converts to null, avoiding binding errors.
    /// </summary>
    public class NullableDecimalConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Decimal? -> String
            if (value is decimal decimalValue)
            {
                return decimalValue.ToString("0.##", CultureInfo.InvariantCulture);
            }

            // null -> empty string
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // String -> Decimal?
            if (value is string stringValue)
            {
                // Empty or whitespace -> null (no error)
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return null;
                }

                // Try parse
                if (decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }

                // Invalid input -> return null (graceful fallback)
                return null;
            }

            return null;
        }
    }
}
