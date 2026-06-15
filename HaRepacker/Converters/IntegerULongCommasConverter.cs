using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HaRepacker.Converters
{
    public class IntegerULongCommasConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value switch
            {
                ulong ul => ul.ToString("N0", culture),
                long l => l.ToString("N0", culture),
                int i => i.ToString("N0", culture),
                _ => value?.ToString()
            };

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string s ? ulong.TryParse(s.Replace(",", ""), out var v) ? v : (object?)null : null;
    }
}
