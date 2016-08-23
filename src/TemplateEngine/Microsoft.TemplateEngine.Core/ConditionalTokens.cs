using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Core
{
    public class ConditionalTokens
    {
        private static readonly IReadOnlyList<string> _NoTokens = new string[0];

        public ConditionalTokens()
        {
            IfTokens = _NoTokens;
            ElseTokens = _NoTokens;
            ElseIfTokens = _NoTokens;
            EndIfTokens = _NoTokens;
            ActionableIfTokens = _NoTokens;
            ActionableElseTokens = _NoTokens;
            ActionableElseIfTokens = _NoTokens;
            ActionableOperations = _NoTokens;
        }

        public static IReadOnlyList<string> NoTokens
        {
            get
            {
                return _NoTokens;
            }
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
