using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class RegexMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string SourceVariable { get; private set; }

        // Regex -> Replacement
        public IList<KeyValuePair<string, string>> Steps { get; private set; }

        public RegexMacroConfig(string variableName, string sourceVariable, IList<KeyValuePair<string, string>> steps)
        {
            VariableName = variableName;
            Type = "regex";
            SourceVariable = sourceVariable;
            Steps = steps;
        }
    }
}
