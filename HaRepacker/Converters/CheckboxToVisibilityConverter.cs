using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HaRepacker.Converters
{
    public class CheckboxToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => false;
    }
}
