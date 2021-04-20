// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class JoinMacroConfig : IMacroConfig
    {
        public string VariableName { get; private set; }

        public string Type { get; private set; }

        internal string DataType { get; private set; }

        // type -> value
        internal IList<KeyValuePair<string, string>> Symbols { get; private set; }

        internal string Separator { get; private set; }

        internal JoinMacroConfig(string variableName, string dataType, IList<KeyValuePair<string, string>> symbols, string separator)
        {
            VariableName = variableName;
            Type = "join";
            DataType = dataType;
            Symbols = symbols;
            Separator = separator;
        }
    }
}
