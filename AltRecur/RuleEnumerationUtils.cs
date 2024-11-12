using NodaTime;
using System.Globalization;

namespace AltRecur
{
    public static class RuleEnumerationUtils
    {
        public static LocalDateTime FloorTo(this LocalDateTime t, PeriodUnits PeriodUnits, IsoDayOfWeek startOfWeek)
            => PeriodUnits switch
            {
                PeriodUnits.Seconds => t.Date.At(new LocalTime(t.Hour, t.Minute, t.Second)),
                PeriodUnits.Minutes => t.Date.At(new LocalTime(t.Hour, t.Minute)),
                PeriodUnits.Hours => t.Date.At(new LocalTime(t.Hour, 0)),
                PeriodUnits.Days => t.Date.AtMidnight(),
                PeriodUnits.Weeks => GetStartOfWeek(t, startOfWeek),
                PeriodUnits.Months => new LocalDate(t.Year, t.Month, 1, t.Calendar).AtMidnight(),
                PeriodUnits.Years => new LocalDate(t.Year, 1, 1, t.Calendar).AtMidnight(),
                _ => throw new ApplicationException()
            };

        internal static Period GetPeriod(PeriodUnits PeriodUnits, int n = 1)
            => PeriodUnits switch
            {
                PeriodUnits.Seconds => Period.FromSeconds(n),
                PeriodUnits.Minutes => Period.FromMinutes(n),
                PeriodUnits.Hours => Period.FromHours(n),
                PeriodUnits.Days => Period.FromDays(n),
                PeriodUnits.Months => Period.FromMonths(n),
                PeriodUnits.Years => Period.FromYears(n),
                PeriodUnits.Weeks => Period.FromWeeks(n),
                _ => throw new ApplicationException()
            };

        private static int GetUnitFromLocalDateTime(LocalDateTime t, PeriodUnits units)
            => units switch
            {
                PeriodUnits.Seconds => t.Second,
                PeriodUnits.Minutes => t.Minute,
                PeriodUnits.Hours => t.Hour,
                PeriodUnits.Days => t.Day,
                PeriodUnits.Months => t.Month,
                PeriodUnits.Years => t.Year,
                _ => throw new ApplicationException()
            };

        internal static int GetPeriodUnits(Period t, PeriodUnits PeriodUnits)
            => PeriodUnits switch
            {
                PeriodUnits.Seconds => (int)t.Seconds,
                PeriodUnits.Minutes => (int)t.Minutes,
                PeriodUnits.Hours => (int)t.Hours,
                PeriodUnits.Days => t.Days,
                PeriodUnits.Weeks => t.Weeks,
                PeriodUnits.Months => t.Months,
                PeriodUnits.Years => t.Years,
                _ => throw new ApplicationException()
            };

        public static LocalDateTimePeriod FindCurrentOrNextInterval(LocalDateTime dtStart, LocalDateTime t, PeriodUnits unit, int interval, Period? offset, IsoDayOfWeek startOfWeek)
        {
            var dtStartFloored = FloorTo(dtStart, unit, startOfWeek);
            if (offset != null)
            {
                dtStartFloored = dtStartFloored.Plus(offset);
                if (dtStartFloored > dtStart)
                    dtStartFloored = dtStartFloored.Plus(-GetPeriod(unit));
            }

            var dt = Period.Between(dtStartFloored, t, unit);
            var dUnits = GetPeriodUnits(dt, unit);
            var incUnits = unchecked((int)((((uint)dUnits) + interval - 1) / (uint)interval * interval));

            var inc = GetPeriod(unit, incUnits);
            var res = dtStartFloored.Plus(inc);
            return new(res, res.Plus(GetPeriod(unit)));
        }

        public delegate LocalDateTimePeriod IncTimeDelegate(LocalDateTime t);

        private class LocalDateTimeAndPeriodHolder(LocalDateTimePeriod value)
        {
            public LocalDateTimePeriod Value { get; set; } = value;
        }

        public static Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> EnumerateWithCount(LocalDateTime dtStart, Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> inner, int count)
            => arg => inner((dtStart, arg.end))
                .Take(count)
                .Where(x => x >= arg.start);

        public static Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> EnumerateWithUntil(LocalDateTime dtStart, Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> inner, LocalDateTime? until)
        {
            return arg =>
            {
                var start = (dtStart > arg.start) ? dtStart : arg.start;
                var end = arg.end ?? until;
                if ((arg.end != null) && (until != null) && (arg.end > until))
                    end = until.Value.PlusTicks(1);
                else
                    end = arg.end ?? until;

                return inner((start, end));
            };
        }

        internal static Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> EnumerateWithSetPos(PeriodUnits freq, Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> inner, int[] setPos, IsoDayOfWeek startOfWeek)
            => arg => EnumerateWithSetPos(arg.start, arg.end, freq, inner, setPos, startOfWeek);

        internal static IEnumerable<LocalDateTime> EnumerateWithSetPos(LocalDateTime start, LocalDateTime? end, PeriodUnits freq, Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> inner, int[] setPos, IsoDayOfWeek startOfWeek)
        {
            LocalDateTime? setPeriodStart = null;
            HashSet<int> by = [];
            int currentSetPos = 0;

            foreach (var item in inner((FloorTo(start, freq, startOfWeek), end)))
            {
                // FloorTo won't work with weeks
                var itemPeriodStart = FloorTo(item, freq, startOfWeek);
                if (setPeriodStart != itemPeriodStart)
                {
                    var nofEntries = inner((itemPeriodStart, itemPeriodStart.Plus(GetPeriod(freq)))).Count();
                    by = setPos.Select(x => (x < 0) ? (nofEntries + 1 + x) : x)
                        .Where(x => x > 0)
                        .Where(x => x <= nofEntries)
                        .ToHashSet();

                    currentSetPos = 1;
                    setPeriodStart = itemPeriodStart;
                }

                if (by.Contains(currentSetPos) && (item >= start))
                    yield return item;

                currentSetPos++;
            }
        }

        public static Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> Enumerate(IRuleIncrementor[] components)
            => arg => Enumerate(arg.start, arg.end, components);

        public static IEnumerable<LocalDateTime> Enumerate(LocalDateTime start, LocalDateTime? end, IRuleIncrementor[] components)
        {
            var state = components.Select(x => (inc: x, t: new LocalDateTimeAndPeriodHolder(x.CurrentOrNext(start)))).ToArray();
            while ((end == null) || !state.Any(s => s.t.Value.Start > end))
            {
                var comb = state.Select(x => x.t.Value).IntersectDt();

                if (comb != null)
                    yield return comb.Start;

                var minEnd = state.Select(x => x.t.Value.End).Min();
                var maxStart = state.Select(x => x.t.Value.Start).Max();
                var threshold = (minEnd > maxStart) ? minEnd : maxStart;

                bool proceeded;
                do
                {
                    proceeded = false;
                    foreach (var item in state)
                    {
                        if ((item.t.Value.End) <= threshold)
                        {
                            item.t.Value = item.inc.CurrentOrNext(threshold);
                            if (threshold < item.t.Value.Start)
                            {
                                threshold = item.t.Value.Start;
                                proceeded = true;
                            }
                        }
                    }
                } while (proceeded);
            }
        }

        internal static LocalDateTime GetStartOfWeekOne(LocalDateTime t, IsoDayOfWeek startOfWeek)
        {
            var weekNo = GetWeekNo(t, startOfWeek);
            t = FloorTo(t, PeriodUnits.Days, startOfWeek);
            t = t.PlusWeeks(-weekNo + 1);
            if (t.DayOfWeek != startOfWeek)
                t = t.Previous(startOfWeek);

            return t;
        }

        internal static int GetWeekNo(LocalDateTime t, IsoDayOfWeek startOfWeek)
        {
            // We add 3 to make sure the test date is in the 'right' year, because
            // otherwise we might end up with week 53 in a year that only has 52.
            var tTest = GetStartOfWeek(t, startOfWeek).PlusDays(3);
            var cal = new GregorianCalendar();
            var res = cal.GetWeekOfYear(tTest.ToDateTimeUnspecified(), CalendarWeekRule.FirstFourDayWeek, ToNetDayOfWeek(startOfWeek));

            return res;
        }

        internal static LocalDateTime GetStartOfWeek(LocalDateTime t, IsoDayOfWeek startOfWeek)
        {
            if (startOfWeek == IsoDayOfWeek.None)
                throw new ArgumentException();

            var t0 = ((int)startOfWeek) % 7;
            var t1 = ((int)t.DayOfWeek) % 7;
            return t.PlusDays(-((t1 + 7 - t0) % 7));
        }

        private static DayOfWeek ToNetDayOfWeek(IsoDayOfWeek dow)
        {
            return (DayOfWeek)((int)dow % 7);
        }

        internal static int GetWeeksInYear(LocalDateTime t, IsoDayOfWeek isoDayOfWeek)
        {
            var t0 = GetStartOfWeekOne(t, isoDayOfWeek);
            var t1 = GetStartOfWeekOne(t.PlusYears(1).PlusWeeks(1), isoDayOfWeek);

            return Period.DaysBetween(t0.Date, t1.Date) / 7;
        }

        public static PeriodUnits ToNodaPeriodUnits(this RuleFrequency freq)
            => freq switch
            {
                RuleFrequency.Secondly => PeriodUnits.Seconds,
                RuleFrequency.Minutely => PeriodUnits.Minutes,
                RuleFrequency.Hourly => PeriodUnits.Hours,
                RuleFrequency.Daily => PeriodUnits.Days,
                RuleFrequency.Weekly => PeriodUnits.Weeks,
                RuleFrequency.Monthly => PeriodUnits.Months,
                RuleFrequency.Yearly => PeriodUnits.Years,
                _ => throw new NotSupportedException()
            };

        public static Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> BuildEnumerationFactory(RecurrenceRule rule)
        {
            if (!rule.IsValid())
                throw new FormatException();

            var startOfWeek = rule.WeekStart ?? IsoDayOfWeek.Monday;
            // The offset of the start of week relative to Monday (Tuesday = +1, ...)
            var weekDayOffset = NodaTime.Period.FromDays((int)startOfWeek - (int)IsoDayOfWeek.Monday);
            IEnumerable<IRuleIncrementor> components = [new RuleIncrementor(
                t => FindCurrentOrNextInterval(rule.DtStart, t, rule.Frequency.ToNodaPeriodUnits(), rule.Interval, (rule.Frequency == RuleFrequency.Weekly) ? weekDayOffset : null, startOfWeek))];

            RuleFrequency GetByDayOuterFreq()
                => rule.Frequency switch
                {
                    RuleFrequency.Monthly => RuleFrequency.Monthly,
                    RuleFrequency.Yearly when (rule.ByMonth != null) => RuleFrequency.Monthly,
                    RuleFrequency.Yearly => RuleFrequency.Yearly,
                    _ => RuleFrequency.Undefined
                };

            IRuleIncrementor BuildByComponent(ByPart byPart, IReadOnlySet<int> byValues)
            {
                var dsr = RecurrenceExpandRules.ByPartDescriptors[byPart];

                return byPart switch
                {
                    ByPart.ByDay => ByPartIncrementor.BuildByDay(rule.ByDay!.ToArray(), GetByDayOuterFreq(), startOfWeek),
                    _ => ByPartIncrementor.Build(byValues.ToArray(), startOfWeek, byPart)
                };
            }

            var byRules = rule.GetByRules();
            components = components.Concat(byRules
                .Where(byRule => byRule.Key != ByPart.BySetPos)
                // time-related BY parts MUST be ignored, if DTSTART is of type DATE (i.e. date-only)
                .Where(byRule => rule.hasTime || !RecurrenceExpandRules.ByPartDescriptors[byRule.Key].IsTime)
                .Select(byRule => BuildByComponent(byRule.Key, byRule.Value)));

            IRuleIncrementor BuildFallbackExpandByComponent(ByPart byPart)
            {
                var dsr = RecurrenceExpandRules.ByPartDescriptors[byPart];
                return byPart switch
                {
                    ByPart.ByDay => ByPartIncrementor.BuildByDay([(rule.DtStart.DayOfWeek, null)], RuleFrequency.Undefined, startOfWeek),
                    _ => ByPartIncrementor.Build([GetUnitFromLocalDateTime(rule.DtStart, dsr.InnerUnit)], startOfWeek, byPart)
                };
            }

            var missingByPartsWithExpansion = RecurrenceExpandRules.FallbackRules
                .Where(x => !byRules.ContainsKey(x.Key))
                .Where(x => RecurrenceExpandRules.FallbackRules.TryGetValue(x.Key, out var predicate) && predicate(rule))
                .Select(x => x.Key);

            components = components.Concat(missingByPartsWithExpansion.Select(BuildFallbackExpandByComponent));

            var enumFactory = Enumerate(components.ToArray());

            if (rule.BySetPos != null)
                enumFactory = EnumerateWithSetPos(rule.Frequency.ToNodaPeriodUnits(), enumFactory, rule.BySetPos.ToArray(), startOfWeek);

            if (rule.Count is not null)
                enumFactory = EnumerateWithCount(rule.DtStart, enumFactory, rule.Count.Value);
            else
                enumFactory = EnumerateWithUntil(rule.DtStart, enumFactory, rule.Until);

            return enumFactory;
        }

        public static IEnumerable<LocalDateTime> Enumerate(RecurrenceRule rule, LocalDateTime? start = null, LocalDateTime? end = null)
            => BuildEnumerationFactory(rule)((start ?? rule.DtStart, end));
    }
}
