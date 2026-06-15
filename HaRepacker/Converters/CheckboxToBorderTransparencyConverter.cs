using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace HaRepacker.Converters
{
    public class CheckboxToBorderTransparencyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? new SolidColorBrush(Colors.Gray) : (object)new SolidColorBrush(Colors.Transparent);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => false;
    }
}
