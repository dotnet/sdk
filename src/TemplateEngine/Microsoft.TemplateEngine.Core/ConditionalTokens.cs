using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Core
{
    public class ConditionalTokens
    {
        public IList<string> IfTokens = new List<string>();
        public IList<string> ElseTokens = new List<string>();
        public IList<string> ElseIfTokens = new List<string>();
        public IList<string> EndIfTokens = new List<string>();
        public IList<string> ActionableIfTokens = new List<string>();
        public IList<string> ActionableElseTokens = new List<string>();
        public IList<string> ActionableElseIfTokens = new List<string>();
        public IList<string> ActionableOperations = new List<string>();
    }
}
