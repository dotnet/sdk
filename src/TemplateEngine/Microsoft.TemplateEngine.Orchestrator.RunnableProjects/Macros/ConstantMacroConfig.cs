// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class ConstantMacroConfig : BaseMacroConfig<ConstantMacro, ConstantMacroConfig>
    {
        internal ConstantMacroConfig(ConstantMacro macro, string? dataType, string variableName, string value)
            : base(macro, variableName, dataType)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        internal ConstantMacroConfig(ConstantMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
            : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            Value = GetMandatoryParameterValue(generatedSymbolConfig, "value");
        }

        internal string Value { get; private set; }
    }
}
