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
    internal class ConstantMacro : IMacro, IDeferredMacro
    {
        public Guid Id => new Guid("370996FE-2943-4AED-B2F6-EC03F0B75B4A");

        public string Type => "constant";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            ConstantMacroConfig config = rawConfig as ConstantMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as ConstantMacroConfig");
            }

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

            vars[config.VariableName] = config.Value;
            setter(p, config.Value);
        }

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            if (!deferredConfig.Parameters.TryGetValue("value", out JToken valueToken))
            {
                throw new ArgumentNullException("value");
            }

            string value = valueToken.ToString();
            IMacroConfig realConfig = new ConstantMacroConfig(deferredConfig.DataType, deferredConfig.VariableName, value);
            return realConfig;
        }
    }
}
