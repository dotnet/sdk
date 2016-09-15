using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class GuidMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public string Format { get; private set; }

        public static readonly string DefaultFormats = "ndbpxNDPBX";

        public GuidMacroConfig(string variableName, string action, string format)
        {
            VariableName = variableName;
            Type = "guid";
            Action = action;
            Format = format;
        }

        public static GuidMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");
            string format = config.ToString("format");

            return new GuidMacroConfig(variableName, action, format);
        }
    }
}
