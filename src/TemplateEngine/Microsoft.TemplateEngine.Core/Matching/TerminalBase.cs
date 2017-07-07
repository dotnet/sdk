namespace Microsoft.TemplateEngine.Core.Matching
{
    public abstract class TerminalBase
    {
        protected TerminalBase(int tokenLength, int start, int end)
        {
            Start = start;
            End = end != -1 ? end : (tokenLength - 1);
            Length = tokenLength;
        }

        public int Start { get; protected set; }

        public int End { get; protected set; }

        public int Length { get; }
    }
}
