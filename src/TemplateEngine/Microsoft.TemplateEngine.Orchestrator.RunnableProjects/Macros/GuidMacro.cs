// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GuidMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("10919008-4E13-4FA8-825C-3B4DA855578E");

        public string Type => "guid";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig)
        {
            if (rawConfig is not GuidMacroConfig config)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as GuidMacroConfig");
            }

            string guidFormats = !string.IsNullOrEmpty(config.Format) ? config.Format! : GuidMacroConfig.DefaultFormats;
            Guid g = Guid.NewGuid();

            for (int i = 0; i < guidFormats.Length; ++i)
            {
                bool isUpperCase = char.IsUpper(guidFormats[i]);
                string value = g.ToString(guidFormats[i].ToString());
                value = isUpperCase ? value.ToUpperInvariant() : value.ToLowerInvariant();
                // Not breaking any dependencies on exact param names and on the
                //  case insensitive matching of parameters (https://github.com/dotnet/templating/blob/7e14ef44/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/RunnableProjectGenerator.cs#L726)
                //  we need to introduce new parameters - with distinc naming for upper- and lower- casing replacements
                string legacyName = config.VariableName + "-" + guidFormats[i];
                string newName = config.VariableName +
                        (isUpperCase ? GuidMacroConfig.UpperCaseDenominator : GuidMacroConfig.LowerCaseDenominator) +
                        guidFormats[i];

                vars[legacyName] = value;
                vars[newName] = value;
            }

            var defaultFormat = string.IsNullOrEmpty(config.DefaultFormat) ? "D" : config.DefaultFormat!;
            string defaultValue = char.IsUpper(defaultFormat[0]) ?
                g.ToString(config.DefaultFormat).ToUpperInvariant() :
                g.ToString(config.DefaultFormat).ToLowerInvariant();
            vars[config.VariableName] = defaultValue;
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            if (rawConfig is not GeneratedSymbolDeferredMacroConfig deferredConfig)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            deferredConfig.Parameters.TryGetValue("format", out JToken? formatToken);
            string? format = formatToken?.ToString();

            deferredConfig.Parameters.TryGetValue("defaultFormat", out JToken defaultFormatToken);
            string? defaultFormat = defaultFormatToken?.ToString();

            IMacroConfig realConfig = new GuidMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, format, defaultFormat);
            return realConfig;
        }
    }
}
