// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class GeneratedSymbolDeferredMacroConfig : IMacroConfig
    {
        internal GeneratedSymbolDeferredMacroConfig(string type, string dataType, string variableName, Dictionary<string, JToken> parameters)
        {
            DataType = dataType;
            Type = type;
            VariableName = variableName;
            Parameters = parameters;
        }

        public string VariableName { get; private set; }

        // comes from GeneratedSymbol.Generator
        // note that for all generated symbols, GeneratedSymbol.Type = "generated"
        public string Type { get; private set; }

        internal Guid Id => new Guid("12CA34F3-A1B7-4859-B08C-172483C9B0FD");

        internal string DataType { get; }

        internal IReadOnlyDictionary<string, JToken> Parameters { get; private set; }
    }
}
