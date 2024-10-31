using NodaTime;
using static AltRecur.RecurrenceExpandRules;

namespace AltRecur
{
    internal static class RecurrenceExpandRules
    {
        public enum RuleHandling
        {
            Undefined,
            Limit,
            Expand,
            Illegal,
            Special,
        }

        internal record ByPartDescriptor(
            bool SupportNegative,
            bool IsTime,
            PeriodUnits OuterUnit,
            PeriodUnits InnerUnit);

        internal static IReadOnlyDictionary<ByPart, ByPartDescriptor> ByPartDescriptors { get; } = new Dictionary<ByPart, ByPartDescriptor>()
        {
            { ByPart.ByMonth, new(false, false, PeriodUnits.Years, PeriodUnits.Months) },
            { ByPart.ByWeekNo, new(true, false, PeriodUnits.Years, PeriodUnits.Weeks) },
            { ByPart.ByYearDay, new(true, false, PeriodUnits.Years, PeriodUnits.Days) },
            { ByPart.ByMonthDay, new(true, false, PeriodUnits.Months, PeriodUnits.Days) },
            { ByPart.ByDay, new(true, false, PeriodUnits.Weeks, PeriodUnits.Days) },
            { ByPart.ByHour, new(false, true, PeriodUnits.Days, PeriodUnits.Hours) },
            { ByPart.ByMinute, new(false, true, PeriodUnits.Hours, PeriodUnits.Minutes) },
            { ByPart.BySecond, new(false, true, PeriodUnits.Minutes, PeriodUnits.Seconds) },
        };

        public static HashSet<RuleFrequency> TimeFrequencies { get; } = new()
        {
            RuleFrequency.Secondly,
            RuleFrequency.Minutely,
            RuleFrequency.Hourly,
        };

        public static Dictionary<ByPart, Func<RecurrenceRule, bool>> FallbackRules = new()
        {
            { ByPart.BySecond, rule => rule.hasTime && rule.Frequency > RuleFrequency.Secondly },
            { ByPart.ByMinute, rule => rule.hasTime && rule.Frequency > RuleFrequency.Minutely },
            { ByPart.ByHour, rule => rule.hasTime && rule.Frequency > RuleFrequency.Hourly },
            { ByPart.ByDay, rule => (rule.Frequency == RuleFrequency.Weekly) || (rule.ByWeekNo != null) },
            { ByPart.ByMonthDay, rule => (rule.Frequency >= RuleFrequency.Monthly) && (rule.ByWeekNo == null) && (rule.ByYearDay == null) && (rule.ByDay == null) },
            { ByPart.ByMonth, rule => (rule.Frequency == RuleFrequency.Yearly) && (rule.ByWeekNo == null) && (rule.ByYearDay == null) },
        };

        public static Dictionary<RuleFrequency, Dictionary<ByPart, RuleHandling>> ExpandRules = new()
        {
            { RuleFrequency.Secondly, new() {
                { ByPart.ByMonth, RuleHandling.Limit },
                { ByPart.ByWeekNo, RuleHandling.Illegal },
                { ByPart.ByYearDay, RuleHandling.Limit },
                { ByPart.ByMonthDay, RuleHandling.Limit },
                { ByPart.ByDay, RuleHandling.Limit },
                { ByPart.ByHour, RuleHandling.Limit },
                { ByPart.ByMinute, RuleHandling.Limit },
                { ByPart.BySecond, RuleHandling.Limit },
            } },
            { RuleFrequency.Minutely, new() {
                { ByPart.ByMonth, RuleHandling.Limit },
                { ByPart.ByWeekNo, RuleHandling.Illegal },
                { ByPart.ByYearDay, RuleHandling.Limit },
                { ByPart.ByMonthDay, RuleHandling.Limit },
                { ByPart.ByDay, RuleHandling.Limit },
                { ByPart.ByHour, RuleHandling.Limit },
                { ByPart.ByMinute, RuleHandling.Limit },
                { ByPart.BySecond, RuleHandling.Expand },
            } },
            { RuleFrequency.Hourly, new() {
                { ByPart.ByMonth, RuleHandling.Limit },
                { ByPart.ByWeekNo, RuleHandling.Illegal },
                { ByPart.ByYearDay, RuleHandling.Limit },
                { ByPart.ByMonthDay, RuleHandling.Limit },
                { ByPart.ByDay, RuleHandling.Limit },
                { ByPart.ByHour, RuleHandling.Limit },
                { ByPart.ByMinute, RuleHandling.Expand },
                { ByPart.BySecond, RuleHandling.Expand },
            } },
            { RuleFrequency.Daily, new() {
                { ByPart.ByMonth, RuleHandling.Limit },
                { ByPart.ByWeekNo, RuleHandling.Illegal },
                { ByPart.ByYearDay, RuleHandling.Illegal },
                { ByPart.ByMonthDay, RuleHandling.Limit },
                { ByPart.ByDay, RuleHandling.Limit },
                { ByPart.ByHour, RuleHandling.Expand },
                { ByPart.ByMinute, RuleHandling.Expand },
                { ByPart.BySecond, RuleHandling.Expand },
            } },
            { RuleFrequency.Weekly, new() {
                { ByPart.ByMonth, RuleHandling.Limit },
                { ByPart.ByWeekNo, RuleHandling.Illegal },
                { ByPart.ByYearDay, RuleHandling.Illegal },
                { ByPart.ByMonthDay, RuleHandling.Illegal },
                { ByPart.ByDay, RuleHandling.Expand },
                { ByPart.ByHour, RuleHandling.Expand },
                { ByPart.ByMinute, RuleHandling.Expand },
                { ByPart.BySecond, RuleHandling.Expand },
            } },
            { RuleFrequency.Monthly, new() {
                { ByPart.ByMonth, RuleHandling.Limit },
                { ByPart.ByWeekNo, RuleHandling.Illegal },
                { ByPart.ByYearDay, RuleHandling.Illegal },
                { ByPart.ByMonthDay, RuleHandling.Expand },
                { ByPart.ByDay, RuleHandling.Special },
                { ByPart.ByHour, RuleHandling.Expand },
                { ByPart.ByMinute, RuleHandling.Expand },
                { ByPart.BySecond, RuleHandling.Expand },
            } },
            { RuleFrequency.Yearly, new() {
                { ByPart.ByMonth, RuleHandling.Expand },
                { ByPart.ByWeekNo, RuleHandling.Expand },
                { ByPart.ByYearDay, RuleHandling.Expand },
                { ByPart.ByMonthDay, RuleHandling.Expand },
                { ByPart.ByDay, RuleHandling.Special },
                { ByPart.ByHour, RuleHandling.Expand },
                { ByPart.ByMinute, RuleHandling.Expand },
                { ByPart.BySecond, RuleHandling.Expand },
            } },
        };

        public static bool IsValid(this RecurrenceRule rule)
        {
            var byRules = rule.GetByRules();
            var expandRules = ExpandRules[rule.Frequency];

            if (!rule.hasTime && TimeFrequencies.Contains(rule.Frequency))
                return false;

            if (byRules.Any(x => (x.Value.Count == 0)))
                return false;

            if (rule.Interval < 1)
                return false;

            foreach (var expandRule in expandRules.Where(x => x.Value == RuleHandling.Illegal))
            {
                if (byRules.ContainsKey(expandRule.Key))
                    return false;
            }

            if (rule.ByDay != null)
            {
                //The BYDAY rule part MUST
                //NOT be specified with a numeric value when the FREQ rule part is
                //not set to MONTHLY or YEARLY.Furthermore, the BYDAY rule part
                //MUST NOT be specified with a numeric value with the FREQ rule part
                //set to YEARLY when the BYWEEKNO rule part is specified.
                var hasOrd = rule.ByDay.Any(x => x.ord != null);
                if (hasOrd)
                {
                    if ((rule.Frequency != RuleFrequency.Monthly) && (rule.Frequency != RuleFrequency.Yearly))
                        return false;
                    else if ((rule.Frequency == RuleFrequency.Yearly) && (rule.ByWeekNo != null))
                        return false;
                }
            }

            if (rule.Count < 0)
                return false;

            if ((rule.Count != null) && (rule.Until != null))
                return false;

            return true;
        }
    }
}
