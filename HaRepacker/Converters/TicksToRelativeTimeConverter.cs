using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HaRepacker.Converters
{
    public class TicksToRelativeTimeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not long ticks) return value?.ToString();

            const int SECOND = 1;
            const int MINUTE = 60 * SECOND;
            const int HOUR = 60 * MINUTE;
            const int DAY = 24 * HOUR;
            const int MONTH = 30 * DAY;

            var ts = new TimeSpan(ticks);
            double delta = Math.Abs(ts.TotalSeconds);

            if (delta < MINUTE) return $"{ts.Seconds} second(s) ago";
            if (delta < 45 * MINUTE) return $"{ts.Minutes} minute(s) ago";
            if (delta < 24 * HOUR) return $"{ts.Hours} hour(s) ago";
            if (delta < 30 * DAY) return $"{ts.Days} day(s) ago";
            if (delta < 12 * MONTH) return $"{(int)Math.Floor((double)ts.Days / 30)} month(s) ago";
            return $"{(int)Math.Floor((double)ts.Days / 365)} year(s) ago";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => 0L;
    }
}
