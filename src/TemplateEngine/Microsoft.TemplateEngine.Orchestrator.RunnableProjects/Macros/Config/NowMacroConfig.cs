using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class NowMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public bool Utc { get; private set; }

        public NowMacroConfig(string variableName, string action, bool utc)
        {
            VariableName = variableName;
            Type = "now";
            Action = action;
            Utc = utc;
        }

        public static NowMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");
            bool utc = config.ToBool("utc");

            return new NowMacroConfig(variableName, action, utc);
        }
    }
}
