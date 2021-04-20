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
    internal class GeneratePortNumberMacro : IDeferredMacro
    {
        public Guid Id => new Guid("D49B3690-B1E5-410F-A260-E1D7E873D8B2");

        public string Type => "port";

        private static readonly int LowPortDefault = 1024;
        private static readonly int HighPortDefault = 65535;

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            GeneratePortNumberConfig config = rawConfig as GeneratePortNumberConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as EvaluateMacroConfig");
            }

            config.Socket?.Dispose();

            Parameter p;

            if (parameters.TryGetParameterDefinition(config.VariableName, out ITemplateParameter existingParam))
            {
                // If there is an existing parameter with this name, it must be reused so it can be referenced by name
                // for other processing, for example: if the parameter had value forms defined for creating variants.
                // When the param already exists, use its definition, but set IsVariable = true for consistency.
                p = (Parameter)existingParam;
                p.IsVariable = true;

                if (string.IsNullOrEmpty(p.DataType))
                {
                    p.DataType = config.DataType;
                }
            }
            else
            {
                p = new Parameter
                {
                    IsVariable = true,
                    Name = config.VariableName,
                    DataType = config.DataType
                };
            }

            vars[config.VariableName] = config.Port.ToString();
            setter(p, config.Port.ToString());
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            int low;
            int high;

            if (!deferredConfig.Parameters.TryGetValue("low", out JToken lowToken) || lowToken.Type != JTokenType.Integer)
            {
                low = LowPortDefault;
            }
            else
            {
                low = lowToken.Value<int>();
                if (low < LowPortDefault)
                {
                    low = LowPortDefault;
                }
            }

            if (!deferredConfig.Parameters.TryGetValue("high", out JToken highToken) || highToken.Type != JTokenType.Integer)
            {
                high = HighPortDefault;
            }
            else
            {
                high = highToken.Value<int>();
                if (high > HighPortDefault)
                {
                    high = HighPortDefault;
                }
            }

            if (low > high)
            {
                low = LowPortDefault;
                high = HighPortDefault;
            }

            int fallback = 0;
            if(deferredConfig.Parameters.TryGetValue("fallback", out JToken fallbackToken) && fallbackToken.Type == JTokenType.Integer)
            {
                fallback = fallbackToken.ToInt32();
            }

            IMacroConfig realConfig = new GeneratePortNumberConfig(deferredConfig.VariableName, deferredConfig.DataType, fallback, low, high);
            return realConfig;
        }
    }
}
