using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Desktop.Converters
{
    public class ConfidenceToColorConverter : IValueConverter
    {
        public static readonly ConfidenceToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is VerificationConfidence confidence)
            {
                return confidence switch
                {
                    VerificationConfidence.High => new SolidColorBrush(Color.Parse("#4CAF50")),
                    VerificationConfidence.Medium => new SolidColorBrush(Color.Parse("#FF9800")),
                    VerificationConfidence.Low => new SolidColorBrush(Color.Parse("#9E9E9E")),
                    VerificationConfidence.Conflict => new SolidColorBrush(Color.Parse("#F44336")),
                    _ => new SolidColorBrush(Color.Parse("#9E9E9E"))
                };
            }

            return new SolidColorBrush(Color.Parse("#9E9E9E"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
