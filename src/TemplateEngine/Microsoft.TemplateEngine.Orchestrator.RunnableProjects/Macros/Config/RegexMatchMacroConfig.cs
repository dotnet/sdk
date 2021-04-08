using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class RegexMatchMacroConfig : IMacroConfig
    {
        internal string DataType { get; }

        public string VariableName { get; }

        public string Type { get; }

        internal string SourceVariable { get; }

        internal string Pattern { get; }

        internal RegexMatchMacroConfig(string variableName, string dataType, string sourceVariable, string pattern)
        {
            DataType = dataType ?? "bool";
            VariableName = variableName;
            Type = "regexMatch";
            SourceVariable = sourceVariable;
            Pattern = pattern;
        }
    }
}
