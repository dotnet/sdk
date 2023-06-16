// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class JoinMacroConfig : BaseMacroConfig<JoinMacro, JoinMacroConfig>, IMacroConfigDependency
    {
        private const string SymbolsPropertyName = "symbols";
        private const string SymbolsTypePropertyName = "type";
        private const string SymbolsValuePropertyName = "value";

        internal JoinMacroConfig(JoinMacro macro, string variableName, string? dataType, IReadOnlyList<(JoinType, string)> symbols, string separator = "", bool removeEmptyValues = false)
             : base(macro, variableName, dataType)
        {
            Symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
            Separator = separator;
            RemoveEmptyValues = removeEmptyValues;
        }

        internal JoinMacroConfig(JoinMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
        : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            Separator = GetOptionalParameterValue(generatedSymbolConfig, "separator") ?? string.Empty;
            RemoveEmptyValues = GetOptionalParameterValue(generatedSymbolConfig, "removeEmptyValues", ConvertJTokenToBool);

            List<(JoinType Type, string Value)> symbolsList = new();

            JArray jArray = GetMandatoryParameterArray(generatedSymbolConfig, SymbolsPropertyName);

            foreach (JToken entry in jArray)
            {
                if (entry is not JObject jobj)
                {
                    throw new TemplateAuthoringException(string.Format(LocalizableStrings.MacroConfig_Exception_ArrayShouldContainObjects, generatedSymbolConfig.VariableName, SymbolsPropertyName), generatedSymbolConfig.VariableName);
                }
                JoinType type = jobj.ToEnum(SymbolsTypePropertyName, JoinType.Const, ignoreCase: true);
                string? value = jobj.ToString(SymbolsValuePropertyName)
                    ?? throw new TemplateAuthoringException(
                        string.Format(
                            LocalizableStrings.MacroConfig_Exception_MissingValueProperty,
                            generatedSymbolConfig.VariableName,
                            SymbolsPropertyName,
                            SymbolsValuePropertyName),
                        generatedSymbolConfig.VariableName);
                if (type == JoinType.Ref && string.IsNullOrWhiteSpace(value))
                {
                    throw new TemplateAuthoringException(
                        string.Format(
                            LocalizableStrings.JoinMacroConfig_Exception_ValuePropertyIsEmpty,
                            generatedSymbolConfig.VariableName,
                            SymbolsPropertyName,
                            SymbolsValuePropertyName,
                            SymbolsTypePropertyName,
                            JoinType.Ref.ToString("g")),
                        generatedSymbolConfig.VariableName);
                }

                symbolsList.Add((type, value));
            }
            Symbols = symbolsList;

        }

        internal enum JoinType
        {
            Const,
            Ref
        }

        internal IReadOnlyList<(JoinType Type, string Value)> Symbols { get; private set; }

        internal string Separator { get; private set; }

        internal bool RemoveEmptyValues { get; private set; }

        public void ResolveSymbolDependencies(IReadOnlyList<string> symbols)
        {
            MacroDependenciesResolved = true;
            Symbols.ForEach(s => PopulateMacroConfigDependencies(s.Value, symbols));
        }
    }
}
