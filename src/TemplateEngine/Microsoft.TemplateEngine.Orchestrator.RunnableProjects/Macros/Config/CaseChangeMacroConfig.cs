// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class CaseChangeMacroConfig : IMacroConfig
    {
        internal string DataType { get; }

        public string Type => "casing";

        public string VariableName { get; }

        internal string SourceVariable { get; }

        internal bool ToLower { get; }

        internal CaseChangeMacroConfig(string variableName, string dataType, string sourceVariable, bool toLower)
        {
            DataType = dataType;
            VariableName = variableName;
            SourceVariable = sourceVariable;
            ToLower = toLower;
        }
    }
}
