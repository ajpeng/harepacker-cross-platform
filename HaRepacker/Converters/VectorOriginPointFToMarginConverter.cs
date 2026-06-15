using Avalonia;
using Avalonia.Data.Converters;
using HaRepacker.Models;
using System;
using System.Globalization;

namespace HaRepacker.Converters
{
    // Converts a NotifyPointF vector origin to an Avalonia Thickness (Margin).
    // Avalonia's layout system handles DPI scaling, so coordinates are used directly.
    public class VectorOriginPointFToMarginConverter : IValueConverter
    {
        private const float CrossHairHalf = 10f / 2f;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not NotifyPointF p) return new Thickness(0);
            return new Thickness(p.X, p.Y - CrossHairHalf, 0, 0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Thickness t) return new NotifyPointF(0, 0);
            return new NotifyPointF((float)t.Left, (float)(t.Top + CrossHairHalf));
        }
    }
}
