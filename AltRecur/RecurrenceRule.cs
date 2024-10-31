using NodaTime;

namespace AltRecur
{
    public enum RuleFrequency
    {
        Undefined,
        Secondly,
        Minutely,
        Hourly,
        Daily,
        Weekly,
        Monthly,
        Yearly,
    }

    public enum ByPart
    {
        None,
        ByMonth,
        ByWeekNo,
        ByYearDay,
        ByMonthDay,
        ByDay,
        ByHour,
        ByMinute,
        BySecond,
        BySetPos,
    }

    public record RecurrenceRule(
        LocalDateTime DtStart,
        bool hasTime,
        RuleFrequency Frequency,
        int Interval,
        IReadOnlySet<int>? ByMonth,
        IReadOnlySet<int>? ByWeekNo,
        IReadOnlySet<int>? ByYearDay,
        IReadOnlySet<int>? ByMonthDay,
        IReadOnlySet<(IsoDayOfWeek dow, int? ord)>? ByDay,
        IReadOnlySet<int>? ByHour,
        IReadOnlySet<int>? ByMinute,
        IReadOnlySet<int>? BySecond,
        IReadOnlySet<int>? BySetPos,
        int? Count,
        LocalDateTime? Until,
        IsoDayOfWeek? WeekStart = null)
    { }
}
