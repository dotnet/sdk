using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core
{
    public class ConditionalTokens
    {
        public ConditionalTokens()
        {
            IfTokens = Empty<string>.List.Value;
            ElseTokens = Empty<string>.List.Value;
            ElseIfTokens = Empty<string>.List.Value;
            EndIfTokens = Empty<string>.List.Value;
            ActionableIfTokens = Empty<string>.List.Value;
            ActionableElseTokens = Empty<string>.List.Value;
            ActionableElseIfTokens = Empty<string>.List.Value;
            ActionableOperations = Empty<string>.List.Value;
        }

        public IReadOnlyList<string> IfTokens { get; set; }

        public IReadOnlyList<string> ElseTokens { get; set; }

        public IReadOnlyList<string> ElseIfTokens { get; set; }

        public IReadOnlyList<string> EndIfTokens { get; set; }

        public IReadOnlyList<string> ActionableIfTokens { get; set; }

        public IReadOnlyList<string> ActionableElseTokens { get; set; }

        public IReadOnlyList<string> ActionableElseIfTokens { get; set; }

        public IReadOnlyList<string> ActionableOperations { get; set; }
    }
}
