// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class RegexMacroConfig : IMacroConfig
    {
        internal RegexMacroConfig(string variableName, string dataType, string sourceVariable, IList<KeyValuePair<string, string>> steps)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "regex";
            SourceVariable = sourceVariable;
            Steps = steps;
        }

        public string VariableName { get; private set; }
        public string Type { get; private set; }
        internal string DataType { get; }
        internal string SourceVariable { get; private set; }

        // Regex -> Replacement
        internal IList<KeyValuePair<string, string>> Steps { get; private set; }
    }
}
