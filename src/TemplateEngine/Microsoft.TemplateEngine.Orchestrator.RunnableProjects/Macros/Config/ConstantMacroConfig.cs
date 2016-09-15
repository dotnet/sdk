using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class ConstantMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public ConstantMacroConfig(string variableName, string action)
        {
            VariableName = variableName;
            Type = "constant";
            Action = action;
        }

        public static ConstantMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");

            return new ConstantMacroConfig(variableName, action);
        }
    }
}
