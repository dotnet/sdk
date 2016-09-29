using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class SwitchMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        // getting deprecated - don't use!
        public string Action { get; private set; }

        public string Evaluator { get; set; }

        // condition -> value
        public IList<KeyValuePair<string, string>> Switches { get; private set; }

        public SwitchMacroConfig(string variableName, string evaluator, IList<KeyValuePair<string, string>> switches)
        {
            VariableName = variableName;
            Type = "switch";
            Evaluator = evaluator;
            Switches = switches;
        }
    }
}
