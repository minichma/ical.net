using NodaTime;

namespace AltRecur
{
    public class RuleIncrementor(Func<LocalDateTime, LocalDateTimeAndPeriod> f) : IRuleIncrementor
    {
        public LocalDateTimeAndPeriod CurrentOrNext(LocalDateTime t)
            => f(t);
    }
}
