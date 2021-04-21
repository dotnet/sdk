// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class ProcessValueFormMacroConfig : IMacroConfig
    {
        internal ProcessValueFormMacroConfig(string sourceSymbol, string symbolName, string dataType, string valueForm, IReadOnlyDictionary<string, IValueForm> forms)
        {
            DataType = dataType;
            SourceVariable = sourceSymbol;
            VariableName = symbolName;
            FormName = valueForm;
            Forms = forms;
        }

        public string VariableName { get; }
        public string Type => "processValueForm";
        internal string DataType { get; }
        internal string SourceVariable { get; }

        internal string FormName { get; }

        internal IReadOnlyDictionary<string, IValueForm> Forms { get; }
    }
}
