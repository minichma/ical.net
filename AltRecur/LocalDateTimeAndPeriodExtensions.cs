namespace AltRecur
{
    public static class LocalDateTimeAndPeriodExtensions
    {
        public static LocalDateTimeAndPeriod? IntersectDt(this IEnumerable<LocalDateTimeAndPeriod> ts)
        {
            var t = ts.Max(x => x.Start);
            var end = ts.Min(x => x.Start.Plus(x.Period));

            if (end <= t)
                return null;

            return new(t, end.Minus(t));
        }
    }
}
