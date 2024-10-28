namespace AltRecur
{
    public static class LocalDateTimeAndPeriodExtensions
    {
        public static LocalDateTimeAndPeriod? IntersectDt(this IEnumerable<LocalDateTimeAndPeriod> ts)
        {
            var t = ts.Max(x => x.T);
            var end = ts.Min(x => x.T.Plus(x.Period));

            if (end <= t)
                return null;

            return new(t, end.Minus(t));
        }
    }
}
