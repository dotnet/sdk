using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class RandomMacroConfig : IMacroConfig
    {
        internal string DataType { get; }

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        internal int Low { get; private set; }

        internal int High { get; private set; }

        internal RandomMacroConfig(string variableName, string dataType, int low, int? high)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "random";
            Low = low;
            High = high ?? int.MaxValue;
        }
    }
}
