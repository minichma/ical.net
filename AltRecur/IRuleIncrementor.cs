using NodaTime;

namespace AltRecur
{
    public interface IRuleIncrementor
    {
        LocalDateTimePeriod CurrentOrNext(LocalDateTime t);
    }
}
