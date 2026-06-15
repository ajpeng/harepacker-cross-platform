using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HaRepacker.Converters
{
    public class ImageSizeDoubleToIntegerConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is double d ? (int)d : value is int ? value : value;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is int i ? (double)i : value;
    }
}
