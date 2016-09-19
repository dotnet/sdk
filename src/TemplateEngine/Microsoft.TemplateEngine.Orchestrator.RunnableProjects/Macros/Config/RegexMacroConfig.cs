using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class RegexMacroConfig : IMacroConfig
    {
        public Guid Id => new Guid("DA9917FA-6011-4447-A0E8-58CF630DA3A6");

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

        public IMacroConfig ConfigFromDeferredConfig(IMacroConfig rawConfig)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            string action;
            if (!deferredConfig.Parameters.TryGetValue("action", out action))
            {
                throw new ArgumentNullException("action");
            }

            string sourceVariable;
            if (!deferredConfig.Parameters.TryGetValue("source", out sourceVariable))
            {
                throw new ArgumentNullException("source");
            }

            string stepListString;
            List<KeyValuePair<string, string>> replacementSteps = new List<KeyValuePair<string, string>>();
            if (deferredConfig.Parameters.TryGetValue("steps", out stepListString))
            {
                JArray stepList = JArray.Parse(stepListString);

                foreach (JToken step in stepList)
                {
                    JObject map = (JObject)step;
                    string regex = map.ToString("regex");
                    string replaceWith = map.ToString("replacement");
                    replacementSteps.Add(new KeyValuePair<string, string>(regex, replaceWith));
                }
            }

            return new RegexMacroConfig(deferredConfig.VariableName, action, sourceVariable, replacementSteps);
        }
    }
}
