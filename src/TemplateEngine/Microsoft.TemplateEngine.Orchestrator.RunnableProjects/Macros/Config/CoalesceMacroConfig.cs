using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class CoalesceMacroConfig : IMacroConfig
    {
        public string DataType { get; }

        public string VariableName { get; }

        public string SourceVariableName { get; }

        public string DefaultValue { get; }

        public string FallbackVariableName { get; }

        public string Type => "coalesce";

        public CoalesceMacroConfig(string variableName, string dataType, string sourceVariableName, string defaultValue, string fallbackVariableName)
        {
            DataType = dataType;
            VariableName = variableName;
            SourceVariableName = sourceVariableName;
            DefaultValue = defaultValue;
            FallbackVariableName = fallbackVariableName;
        }
    }
}
