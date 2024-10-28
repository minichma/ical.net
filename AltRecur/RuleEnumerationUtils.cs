using NodaTime;

namespace AltRecur
{
    public static class RuleEnumerationUtils
    {
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

        public static LocalDateTimeAndPeriod NextInterval(LocalDateTime dtStart, LocalDateTime t, PeriodUnits unit, int interval)
        {
            if (t < dtStart)
                return new(dtStart, GetPeriod(unit, interval));

            var dt = Period.Between(dtStart, t, unit);
            var dUnits = GetPeriodUnits(dt, unit);
            var inc = GetPeriod(unit, ((dUnits / interval) + 1) * interval);

            return new(dtStart.Plus(inc), GetPeriod(unit, interval));
        }

        private static LocalDateTimeAndPeriod FindCurrentOrNextByWeekDay(LocalDateTime t, IsoDayOfWeek[] by)
        {
            // Floor to period boundary (start of sec, min, hour)
            t = t.Date.AtMidnight();

            while (!by.Contains(t.DayOfWeek))
                t = t.PlusDays(1);

            return new(t, Period.FromDays(1));
        }

        public static LocalDateTimeAndPeriod FindCurrentOrNextByDay(LocalDateTime t, PeriodUnits outerUnit, (IsoDayOfWeek dow, int? ord)[] by)
        {
            var byWithOrd = by.Where(x => x.ord.HasValue).Select(x => (x.dow, ord: x.ord!.Value)).ToArray();
            var byWithoutOrd = by.Where(x => !x.ord.HasValue).Select(x => x.dow).ToArray();

            LocalDateTimeAndPeriod[] t1 = byWithOrd.Any() ? [FindCurrentOrNextByDayWithOrd(t, outerUnit, byWithOrd)] : [];
            LocalDateTimeAndPeriod[] t2 = byWithOrd.Any() ? [FindCurrentOrNextByWeekDay(t, byWithoutOrd)] : [];

            LocalDateTimeAndPeriod[] candidates = [.. t1, .. t2];

            var res = candidates.MinBy(x => x.T);
            return res!;
        }

        private static LocalDateTimeAndPeriod FindCurrentOrNextByDayWithOrd(LocalDateTime t, PeriodUnits outerUnit, (IsoDayOfWeek dow, int ord)[] by)
            => FindCurrentOrNextBy(t, outerUnit, PeriodUnits.Days, periodStart => GetByDaysWithOrd(periodStart, outerUnit, by));

        private static int[] GetByDaysWithOrd(LocalDateTime t, PeriodUnits outerUnit, (IsoDayOfWeek dow, int ord)[] by)
        {
            var totalIncs = GetPeriodUnits(Period.Between(t, t.Plus(GetPeriod(outerUnit)), PeriodUnits.Days), PeriodUnits.Days);
            var startDoW = t.DayOfWeek;

            var res = by.Select(x => GetMonthOrYearDayFromDowAndOrd(startDoW, totalIncs, x.dow, x.ord));

            res = res.Where(x => (x >= 1) && (x < (totalIncs + 1)));
            return res.ToArray();
        }

        private static int GetMonthOrYearDayFromDowAndOrd(IsoDayOfWeek startDoW, int totalDays, IsoDayOfWeek dow, int ord)
        {
            if (ord < 0)
            {
                var nofDowInPeriod = ((totalDays - (dow - startDoW + 7) % 7) + 6) / 7;
                ord = nofDowInPeriod + 1 + ord;
            }

            return ((ord - 1) * 7) + (dow - startDoW + 7) % 7 + 1;
        }

        public static LocalDateTimeAndPeriod FindCurrentOrNextBy(LocalDateTime t, PeriodUnits outerUnit, PeriodUnits innerUnit, int[] by, bool supportNegative = false)
            => FindCurrentOrNextBy(t, outerUnit, innerUnit, t => PrepareByArray(t, outerUnit, innerUnit, by, supportNegative));

        private static LocalDateTimeAndPeriod FindCurrentOrNextBy(LocalDateTime t, PeriodUnits outerUnit, PeriodUnits innerUnit, Func<LocalDateTime, int[]> getByByPeriod)
        {
            // Floor to period boundary (start of sec, min, hour)
            var outerStart = FloorTo(t, outerUnit);

            bool first = true;
            int next;
            do
            {
                var preparedBy = getByByPeriod(outerStart);

                next = first
                    ? preparedBy.Where(x => x >= GetUnitFromLocalDateTime(t, innerUnit)).FirstOrDefault(-1)
                    : preparedBy.FirstOrDefault(-1);

                if (next < 0)
                {
                    outerStart = outerStart.Plus(GetPeriod(outerUnit));
                    preparedBy = getByByPeriod(outerStart);
                }

                first = false;
            } while (next < 0);

            outerStart = outerStart.Plus(GetPeriod(innerUnit, next - GetUnitFromLocalDateTime(outerStart, innerUnit)));

            return new(outerStart, GetPeriod(innerUnit));
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
    }
}
