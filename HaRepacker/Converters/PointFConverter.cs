using HaRepacker.Models;
using System;
using System.ComponentModel;
using System.Globalization;

namespace HaRepacker.Converters
{
    public class PointFConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string s)
            {
                var parts = s.Split(',');
                if (parts.Length == 2
                    && float.TryParse(parts[0], NumberStyles.Float, culture, out float x)
                    && float.TryParse(parts[1], NumberStyles.Float, culture, out float y))
                    return new NotifyPointF(x, y);
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is NotifyPointF p)
                return $"{p.X},{p.Y}";
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
