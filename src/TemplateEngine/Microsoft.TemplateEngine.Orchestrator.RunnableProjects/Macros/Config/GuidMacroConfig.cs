// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class GuidMacroConfig : IMacroConfig
    {
        internal const string DefaultFormats = "ndbpxNDPBX";

        internal GuidMacroConfig(string variableName, string dataType, string? format, string? defaultFormat)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "guid";
            Format = format;
            DefaultFormat = defaultFormat;
        }

        public string VariableName { get; }

        public string Type { get; }

        internal string DataType { get; }

        internal string? DefaultFormat { get; }

        internal string? Format { get; }
    }
}
