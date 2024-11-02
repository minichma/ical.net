using NodaTime;

namespace AltRecur
{
    public class RuleIncrementor(Func<LocalDateTime, LocalDateTimePeriod> f) : IRuleIncrementor
    {
        public LocalDateTimePeriod CurrentOrNext(LocalDateTime t)
            => f(t);
    }
}
