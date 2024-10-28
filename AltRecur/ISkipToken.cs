using NodaTime;

namespace AltRecur
{
    public interface ISkipToken
    {
        public LocalDateTime SkipTo { get; }
    }
}
