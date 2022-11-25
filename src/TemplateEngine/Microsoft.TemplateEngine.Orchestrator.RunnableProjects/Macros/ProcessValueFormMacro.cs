// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class ProcessValueFormMacro : BaseMacro<ProcessValueFormMacroConfig>
    {
        internal const string MacroType = "processValueForm";

        public override string Type => MacroType;

        public override Guid Id { get; } = new Guid("642E0443-F82B-4A4B-A797-CC1EB42221AE");

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, ProcessValueFormMacroConfig config)
        {
            string? value = string.Empty;
            if (!vars.TryGetValue(config.SourceVariable, out object working))
            {
                environmentSettings.Host.Logger.LogDebug("[{macro}]: Source variable '{sourceVar}' was not found, skipping processing for macro '{var}'.", nameof(ProcessValueFormMacro), config.SourceVariable, config.VariableName);
                return;
            }
            if (working == null)
            {
                environmentSettings.Host.Logger.LogDebug("[{macro}]: The value of source variable '{sourceVar}' is null, skipping processing for macro '{var}'.", nameof(ProcessValueFormMacro), config.SourceVariable, config.VariableName);
                return;
            }

            string? result = config.Form.Process(working.ToString(), config.Forms);
            if (result == null)
            {
                environmentSettings.Host.Logger.LogDebug("[{macro}]: Processing form {formName} on {val} resulted in null.", nameof(JoinMacro), config.Forms, working);
                return;
            }
            vars[config.VariableName] = result;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(JoinMacro), config.VariableName, result);
        }
    }
}
