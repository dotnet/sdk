// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class ProcessValueFormMacroConfig : BaseMacroConfig<ProcessValueFormMacro, ProcessValueFormMacroConfig>, IMacroConfigDependency
    {
        private static readonly ProcessValueFormMacro DefaultMacro = new();

        internal ProcessValueFormMacroConfig(string sourceSymbol, string symbolName, string? dataType, string valueForm, IReadOnlyDictionary<string, IValueForm> forms)
             : base(DefaultMacro, symbolName, dataType)
        {
            if (string.IsNullOrWhiteSpace(sourceSymbol))
            {
                throw new ArgumentException($"'{nameof(sourceSymbol)}' cannot be null or whitespace.", nameof(sourceSymbol));
            }

            if (string.IsNullOrWhiteSpace(valueForm))
            {
                throw new ArgumentException($"'{nameof(valueForm)}' cannot be null or whitespace.", nameof(valueForm));
            }

            SourceVariable = sourceSymbol;
            Forms = forms;
            Form = forms.TryGetValue(valueForm, out IValueForm? form)
                ? form
                : throw new InvalidOperationException($"''{nameof(symbolName)}': form '{nameof(valueForm)}' cannot be found.");
        }

        internal string SourceVariable { get; }

        internal IReadOnlyDictionary<string, IValueForm> Forms { get; }

        internal IValueForm Form { get; }

        public void ResolveSymbolDependencies(IReadOnlyList<string> symbols)
        {
            MacroDependenciesResolved = true;
            PopulateMacroConfigDependencies(SourceVariable, symbols);
        }
    }
}
