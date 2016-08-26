using System.Collections.Generic;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core
{
    public class ConditionalTokens
    {
        public ConditionalTokens()
        {
            IfTokens = No<string>.List.Value;
            ElseTokens = No<string>.List.Value;
            ElseIfTokens = No<string>.List.Value;
            EndIfTokens = No<string>.List.Value;
            ActionableIfTokens = No<string>.List.Value;
            ActionableElseTokens = No<string>.List.Value;
            ActionableElseIfTokens = No<string>.List.Value;
            ActionableOperations = No<string>.List.Value;
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
