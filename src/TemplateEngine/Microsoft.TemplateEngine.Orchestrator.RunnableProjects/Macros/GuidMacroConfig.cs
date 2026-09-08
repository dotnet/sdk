// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    //needs better validation if formats are correct
    internal class GuidMacroConfig : BaseMacroConfig<GuidMacro, GuidMacroConfig>
    {
        internal const string DefaultFormats = "ndbpxNDPBX";
        internal const string UpperCaseDenominator = "-uc-";
        internal const string LowerCaseDenominator = "-lc-";
        private static readonly GuidMacro DefaultMacro = new();

        internal GuidMacroConfig(string variableName, string? dataType, string? format, string? defaultFormat)
             : base(DefaultMacro, variableName, dataType)
        {
            if (!string.IsNullOrEmpty(format))
            {
                Format = format!;
            }
            if (!string.IsNullOrEmpty(defaultFormat))
            {
                DefaultFormat = defaultFormat!;
            }
        }

        internal GuidMacroConfig(GuidMacro macro, IGeneratedSymbolConfig generatedSymbolConfig)
        : base(macro, generatedSymbolConfig.VariableName, generatedSymbolConfig.DataType)
        {
            string? configuredFormat = GetOptionalParameterValue(generatedSymbolConfig, "format");
            if (!string.IsNullOrEmpty(configuredFormat))
            {
                Format = configuredFormat!;
            }
            string? configuredDefaultFormat = GetOptionalParameterValue(generatedSymbolConfig, "defaultFormat");
            if (!string.IsNullOrEmpty(configuredDefaultFormat))
            {
                DefaultFormat = configuredDefaultFormat!;
            }
        }

        internal string DefaultFormat { get; } = "D";

        internal string Format { get; } = DefaultFormats;
    }
}
