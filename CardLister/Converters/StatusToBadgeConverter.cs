using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Desktop.Converters
{
    public class StatusToBadgeConverter : IValueConverter
    {
        public static readonly StatusToBadgeConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is CardStatus status)
            {
                return status switch
                {
                    CardStatus.Draft => "Draft",
                    CardStatus.Priced => "Priced",
                    CardStatus.Ready => "Ready",
                    CardStatus.Listed => "Listed",
                    CardStatus.Sold => "Sold",
                    _ => status.ToString()
                };
            }
            return "--";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
