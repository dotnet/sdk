// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class GuidMacroConfig : IMacroConfig
    {
        internal string DataType { get; }

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        internal string DefaultFormat { get; private set; }

        internal string Format { get; private set; }

        internal static readonly string DefaultFormats = "ndbpxNDPBX";

        internal GuidMacroConfig(string variableName, string dataType, string format, string defaultFormat)
        {
            DataType = dataType;
            VariableName = variableName;
            Type = "guid";
            Format = format;
            DefaultFormat = string.IsNullOrWhiteSpace(defaultFormat) ? "D" : defaultFormat;
        }
    }
}
