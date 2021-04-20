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

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            GuidMacroConfig config = rawConfig as GuidMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as GuidMacroConfig");
            }

            string guidFormats;
            if (!string.IsNullOrEmpty(config.Format))
            {
                guidFormats = config.Format;
            }
            else
            {
                guidFormats = GuidMacroConfig.DefaultFormats;
            }

            Guid g = Guid.NewGuid();

            for (int i = 0; i < guidFormats.Length; ++i)
            {
                string value = char.IsUpper(guidFormats[i]) ? g.ToString(guidFormats[i].ToString()).ToUpperInvariant() : g.ToString(guidFormats[i].ToString()).ToLowerInvariant();
                Parameter p = new Parameter
                {
                    IsVariable = true,
                    Name = config.VariableName + "-" + guidFormats[i],
                    DataType = config.DataType
                };

                vars[p.Name] = value;
                setter(p, value);
            }

            Parameter pd;

            if (parameters.TryGetParameterDefinition(config.VariableName, out ITemplateParameter existingParam))
            {
                // If there is an existing parameter with this name, it must be reused so it can be referenced by name
                // for other processing, for example: if the parameter had value forms defined for creating variants.
                // When the param already exists, use its definition, but set IsVariable = true for consistency.
                pd = (Parameter)existingParam;
                pd.IsVariable = true;
            }
            else
            {
                pd = new Parameter
                {
                    IsVariable = true,
                    Name = config.VariableName
                };
            }

            vars[config.VariableName] = g.ToString(config.DefaultFormat);
            setter(pd, g.ToString(config.DefaultFormat));
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            deferredConfig.Parameters.TryGetValue("format", out JToken formatToken);
            string format = formatToken?.ToString();

            deferredConfig.Parameters.TryGetValue("defaultFormat", out JToken defaultFormatToken);
            string defaultFormat = defaultFormatToken?.ToString();

            IMacroConfig realConfig = new GuidMacroConfig(deferredConfig.VariableName, deferredConfig.DataType, format, defaultFormat);
            return realConfig;
        }
    }
}
