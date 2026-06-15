using Avalonia.Data.Converters;
using HaRepacker.Models;
using System;
using System.Globalization;

namespace HaRepacker.Converters
{
    public class PointFOriginToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is NotifyPointF p ? $"X {p.X}, Y {p.Y}" : value?.ToString();

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => new NotifyPointF(0, 0);
    }
}
