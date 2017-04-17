using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class ConditionalKeywords
    {
        private static readonly string DefaultPrefix = "#";
        private static readonly IReadOnlyList<string> DefaultIfKeywords = new[] { "if" };
        private static readonly IReadOnlyList<string> DefaultElseIfKeywords = new[] { "elseif", "elif" };
        private static readonly IReadOnlyList<string> DefaultElseKeywords = new[] { "else" };
        private static readonly IReadOnlyList<string> DefaultEndIfKeywords = new[] { "endif" };

        public ConditionalKeywords()
        {
            KeywordPrefix = DefaultPrefix;
            IfKeywords = DefaultIfKeywords;
            ElseIfKeywords = DefaultElseIfKeywords;
            ElseKeywords = DefaultElseKeywords;
            EndIfKeywords = DefaultEndIfKeywords;
        }

        public string KeywordPrefix { get; set; }

        public IReadOnlyList<string> IfKeywords { get; set; }

        public IReadOnlyList<string> ElseIfKeywords { get; set; }

        public IReadOnlyList<string> ElseKeywords { get; set; }

        public IReadOnlyList<string> EndIfKeywords { get; set; }

        public static ConditionalKeywords FromJObject(JObject rawConfiguration)
        {
            ConditionalKeywords keywords = new ConditionalKeywords();
            string ifKeyword = rawConfiguration.ToString("ifKeyword");
            if (!string.IsNullOrWhiteSpace(ifKeyword))
            {
                keywords.IfKeywords = new[] { ifKeyword };
            }

            string elseIfKeyword = rawConfiguration.ToString("elseIfKeyword");
            if (!string.IsNullOrWhiteSpace(elseIfKeyword))
            {
                keywords.ElseIfKeywords = new[] { elseIfKeyword };
            }

            string elseKeyword = rawConfiguration.ToString("elseKeyword");
            if (!string.IsNullOrWhiteSpace(elseKeyword))
            {
                keywords.ElseKeywords = new[] { elseKeyword };
            }

            string endIfKeyword = rawConfiguration.ToString("endIfKeyword");
            if (!string.IsNullOrWhiteSpace(endIfKeyword))
            {
                keywords.EndIfKeywords = new[] { endIfKeyword };
            }

            return keywords;
        }
    }
}
