// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    internal class NowMacroConfig : IMacroConfig
    {
        internal NowMacroConfig(string variableName, string format, bool utc)
        {
            VariableName = variableName;
            Type = "now";
            Format = format;
            Utc = utc;
        }

        public string VariableName { get; private set; }

        public string Type { get; private set; }
        
        internal string DataType => "string";
        
        internal string Format { get; private set; }

        internal bool Utc { get; private set; }
    }
}
