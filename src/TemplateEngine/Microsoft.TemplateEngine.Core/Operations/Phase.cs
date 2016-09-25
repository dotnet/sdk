using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class Phase
    {
        public Phase(string match, IReadOnlyList<string> resetsWith)
            : this(match, null, resetsWith)
        {
        }

        public Phase(string match, string replacement, IReadOnlyList<string> resetsWith)
        {
            Match = match;
            Replacement = replacement;
            ResetsWith = resetsWith;
            Next = new List<Phase>();
        }

        public string Match { get; }

        public List<Phase> Next { get; }

        public string Replacement { get; }

        public IReadOnlyList<string> ResetsWith { get; }
    }
}