using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class NowMacroConfig : IMacroConfig
    {
        internal string DataType { get; }

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        internal string Format { get; private set; }

        internal bool Utc { get; private set; }

        internal NowMacroConfig(string variableName, string dataType, string format, bool utc)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "now";
            Format = format;
            Utc = utc;
        }
    }
}
