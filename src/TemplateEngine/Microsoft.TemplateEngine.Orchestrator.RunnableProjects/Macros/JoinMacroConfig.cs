// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class JoinMacroConfig : BaseMacroConfig<JoinMacro, JoinMacroConfig>
    {
        internal JoinMacroConfig(JoinMacro macro, string variableName, string? dataType, IReadOnlyList<(JoinType, string)> symbols, string separator, bool removeEmptyValues)
             : base(macro, variableName, dataType)
        {
            Symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
            Separator = separator;
            RemoveEmptyValues = removeEmptyValues;
        }

        internal JoinMacroConfig(JoinMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
        : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            Separator = GetMandatoryParameterValue(generatedSymbolConfig, nameof(Separator));
            RemoveEmptyValues = GetOptionalParameterValue(generatedSymbolConfig, nameof(RemoveEmptyValues), ConvertJTokenToBool);

            List<(JoinType Type, string Value)> symbolsList = new();

            JArray jArray = GetMandatoryParameterArray(generatedSymbolConfig, nameof(Symbols));

            foreach (JToken entry in jArray)
            {
                if (entry is not JObject jobj)
                {
                    throw new TemplateAuthoringException($"Generated symbol '{generatedSymbolConfig.VariableName}': array '{nameof(Symbols)}' should contain JSON objects.", generatedSymbolConfig.VariableName);
                }
                JoinType type = jobj.ToEnum("type", JoinType.Const, ignoreCase: true);
                string? value = jobj.ToString("value");
                if (value == null)
                {
                    throw new TemplateAuthoringException($"Generated symbol '{generatedSymbolConfig.VariableName}': array '{nameof(Symbols)}' should contain JSON objects with property 'value'.", generatedSymbolConfig.VariableName);
                }
                if (type == JoinType.Ref && string.IsNullOrWhiteSpace(value))
                {
                    throw new TemplateAuthoringException($"Generated symbol '{generatedSymbolConfig.VariableName}': array '{nameof(Symbols)}' should contain JSON objects with property non-empty 'value' when 'type' is 'ref'.", generatedSymbolConfig.VariableName);
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
    }
}
