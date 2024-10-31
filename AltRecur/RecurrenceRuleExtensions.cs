namespace AltRecur
{
    public static class RecurrenceRuleExtensions
    {
        internal static Dictionary<ByPart, IReadOnlySet<int>> GetByRules(this RecurrenceRule rule)
        {
            var byRules = new (ByPart by, IReadOnlySet<int>? values)[]
            {
                (ByPart.ByMonth, rule.ByMonth),
                (ByPart.ByWeekNo, rule.ByWeekNo),
                (ByPart.ByMonthDay, rule.ByMonthDay),
                (ByPart.ByYearDay, rule.ByYearDay),
                (ByPart.ByDay, rule.ByDay?.Select(x => ((int)x.dow) + (x.ord ?? 0) * 8).ToHashSet()),
                (ByPart.ByHour, rule.ByHour),
                (ByPart.ByMinute, rule.ByMinute),
                (ByPart.BySecond, rule.BySecond),
                (ByPart.BySetPos, rule.BySetPos),
            }
            .Where(x => x.values != null)
            .ToDictionary(x => x.by, x => x.values!);

            return byRules;
        }
    }
}
