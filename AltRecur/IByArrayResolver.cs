using NodaTime;

namespace AltRecur
{
    internal interface IByArrayResolver
    {
        int[] GetResolvedByArray(LocalDateTime t);
    }
}
