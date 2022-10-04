// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal sealed class MacroProcessingException : ContentGenerationException
    {
        internal MacroProcessingException(IMacroConfig config) : base(string.Format(LocalizableStrings.MacroProcessingException_Message, config.VariableName, config.Type))
        {
        }

        internal MacroProcessingException(IMacroConfig config, Exception innerException) : base(string.Format(LocalizableStrings.MacroProcessingException_Message, config.VariableName, config.Type), innerException)
        {
        }
    }
}
