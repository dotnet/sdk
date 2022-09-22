// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ValueForms;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class ProcessValueFormMacro : IMacro
    {
        public string Type => "processValueForm";

        public Guid Id => new Guid("642E0443-F82B-4A4B-A797-CC1EB42221AE");

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config)
        {
            if (config is not ProcessValueFormMacroConfig realConfig)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a ProcessValueFormMacroConfig");
            }

            string? value = string.Empty;
            if (vars.TryGetValue(realConfig.SourceVariable, out object working))
            {
                value = working.ToString() ?? string.Empty;
            }

            if (realConfig.Forms.TryGetValue(realConfig.FormName, out IValueForm? form))
            {
                value = form.Process(value, realConfig.Forms);
                if (value != null)
                {
                    vars[config.VariableName] = value;
                }
            }
            else
            {
                environmentSettings.Host.Logger.LogDebug($"Unable to find a form called '{realConfig.FormName}'");
            }
        }
    }
}
