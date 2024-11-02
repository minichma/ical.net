namespace AltRecur
{
    public static class LocalDateTimeAndPeriodExtensions
    {
        public static LocalDateTimePeriod? IntersectDt(this IEnumerable<LocalDateTimePeriod> ts)
        {
            var t = ts.Max(x => x.Start);
            var end = ts.Min(x => x.End);

            if (end <= t)
                return null;

            return new(t, end);
        }
    }
}
