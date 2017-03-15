using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class ReplacementTokens : IReplacementTokens
    {
        public string VariableName { get; }

        public ITokenConfig OriginalValue { get; }

        public ReplacementTokens(string identity, ITokenConfig originalValue)
        {
            VariableName = identity;
            OriginalValue = originalValue;
        }
    }
}
