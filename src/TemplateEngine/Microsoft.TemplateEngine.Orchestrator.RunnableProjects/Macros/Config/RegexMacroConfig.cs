using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class RegexMacroConfig : IMacroConfig
    {
        internal string DataType { get; }

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        internal string SourceVariable { get; private set; }

        // Regex -> Replacement
        internal IList<KeyValuePair<string, string>> Steps { get; private set; }

        internal RegexMacroConfig(string variableName, string dataType, string sourceVariable, IList<KeyValuePair<string, string>> steps)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "regex";
            SourceVariable = sourceVariable;
            Steps = steps;
        }
    }
}
