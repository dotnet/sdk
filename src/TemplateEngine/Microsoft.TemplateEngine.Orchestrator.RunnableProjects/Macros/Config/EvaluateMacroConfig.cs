// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class EvaluateMacroConfig : IMacroConfig
    {
        internal EvaluateMacroConfig(string variableName, string dataType, string value, string evaluator)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "evaluate";
            Value = value;
            Evaluator = evaluator;
        }

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        internal string DataType { get; }

        internal string Value { get; private set; }

        internal string Evaluator { get; set; }
    }
}
