using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class RegexMatchMacroConfig : IMacroConfig
    {
        public string DataType { get; }

        public string VariableName { get; }

        public string Type { get; }

        public string SourceVariable { get; }

        public string Pattern { get; }

        public RegexMatchMacroConfig(string variableName, string dataType, string sourceVariable, string pattern)
        {
            DataType = dataType ?? "bool";
            VariableName = variableName;
            Type = "regexMatch";
            SourceVariable = sourceVariable;
            Pattern = pattern;
        }
    }
}
