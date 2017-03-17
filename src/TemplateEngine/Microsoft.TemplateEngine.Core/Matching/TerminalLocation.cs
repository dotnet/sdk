namespace Microsoft.TemplateEngine.Core.Matching
{
    public class TerminalLocation<T>
        where T : TerminalBase
    {
        public int Location;

        public readonly T Terminal;

        public TerminalLocation(T terminal, int location)
        {
            Terminal = terminal;
            Location = location;
        }
    }
}