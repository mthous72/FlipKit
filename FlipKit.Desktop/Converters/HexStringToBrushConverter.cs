using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlipKit.Desktop.Converters
{
    /// <summary>
    /// Turns a "#RRGGBB" hex string into a SolidColorBrush. Used by the Phase 2 tier
    /// badge so the ViewModel can decide the colour without any XAML branching.
    /// Returns transparent when the string isn't parseable.
    /// </summary>
    public class HexStringToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string hex && Color.TryParse(hex, out var parsed))
                return new SolidColorBrush(parsed);
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
