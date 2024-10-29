using Ical.Net.DataTypes;
using NodaTime;
using System;

namespace Ical.Net.Utility
{
    public static class NodaExtensions
    {
        public static LocalDateTime ToNodaLocalDateTime(this IDateTime dt)
            => LocalDateTime.FromDateTime(dt.Value);

        public static PeriodUnits ToNodaPeriodUnits(this FrequencyType freq)
            => freq switch
            {
                FrequencyType.Secondly => PeriodUnits.Seconds,
                FrequencyType.Minutely => PeriodUnits.Minutes,
                FrequencyType.Hourly => PeriodUnits.Hours,
                FrequencyType.Daily => PeriodUnits.Days,
                FrequencyType.Weekly => PeriodUnits.Weeks,
                FrequencyType.Monthly => PeriodUnits.Months,
                FrequencyType.Yearly => PeriodUnits.Years,
                _ => throw new NotSupportedException()
            };

        public static IsoDayOfWeek ToNodaIsoDayOfWeek(this DayOfWeek dow)
            => (dow == DayOfWeek.Sunday) ? IsoDayOfWeek.Sunday : (IsoDayOfWeek)dow;

        public static CalDateTime ToCalDateTime(this LocalDateTime t, string tzId = null)
            => new CalDateTime(t.ToDateTimeUnspecified(), tzId) { HasTime = true };

        public static int GetLocalTimeComponent(this LocalDateTime t, PeriodUnits unit)
            => unit switch
            {
                PeriodUnits.Seconds => t.Second,
                PeriodUnits.Minutes => t.Minute,
                PeriodUnits.Hours => t.Hour,
                PeriodUnits.Days => t.Day,
                PeriodUnits.Months => t.Month,
                PeriodUnits.Years => t.Year,
                _ => throw new NotSupportedException()
            };
    }
}
