using NodaTime;

namespace AltRecur
{
    internal static class ByArrayResolver
    {
        private class StaticByArrayResolver(int[] by) : IByArrayResolver
        {
            public int[] GetResolvedByArray(LocalDateTime t)
                => by;
        }

        public static IByArrayResolver Static(int[] by)
            => new StaticByArrayResolver(by.OrderBy(x => x).ToArray());

        private class DynamicByArrayResolver(Func<LocalDateTime, int[]> f) : IByArrayResolver
        {
            public int[] GetResolvedByArray(LocalDateTime t) => f(t);
        }

        public static IByArrayResolver DynamicBy(int[] by, PeriodUnits outerUnit, PeriodUnits innerUnit)
        {
            int[] GetResolvedByArray(LocalDateTime t)
            {
                var outerFlooredT = RuleEnumerationUtils.FloorTo(t, outerUnit, IsoDayOfWeek.None);
                var totalIncs = RuleEnumerationUtils.GetPeriodUnits(Period.Between(outerFlooredT, outerFlooredT.Plus(RuleEnumerationUtils.GetPeriod(outerUnit)), innerUnit), innerUnit);
                var res = by
                    .Select(x => (x >= 0) ? x : (totalIncs + 1 + x))
                    .Where(x => (x <= totalIncs))
                    .OrderBy(x => x)
                    .ToArray();

                return res;
            }

            return new DynamicByArrayResolver(GetResolvedByArray);
        }

        public static IByArrayResolver DynamicByWeekNo(int[] by, IsoDayOfWeek startOfWeek)
        {
            int[] GetResolvedByArray(LocalDateTime t)
            {
                var totalIncs = RuleEnumerationUtils.GetWeeksInYear(t, startOfWeek);
                var res = by
                    .Select(x => (x >= 0) ? x : (totalIncs + 1 + x))
                    .Where(x => (x <= totalIncs))
                    .OrderBy(x => x)
                    .ToArray();

                return res;
            }

            return new DynamicByArrayResolver(GetResolvedByArray);
        }

        internal static IByArrayResolver DynamicByDayWithOrdAsDayOfPeriod((IsoDayOfWeek dow, int ord)[] by, PeriodUnits outerUnit)
        {
            int[] GetResolvedByArray(LocalDateTime t)
            {
                var startOfOuter = t.FloorTo(outerUnit, IsoDayOfWeek.None).Date;
                var endOfOuter = startOfOuter.Plus(RuleEnumerationUtils.GetPeriod(outerUnit)).PlusDays(-1);

                var res = by
                    .Select(x =>
                        (x.ord >= 0)
                        ? startOfOuter.Previous(x.dow).PlusWeeks(x.ord)
                        : endOfOuter.Next(x.dow).PlusWeeks(x.ord))
                    .Where(x => (x >= startOfOuter) && (x <= endOfOuter))
                    .Select(x => Period.DaysBetween(startOfOuter, x) + 1)
                    .OrderBy(x => x)
                    .ToArray();

                return res;
            }

            return new DynamicByArrayResolver(GetResolvedByArray);
        }
    }
}
