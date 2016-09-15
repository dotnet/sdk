using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class ReplacementTokens : IReplacementTokens
    {
        public string Identity { get; private set; }

        public string OriginalValue { get; private set; }

        public ReplacementTokens(string identity, string originalValue)
        {
            Identity = identity;
            OriginalValue = originalValue;
        }
    }
}
