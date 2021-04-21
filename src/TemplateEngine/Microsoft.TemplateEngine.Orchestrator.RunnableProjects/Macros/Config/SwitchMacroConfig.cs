// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class SwitchMacroConfig : IMacroConfig
    {
        internal SwitchMacroConfig(string variableName, string evaluator, string dataType, IList<KeyValuePair<string, string>> switches)
        {
            VariableName = variableName;
            Type = "switch";
            Evaluator = evaluator;
            DataType = dataType;
            Switches = switches;
        }

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        internal string Evaluator { get; set; }

        internal string DataType { get; set; }

        // condition -> value
        internal IList<KeyValuePair<string, string>> Switches { get; private set; }
    }
}
