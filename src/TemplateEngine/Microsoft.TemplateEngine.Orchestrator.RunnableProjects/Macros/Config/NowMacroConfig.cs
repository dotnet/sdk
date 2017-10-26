using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class NowMacroConfig : IMacroConfig
    {
        public string DataType { get; }

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Format { get; private set; }

        public bool Utc { get; private set; }

        public NowMacroConfig(string variableName, string dataType, string format, bool utc)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "now";
            Format = format;
            Utc = utc;
        }
    }
}
