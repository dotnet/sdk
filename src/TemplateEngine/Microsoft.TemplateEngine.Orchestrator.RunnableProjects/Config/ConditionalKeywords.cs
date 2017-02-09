using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class ConditionalKeywords
    {
        private static readonly string DefaultPrefix = "#";
        private static readonly string DefaultIfKeyword = "if";
        private static readonly string DefaultElseIfKeyword = "elseif";
        private static readonly string DefaultElseKeyword = "else";
        private static readonly string DefaultEndIfKeyword = "endif";

        public ConditionalKeywords()
        {
            KeywordPrefix = DefaultPrefix;
            IfKeyword = DefaultIfKeyword;
            ElseIfKeyword = DefaultElseIfKeyword;
            ElseKeyword = DefaultElseKeyword;
            EndIfKeyword = DefaultEndIfKeyword;
        }

        public string KeywordPrefix { get; set; }
        public string IfKeyword { get; set; }
        public string ElseIfKeyword { get; set; }
        public string ElseKeyword { get; set; }
        public string EndIfKeyword { get; set; }

        public static ConditionalKeywords FromJObject(JObject rawConfiguration)
        {
            ConditionalKeywords keywords = new ConditionalKeywords();
            string ifKeyword = rawConfiguration.ToString("ifKeyword");
            if (!string.IsNullOrWhiteSpace(ifKeyword))
            {
                keywords.IfKeyword = ifKeyword;
            }

            string elseIfKeyword = rawConfiguration.ToString("elseIfKeyword");
            if (!string.IsNullOrWhiteSpace(elseIfKeyword))
            {
                keywords.ElseIfKeyword = elseIfKeyword;
            }

            string elseKeyword = rawConfiguration.ToString("elseKeyword");
            if (!string.IsNullOrWhiteSpace(elseKeyword))
            {
                keywords.ElseKeyword = elseKeyword;
            }

            string endIfKeyword = rawConfiguration.ToString("endIfKeyword");
            if (!string.IsNullOrWhiteSpace(endIfKeyword))
            {
                keywords.EndIfKeyword = endIfKeyword;
            }

            return keywords;
        }
    }
}
