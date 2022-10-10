// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class NowMacroConfig : BaseMacroConfig<NowMacro, NowMacroConfig>
    {
        internal NowMacroConfig(NowMacro macro, string variableName, string? format = null, bool utc = false)
             : base(macro, variableName, "string")
        {
            Format = format;
            Utc = utc;
        }

        internal NowMacroConfig(NowMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
        : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            Format = GetOptionalParameterValue(generatedSymbolConfig, "format");
            Utc = GetOptionalParameterValue(generatedSymbolConfig, "utc", ConvertJTokenToBool);
        }

        internal string? Format { get; private set; }

        internal bool Utc { get; private set; }
    }
}
