// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class CoalesceMacroConfig : IMacroConfig
    {
        internal CoalesceMacroConfig(string variableName, string dataType, string sourceVariableName, string defaultValue, string fallbackVariableName)
        {
            DataType = dataType;
            VariableName = variableName;
            SourceVariableName = sourceVariableName;
            DefaultValue = defaultValue;
            FallbackVariableName = fallbackVariableName;
        }

        public string VariableName { get; }

        public string Type => "coalesce";

        internal string DataType { get; }

        internal string SourceVariableName { get; }

        internal string DefaultValue { get; }

        internal string FallbackVariableName { get; }
    }
}
