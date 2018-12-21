using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class JoinMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string DataType { get; private set; }

        // type -> value
        public IList<KeyValuePair<string,string>> Symbols { get; private set; }

        public string Separator { get; private set; }

        public JoinMacroConfig(string variableName, string dataType, IList<KeyValuePair<string,string>> symbols, string separator)
        {
            VariableName = variableName;
            Type = "join";
            DataType = dataType;
            Symbols = symbols;
            Separator = separator;
        }
    }
}
