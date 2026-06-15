using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HaRepacker.Converters
{
    // Avalonia handles DPI scaling automatically in its layout system; this is a pass-through.
    public class ImageWidthOrHeightToScreenDPIConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value;
    }
}
