using NodaTime;

namespace AltRecur
{
    public interface IRuleEnumerator
    {
        LocalTime Current { get; set; }

        bool MoveNext();

        void SkipForward(LocalTime to);
    }
}
