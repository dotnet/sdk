// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RandomMacroConfig : BaseMacroConfig<RandomMacro, RandomMacroConfig>
    {
        internal RandomMacroConfig(RandomMacro macro, string variableName, string? dataType, int low, int? high)
             : base(macro, variableName, dataType)
        {
            Low = low;
            High = high ?? int.MaxValue;
        }

        internal RandomMacroConfig(RandomMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
        : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            Low = GetMandatoryParameterValue(generatedSymbolConfig, "low", ConvertJTokenToInt);
            High = GetOptionalParameterValue(generatedSymbolConfig, "high", ConvertJTokenToInt, int.MaxValue);
        }

        internal int Low { get; private set; }

        internal int High { get; private set; }
    }
}
