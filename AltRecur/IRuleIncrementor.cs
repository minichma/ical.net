using NodaTime;

namespace AltRecur
{
    public interface IRuleIncrementor
    {
        LocalDateTimeAndPeriod CurrentOrNext(LocalDateTime t);
    }
}
