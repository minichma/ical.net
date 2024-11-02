using NodaTime;
using NodaTime.Text;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;
using System.Globalization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AltRecur
{
    public static class RuleEnumerationUtils
    {
        public static LocalDateTime FloorTo(LocalDateTime t, PeriodUnits PeriodUnits)
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
                case PeriodUnits.Weeks:
                    var tmp = FloorTo(t, PeriodUnits.Days);
                    var dow = tmp.DayOfWeek;
                    return tmp.PlusDays(-((int)dow - 1));
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

        private static int GetPeriodUnits(Period t, PeriodUnits PeriodUnits)
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

        public static LocalDateTimeAndPeriod FindCurrentOrNextInterval(LocalDateTime dtStart, LocalDateTime t, PeriodUnits unit, int interval, Period? offset)
        {
            var dtStartFloored = FloorTo(dtStart, unit);
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
            return new(res, GetPeriod(unit));
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
            LocalDateTimeAndPeriod[] t2 = byWithoutOrd.Any() ? [FindCurrentOrNextByWeekDay(t, byWithoutOrd)] : [];

            LocalDateTimeAndPeriod[] candidates = [.. t1, .. t2];

            var res = candidates.MinBy(x => x.Start);
            return res!;
        }

        private static LocalDateTimeAndPeriod FindCurrentOrNextByDayWithOrd(LocalDateTime t, PeriodUnits outerUnit, (IsoDayOfWeek dow, int ord)[] by)
            => FindCurrentOrNextByInner(t, FloorTo(t, outerUnit), t => t.Plus(GetPeriod(outerUnit)), PeriodUnits.Days, 1, periodStart => GetByDaysWithOrd(periodStart, outerUnit, by));

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
            => FindCurrentOrNextByInner(t, FloorTo(t, outerUnit), t => t.Plus(GetPeriod(outerUnit)), innerUnit, GetUnitFromLocalDateTime(FloorTo(t, outerUnit), innerUnit), t => PrepareByArray(t, outerUnit, innerUnit, by, supportNegative));

        public static LocalDateTimeAndPeriod FindCurrentOrNextByWeekNo(LocalDateTime t, int[] by, IsoDayOfWeek startOfWeek)
            => FindCurrentOrNextByInner(t, GetStartOfWeekOne(t, startOfWeek), t => t.PlusWeeks(GetWeeksInYear(t, startOfWeek)), PeriodUnits.Weeks, 1, t => PrepareByWeekNoArray(t, by, startOfWeek));

        private static LocalDateTimeAndPeriod FindCurrentOrNextByInner(LocalDateTime t, LocalDateTime outerStart, Func<LocalDateTime, LocalDateTime> incOuter, PeriodUnits innerUnit, int unitMinValue, Func<LocalDateTime, int[]> getByByPeriod)
        {
            // Floor to period boundary (start of sec, min, hour)
            bool first = true;
            int next;
            do
            {
                var preparedBy = getByByPeriod(outerStart);

                next = first
                    ? preparedBy.Where(x => x >= GetPeriodUnits(Period.Between(outerStart, t, innerUnit), innerUnit) + unitMinValue).FirstOrDefault(-1)
                    : preparedBy.FirstOrDefault(-1);

                if (next < 0)
                {
                    outerStart = incOuter(outerStart);
                    preparedBy = getByByPeriod(outerStart);
                }

                first = false;
            } while (next < 0);

            outerStart = outerStart.Plus(GetPeriod(innerUnit, next - unitMinValue));

            return new(outerStart, GetPeriod(innerUnit));
        }

        private static int[] PrepareByArray(LocalDateTime t, PeriodUnits outerUnit, PeriodUnits innerUnit, int[] by, bool supportNegative)
        {
            var outerFlooredT = FloorTo(t, outerUnit);
            var totalIncs = GetPeriodUnits(Period.Between(outerFlooredT, outerFlooredT.Plus(GetPeriod(outerUnit)), innerUnit), innerUnit);
            var v0 = GetUnitFromLocalDateTime(t, innerUnit);
            return PrepareByArray(by, supportNegative, totalIncs, v0);
        }

        private static int[] PrepareByArray(int[] by, bool supportNegative, int totalIncs, int current)
        {
            IEnumerable<int> newBy = by;
            if (supportNegative)
                newBy = newBy.Select(x => (x >= 0) ? x : (totalIncs + 1 + x));

            newBy = newBy.Where(x => (x >= current) && (x <= totalIncs));
            var res = newBy.OrderBy(x => x).ToArray();
            return res;
        }

        private static int[] PrepareByWeekNoArray(LocalDateTime t, int[] by, IsoDayOfWeek startOfWeek)
        {
            var totalIncs = GetWeeksInYear(t, startOfWeek);
            var v0 = GetWeekNo(t, startOfWeek);
            return PrepareByArray(by, true, totalIncs, v0);
        }

        public delegate LocalDateTimeAndPeriod IncTimeDelegate(LocalDateTime t);

        private class LocalDateTimeAndPeriodHolder(LocalDateTimeAndPeriod value)
        {
            public LocalDateTimeAndPeriod Value { get; set; } = value;
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

        public static Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> EnumerateWithSetPos(PeriodUnits freq, Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> inner, int[] setPos)
            => arg => EnumerateWithSetPos(arg.start, arg.end, freq, inner, setPos);

        public static IEnumerable<LocalDateTime> EnumerateWithSetPos(LocalDateTime start, LocalDateTime? end, PeriodUnits freq, Func<(LocalDateTime start, LocalDateTime? end), IEnumerable<LocalDateTime>> inner, int[] setPos)
        {
            LocalDateTime? setPeriodStart = null;
            HashSet<int> by = [];
            int currentSetPos = 0;

            foreach (var item in inner((FloorTo(start, freq), end)))
            {
                // FloorTo won't work with weeks
                var itemPeriodStart = FloorTo(item, freq);
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

                var minEnd = state.Select(x => x.t.Value.Start + x.t.Value.Period).Min();
                var maxStart = state.Select(x => x.t.Value.Start).Max();
                var threshold = (minEnd > maxStart) ? minEnd : maxStart;

                bool proceeded;
                do
                {
                    proceeded = false;
                    foreach (var item in state)
                    {
                        if ((item.t.Value.Start + item.t.Value.Period) <= threshold)
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

        private static LocalDateTime GetStartOfWeekOne(LocalDateTime t, IsoDayOfWeek isoDayOfWeek)
        {
            var weekNo = GetWeekNo(t, isoDayOfWeek);
            t = FloorTo(t, PeriodUnits.Days);
            t = t.PlusWeeks(-weekNo + 1);
            if (t.DayOfWeek != isoDayOfWeek)
                t = t.Previous(isoDayOfWeek);

            return t;
        }

        private static int GetWeekNo(LocalDateTime t, IsoDayOfWeek startOfWeek)
        {
            // We add 3 to make sure the test date is in the 'right' year, because
            // otherwise we might end up with week 53 in a year that only has 52.
            var tTest = GetStartOfWeek(t, startOfWeek).PlusDays(3);
            var cal = new GregorianCalendar();
            var res = cal.GetWeekOfYear(tTest.ToDateTimeUnspecified(), CalendarWeekRule.FirstFourDayWeek, ToNetDayOfWeek(startOfWeek));

            return res;
        }

        private static LocalDateTime GetStartOfWeek(LocalDateTime t, IsoDayOfWeek startOfWeek)
        {
            var t0 = ((int)startOfWeek) % 7;
            var t1 = ((int)t.DayOfWeek) % 7;
            return t.PlusDays(-((t1 + 7 - t0) % 7));
        }

        private static DayOfWeek ToNetDayOfWeek(IsoDayOfWeek dow)
        {
            return (DayOfWeek)((int)dow % 7);
        }

        private static int GetWeeksInYear(LocalDateTime t, IsoDayOfWeek isoDayOfWeek)
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

            // The offset of the start of week relative to Monday (Tuesday = +1, ...)
            var weekDayOffset = NodaTime.Period.FromDays((int)(rule.WeekStart ?? IsoDayOfWeek.Monday) - (int)IsoDayOfWeek.Monday);

            IEnumerable<IRuleIncrementor> components = [new RuleIncrementor(
                t => FindCurrentOrNextInterval(rule.DtStart, t, rule.Frequency.ToNodaPeriodUnits(), rule.Interval, (rule.Frequency == RuleFrequency.Weekly) ? weekDayOffset : null))];

            PeriodUnits GetByDayOuterUnit()
                => rule.Frequency switch
                {
                    RuleFrequency.Monthly => PeriodUnits.Months,
                    RuleFrequency.Yearly when (rule.ByMonth != null) => PeriodUnits.Months,
                    RuleFrequency.Yearly => PeriodUnits.Years,
                    _ => PeriodUnits.None
                };

            RuleIncrementor BuildByComponent(ByPart byPart, IReadOnlySet<int> byValues)
            {
                var dsr = RecurrenceExpandRules.ByPartDescriptors[byPart];

                Func<LocalDateTime, LocalDateTimeAndPeriod> f = byPart switch
                {
                    ByPart.ByWeekNo => t => RuleEnumerationUtils.FindCurrentOrNextByWeekNo(t, byValues.ToArray(), rule.WeekStart ?? IsoDayOfWeek.Monday),
                    ByPart.ByDay => t => RuleEnumerationUtils.FindCurrentOrNextByDay(t, GetByDayOuterUnit(), rule.ByDay!.ToArray()),
                    _ => t => RuleEnumerationUtils.FindCurrentOrNextBy(t, dsr.OuterUnit, dsr.InnerUnit, byValues.ToArray(), dsr.SupportNegative)
                };

                return new RuleIncrementor(f);
            }

            var byRules = rule.GetByRules();
            components = components.Concat(byRules
                .Where(byRule => byRule.Key != ByPart.BySetPos)
                // time-related BY parts MUST be ignored, if DTSTART is of type DATE (i.e. date-only)
                .Where(byRule => rule.hasTime || !RecurrenceExpandRules.ByPartDescriptors[byRule.Key].IsTime)
                .Select(byRule => BuildByComponent(byRule.Key, byRule.Value)));

            RuleIncrementor BuildFallbackExpandByComponent(ByPart byPart)
            {
                var dsr = RecurrenceExpandRules.ByPartDescriptors[byPart];
                switch (byPart)
                {
                    case ByPart.ByDay:
                        return new RuleIncrementor(t => RuleEnumerationUtils.FindCurrentOrNextByDay(t, PeriodUnits.None, [(rule.DtStart.DayOfWeek, null)]));

                    default:
                        return new RuleIncrementor(t => RuleEnumerationUtils.FindCurrentOrNextBy(t, dsr.OuterUnit, dsr.InnerUnit, [GetUnitFromLocalDateTime(rule.DtStart, dsr.InnerUnit)]));
                }
            }

            var missingByPartsWithExpansion = RecurrenceExpandRules.FallbackRules
                .Where(x => !byRules.ContainsKey(x.Key))
                .Where(x => RecurrenceExpandRules.FallbackRules.TryGetValue(x.Key, out var predicate) && predicate(rule))
                .Select(x => x.Key);

            components = components.Concat(missingByPartsWithExpansion.Select(BuildFallbackExpandByComponent));

            var enumFactory = Enumerate(components.ToArray());

            if (rule.BySetPos != null)
                enumFactory = EnumerateWithSetPos(rule.Frequency.ToNodaPeriodUnits(), enumFactory, rule.BySetPos.ToArray());

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
