using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class RegexMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public string SourceVariable { get; private set; }

        // Regex -> Replacement
        public IList<KeyValuePair<string, string>> Steps { get; private set; }

        public RegexMacroConfig(string variableName, string action, string sourceVariable, IList<KeyValuePair<string, string>> steps)
        {
            VariableName = variableName;
            Type = "regex";
            Action = action;
            SourceVariable = sourceVariable;
            Steps = steps;
        }

        public static RegexMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");
            string sourceVariable = config.ToString("source");
            JArray stepList = config.Get<JArray>("steps");

            List<KeyValuePair<string, string>> replacementSteps = new List<KeyValuePair<string, string>>();

            if (stepList != null)
            {
                foreach (JToken step in stepList)
                {
                    JObject map = (JObject)step;
                    string regex = map.ToString("regex");
                    string replaceWith = map.ToString("replacement");
                    replacementSteps.Add(new KeyValuePair<string, string>(regex, replaceWith));
                }
            }

            return new RegexMacroConfig(variableName, action, sourceVariable, replacementSteps);
        }
    }
}
