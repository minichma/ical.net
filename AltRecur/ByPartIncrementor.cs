using NodaTime;
using static AltRecur.RecurrenceExpandRules;

namespace AltRecur
{
    internal class ByPartIncrementor(
            IsoDayOfWeek startOfWeek,
            IByArrayResolver byArrayResolver,
            Func<LocalDateTime, IsoDayOfWeek, int> GetValue,
            Func<LocalDateTime, IsoDayOfWeek, LocalDateTime> FloorOuter,
            Func<LocalDateTime, IsoDayOfWeek, LocalDateTime> IncOuter,
            Func<LocalDateTime, int, LocalDateTime> IncInner) : IRuleIncrementor
    {
        public LocalDateTimePeriod CurrentOrNext(LocalDateTime t)
        {
            var outerStart = FloorOuter(t, startOfWeek);

            // Floor to period boundary (start of sec, min, hour)
            int next;
            int currentUnitVal;
            do
            {
                var preparedBy = byArrayResolver.GetResolvedByArray(t);

                currentUnitVal = GetValue(t, startOfWeek);
                next = preparedBy.Where(x => x >= currentUnitVal).FirstOrDefault(-1);

                if (next < 0)
                {
                    outerStart = IncOuter(FloorOuter(outerStart, startOfWeek), startOfWeek);
                    preparedBy = byArrayResolver.GetResolvedByArray(outerStart);
                }
            } while (next < 0);

            var res = outerStart;
            if (currentUnitVal != next)
                res = IncInner(res, next - currentUnitVal);

            return new(res, IncInner(res, 1));
        }

        public static Func<LocalDateTime, LocalDateTimePeriod> Build(int[] by, IsoDayOfWeek startOfWeek, ByPart byPart)
        {
            var dsr = ByPartDescriptors[byPart];
            var incrementor = new ByPartIncrementor(startOfWeek, dsr.BuildByArrayResolver(by, startOfWeek), dsr.GetValue, dsr.FloorOuter, dsr.IncOuter, dsr.IncInner);
            return incrementor.CurrentOrNext;
        }

        public static Func<LocalDateTime, LocalDateTimePeriod> BuildByDay((IsoDayOfWeek dow, int? ord)[] by, RuleFrequency outerFreq , IsoDayOfWeek startOfWeek)
        {
            var byWithOrd = by.Where(x => x.ord.HasValue).Select(x => (x.dow, ord: x.ord!.Value)).ToArray();
            var byWithoutOrd = by.Where(x => !x.ord.HasValue).Select(x => ((int)(x.dow + 7 - startOfWeek)) % 7).ToArray();
            var byDayDsr = ByPartDescriptors[ByPart.ByDay];

            ByPartIncrementor? i1 = byWithoutOrd.Any() ? new ByPartIncrementor(startOfWeek, ByArrayResolver.Static(byWithoutOrd), byDayDsr.GetValue, byDayDsr.FloorOuter, byDayDsr.IncOuter, byDayDsr.IncInner) : null;
            ByPartIncrementor? i2 = null;
            if (byWithOrd.Any()) {
                var byOuterDsr = outerFreq switch
                {
                    RuleFrequency.Yearly => ByPartDescriptors[ByPart.ByYearDay],
                    RuleFrequency.Monthly => ByPartDescriptors[ByPart.ByMonthDay],
                    _ => throw new ApplicationException()
                };

                i2 = new ByPartIncrementor(startOfWeek, ByArrayResolver.DynamicByDayWithOrdAsDayOfPeriod(byWithOrd, byOuterDsr.OuterUnit), byOuterDsr.GetValue, byOuterDsr.FloorOuter, byOuterDsr.IncOuter, byOuterDsr.IncInner);
            }

            switch (i1, i2)
            {
                case (null, null):
                    throw new ApplicationException();
                case (not null, not null):
                    LocalDateTimePeriod FindComposed(LocalDateTime t)
                    {
                        var res1 = i1.CurrentOrNext(t);
                        var res2 = i2.CurrentOrNext(t);
                        return (res1.Start <= res2.Start) ? res1 : res2;
                    }

                    return FindComposed;
                default:
                    return (i1 ?? i2)!.CurrentOrNext;
            }
        }
    }
}
