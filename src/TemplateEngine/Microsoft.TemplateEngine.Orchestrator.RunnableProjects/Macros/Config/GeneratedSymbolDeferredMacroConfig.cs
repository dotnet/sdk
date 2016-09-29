using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class GeneratedSymbolDeferredMacroConfig : IMacroConfig
    {
        public Guid Id => new Guid("12CA34F3-A1B7-4859-B08C-172483C9B0FD");

        public string VariableName { get; private set; }

        // comes from GeneratedSymbol.Generator
        // note that for all generated symbols, GeneratedSymbol.Type = "generated"
        public string Type { get; private set;  }

        public IReadOnlyDictionary<string, JToken> Parameters { get; private set; }

        public GeneratedSymbolDeferredMacroConfig(string type, string variableName, Dictionary<string, JToken> parameters)
        {
            Type = type;
            VariableName = variableName;
            Parameters = parameters;
        }
    }
}
