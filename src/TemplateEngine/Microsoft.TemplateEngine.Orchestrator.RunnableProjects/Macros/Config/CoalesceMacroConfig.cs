using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class CoalesceMacroConfig : IMacroConfig
    {
        internal string DataType { get; }

        public string VariableName { get; }

        internal string SourceVariableName { get; }

        internal string DefaultValue { get; }

        internal string FallbackVariableName { get; }

        public string Type => "coalesce";

        internal CoalesceMacroConfig(string variableName, string dataType, string sourceVariableName, string defaultValue, string fallbackVariableName)
        {
            DataType = dataType;
            VariableName = variableName;
            SourceVariableName = sourceVariableName;
            DefaultValue = defaultValue;
            FallbackVariableName = fallbackVariableName;
        }
    }
}
