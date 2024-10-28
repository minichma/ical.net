using NodaTime;

namespace AltRecur
{
    public class SkipTokenSource
    {
        private class SkipToken(SkipTokenSource outer) : ISkipToken
        {
            public LocalDateTime SkipTo => outer._skipTo;
        }

        private LocalDateTime _skipTo;

        public ISkipToken Token { get; }

        public SkipTokenSource(LocalDateTime startValue)
        {
            this._skipTo = startValue;
            this.Token = new SkipToken(this);
        }

        public void SkipTo(LocalDateTime skipTo)
            => this._skipTo = skipTo;
    }
}
