// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMatchMacro : IDeferredMacro
    {
        public Guid Id => new Guid("AA5957B0-07B1-4B68-847F-83713973E86F");

        public string Type => "regexMatch";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig)
        {
            string value;

            if (rawConfig is not RegexMatchMacroConfig config)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as RegexMatchMacroConfig");
            }

            value = !vars.TryGetValue(config.SourceVariable, out object working) ? string.Empty : working?.ToString() ?? string.Empty;

            bool result = false;

            try
            {
                result = Regex.IsMatch(value, config.Pattern);
            }
            catch (ArgumentException)
            {
                environmentSettings.Host.Logger.LogDebug(string.Format(LocalizableStrings.Authoring_InvalidRegex, config.Pattern));
            }
            vars[config.VariableName] = result;
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            if (rawConfig is not GeneratedSymbolDeferredMacroConfig deferredConfig)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            if (!deferredConfig.Parameters.TryGetValue("source", out JToken sourceVarToken))
            {
                throw new ArgumentNullException("source");
            }

            string sourceVariable = sourceVarToken.ToString();

            if (!deferredConfig.Parameters.TryGetValue("pattern", out JToken patternToken))
            {
                throw new ArgumentNullException("pattern");
            }

            string pattern = patternToken.ToString();

            //Warn the user if they explicitly specify something other than "bool" for DataType for this macro
            if (deferredConfig.DataType != null
                && !string.Equals(deferredConfig.DataType, "bool", StringComparison.OrdinalIgnoreCase))
            {
                environmentSettings.Host.Logger.LogDebug(LocalizableStrings.Authoring_NonBoolDataTypeForRegexMatch);
            }

            IMacroConfig realConfig = new RegexMatchMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, sourceVariable, pattern);
            return realConfig;
        }
    }
}
