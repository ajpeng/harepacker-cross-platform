using Avalonia.Data.Converters;
using HaRepacker.Models;
using System;
using System.Globalization;

namespace HaRepacker.Converters
{
    public class PointFToVisiblityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is NotifyPointF p && (p.X != 0 || p.Y != 0);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => new NotifyPointF(0, 0);
    }
}
