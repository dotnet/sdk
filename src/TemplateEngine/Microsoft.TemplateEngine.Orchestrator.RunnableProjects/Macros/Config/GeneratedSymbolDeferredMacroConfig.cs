using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class GeneratedSymbolDeferredMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        // comes from GeneratedSymbol.Generator
        // note that GeneratedSymbol.Type = "generated" is always the case
        public string Type { get; private set;  }

        public string Action
        {
            get
            {
                return string.Empty;
            }
        }

        public IReadOnlyDictionary<string, string> Parameters { get; private set; }


        public GeneratedSymbolDeferredMacroConfig(string type, string variableName, Dictionary<string, string> parameters)
        {
            Type = type;
            VariableName = variableName;
            Parameters = parameters;
        }

        public GeneratedSymbolDeferredMacroConfig(string variableName, GeneratedSymbol symbol)
        {
            VariableName = variableName;
            Type = symbol.Generator;    // symbol.Type == "generated" always. Generator is a string that refers to the actual macro type.
            Parameters = symbol.Parameters;

        }
    }
}
