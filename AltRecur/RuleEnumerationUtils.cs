using NodaTime;
using NodaTime.Extensions;

namespace AltRecur
{
    public static class RuleEnumerationUtils
    {
        //public static IEnumerable<LocalDateTime> Seconds(ISkipToken skipToken, int interval)
        //    => Enumerate(skipToken, Period.FromSeconds(interval));

        //public static IEnumerable<LocalDateTime> Enumerate(ISkipToken skipToken, Period period)
        //{
        //    var next = skipToken.SkipTo;
        //    while (true)
        //    {
        //        if (next < skipToken.SkipTo)
        //            next = skipToken.SkipTo;

        //        yield return next;

        //        next = next.Plus(period);
        //    }
        //}

        private static LocalDateTime FloorTo(LocalDateTime t, PeriodUnits PeriodUnits)
        {
            var b = new PeriodBuilder();
            switch (PeriodUnits)
            {
                case PeriodUnits.Seconds:
                    b.Seconds = t.Second;
                    goto case PeriodUnits.Minutes;
                case PeriodUnits.Minutes:
                    b.Minutes = t.Minute;
                    goto case PeriodUnits.Hours;
                case PeriodUnits.Hours:
                    b.Hours = t.Hour;
                    goto case PeriodUnits.Days;
                case PeriodUnits.Days:
                    b.Days = t.Day - 1;
                    goto case PeriodUnits.Months;
                case PeriodUnits.Months:
                    b.Months = t.Month - 1;
                    goto case PeriodUnits.Years;
                case PeriodUnits.Years:
                    break;
                default:
                    throw new ApplicationException();
            }

            return new LocalDateTime(t.Year, 1, 1, 0, 0).Plus(b.Build());
        }

        private static Period GetPeriod(PeriodUnits PeriodUnits, int n = 1)
            => PeriodUnits switch
            {
                PeriodUnits.Seconds => Period.FromSeconds(n),
                PeriodUnits.Minutes => Period.FromMinutes(n),
                PeriodUnits.Hours => Period.FromHours(n),
                PeriodUnits.Days => Period.FromDays(n),
                PeriodUnits.Months => Period.FromMonths(n),
                PeriodUnits.Years => Period.FromYears(n),
                _ => throw new ApplicationException()
            };

        private static int GetUnitFromLocalDateTime(LocalDateTime t, PeriodUnits PeriodUnits)
            => PeriodUnits switch
            {
                PeriodUnits.Seconds => t.Second,
                PeriodUnits.Minutes => t.Minute,
                PeriodUnits.Hours => t.Hour,
                PeriodUnits.Days => t.Day,
                PeriodUnits.Months => t.Month,
                PeriodUnits.Years => t.Year,
                _ => throw new ApplicationException()
            };

        private static int GetPeriodUnits(Period t, PeriodUnits PeriodUnits)
            => PeriodUnits switch
            {
                PeriodUnits.Seconds => (int)t.Seconds,
                PeriodUnits.Minutes => (int)t.Minutes,
                PeriodUnits.Hours => (int)t.Hours,
                PeriodUnits.Days => t.Days,
                PeriodUnits.Months => t.Months,
                PeriodUnits.Years => t.Years,
                _ => throw new ApplicationException()
            };

        //private static int GetIncsInOuterPeriodUnits(LocalDateTime t, PeriodUnits innerPeriodUnits)
        //{
        //    var tStart = FloorTo(t, innerPeriodUnits + 1);
        //    var tNext = t.Plus(GetPeriod(innerPeriodUnits + 1));

        //    var diff = tNext.Minus(tStart);
        //}

        public static LocalDateTimeAndPeriod NextInterval(LocalDateTime dtStart, LocalDateTime t, PeriodUnits unit, int interval)
        {
            if (t < dtStart)
                return new(dtStart, GetPeriod(unit, interval));

            var dt = Period.Between(dtStart, t, unit);
            var dUnits = GetPeriodUnits(dt, unit);
            var inc = GetPeriod(unit, ((dUnits / interval) + 1) * interval);

            return new(dtStart.Plus(inc), GetPeriod(unit, interval));
        }

        public static LocalDateTimeAndPeriod FindCurrentOrNextBy(LocalDateTime t, PeriodUnits outerUnit, PeriodUnits innerUnit, int[] by, bool supportNegative = false)
        {
            // Floor to period boundary (start of sec, min, hour)
            var outerStart = FloorTo(t, outerUnit);

            bool first = true;
            int next;
            do
            {
                var preparedBy = PrepareByArray(outerStart, outerUnit, innerUnit, by, supportNegative);

                next = first
                    ? preparedBy.Where(x => x >= GetUnitFromLocalDateTime(t, innerUnit)).FirstOrDefault(-1)
                    : preparedBy.FirstOrDefault(-1);

                if (next < 0)
                {
                    outerStart = outerStart.Plus(GetPeriod(outerUnit));
                    preparedBy = PrepareByArray(outerStart, outerUnit, innerUnit, by, supportNegative);
                }

                first = false;
            } while (next < 0);

            outerStart = outerStart.Plus(GetPeriod(innerUnit, next - GetUnitFromLocalDateTime(outerStart, innerUnit)));

            return new(outerStart, GetPeriod(innerUnit, 1));
        }

        private static int[] PrepareByArray(LocalDateTime t, PeriodUnits outerUnit, PeriodUnits innerUnit, int[] by, bool supportNegative)
        {
            var totalIncs = GetPeriodUnits(Period.Between(t, t.Plus(GetPeriod(outerUnit)), innerUnit), innerUnit);
            var v0 = GetUnitFromLocalDateTime(t, innerUnit);

            if (supportNegative)
                by = by.Select(x => (x >= 0) ? x : (totalIncs + 1 + x)).OrderBy(x => x).ToArray();

            by = by.Where(x => (x >= v0) && (x < (totalIncs + v0))).ToArray();
            return by;
        }

        //public static IEnumerable<LocalDateTimeAndPeriod> EnumerateInterval(LocalDateTime start, PeriodUnits unit, int interval)
        //{
        //    var t = start;
        //    while (true)
        //    {
        //        yield return new(t, Period.FromTicks(1));
        //        t = NextInterval(start, t, unit, interval);
        //    }
        //}

        public delegate LocalDateTimeAndPeriod IncTimeDelegate(LocalDateTime t);

        private class LocalDateTimeAndPeriodHolder(LocalDateTimeAndPeriod value)
        {
            public LocalDateTimeAndPeriod Value { get; set; } = value;
        }

        public static IEnumerable<LocalDateTime> Enumerate(LocalDateTime dtStart, IncTimeDelegate[] components)
        {
            var state = components.Select(x => (del: x, t: new LocalDateTimeAndPeriodHolder(x(dtStart.PlusTicks(-1))))).ToArray();
            while (true)
            {
                var comb = state.Select(x => x.t.Value).IntersectDt();

                if (comb != null)
                {
                    yield return comb.T;
                    var minEnd = state.MinBy(x => x.t.Value.T + x.t.Value.Period);
                    minEnd.t.Value = minEnd.del(minEnd.t.Value.T + minEnd.t.Value.Period);
                }
                else
                {
                    var maxStart = state.MaxBy(x => x.t.Value.T).t.Value.T;

                    foreach (var item in state.Where(x => (x.t.Value.T + x.t.Value.Period) <= maxStart))
                        item.t.Value = item.del(maxStart);
                }
            }
        }

        //public static IEnumerable<LocalDateTimeAndPeriod> EnumerateBy(LocalDateTime start, PeriodUnits outerUnit, PeriodUnits innerUnit, int[] by, bool supportNegative = false)
        //{
        //    var t = FloorTo(start, innerUnit);
        //    var period = GetPeriod(innerUnit);
        //    while (true)
        //    {
        //        yield return new(t, period);
        //        t = NextTime(t, outerUnit, innerUnit, by, supportNegative);
        //    }
        //}

        public static IEnumerable<LocalDateTimeAndPeriod> EnumerateByYearDay(LocalDate start, int[] by)
            => EnumerateByYearOrMonthDay(new LocalDate(start.Year, 1, 1), Period.FromYears(1), by);

        public static IEnumerable<LocalDateTimeAndPeriod> EnumerateByMonthDay(LocalDate start, int[] by)
            => EnumerateByYearOrMonthDay(new LocalDate(start.Year, start.Month, 1), Period.FromMonths(1), by);

        private static IEnumerable<LocalDateTimeAndPeriod> EnumerateByYearOrMonthDay(LocalDate intervalStart, Period intervalPeriod, int[] by)
        {
            while (true)
            {
                var daysInInterval = Period.DaysBetween(intervalStart, intervalStart.Plus(intervalPeriod));
                var byNormalized = by
                    .Select(x => (x < 0) ? daysInInterval + 1 + x : x)
                    .Where(x => (x >= 1) && (x <= daysInInterval))
                    .OrderBy(x => x)
                    .ToArray();

                foreach (var c in byNormalized)
                    yield return new(intervalStart.PlusDays(c - 1).AtMidnight(), Period.FromDays(1));

                intervalStart = intervalStart.Plus(intervalPeriod);
            }
        }

        public static IEnumerable<LocalDateTimeAndPeriod> OrderedIntersectDt(this IEnumerable<LocalDateTimeAndPeriod> items1, IEnumerable<LocalDateTimeAndPeriod> items2)
        {
            using var it1 = items1.GetEnumerator();
            using var it2 = items2.GetEnumerator();

            var iterators = new[] { it1, it2 }.ToList();

            bool MoveAllNext()
                => iterators.All(x => x.MoveNext());

            MoveAllNext();

            while (true)
            {
                var nextVal = iterators.Select(x => x.Current).IntersectDt();
                if (nextVal != null)
                    yield return nextVal;

                if (!iterators.MinBy(x => x.Current.T + x.Current.Period)!.MoveNext())
                    break;
            }
        }
    }
}
