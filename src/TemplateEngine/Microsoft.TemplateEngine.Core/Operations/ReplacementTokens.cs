using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class ReplacementTokens : IReplacementTokens
    {
        public string VariableName { get; }

        public string OriginalValue { get; }

        public ReplacementTokens(string identity, string originalValue)
        {
            VariableName = identity;
            OriginalValue = originalValue;
        }
    }
}
