using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    public class CoalesceMacro : IMacro, IDeferredMacro
    {
        public string Type => "coalesce";

        public Guid Id => new Guid("11C6EACF-8D24-42FD-8FC6-84063FCD8F14");

        public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            string sourceVariableName = null;
            if (deferredConfig.Parameters.TryGetValue("sourceVariableName", out JToken sourceVariableToken) && sourceVariableToken.Type == JTokenType.String)
            {
                sourceVariableName = sourceVariableToken.ToString();
            }

            string defaultValue = null;
            if (deferredConfig.Parameters.TryGetValue("defaultValue", out JToken defaultValueToken) && defaultValueToken.Type == JTokenType.String)
            {
                defaultValue = defaultValueToken.ToString();
            }

            string fallbackVariableName = null;
            if (deferredConfig.Parameters.TryGetValue("fallbackVariableName", out JToken fallbackVariableNameToken) && fallbackVariableNameToken.Type == JTokenType.String)
            {
                fallbackVariableName = fallbackVariableNameToken.ToString();
            }

            IMacroConfig realConfig = new CoalesceMacroConfig(deferredConfig.VariableName, sourceVariableName, defaultValue, fallbackVariableName);
            return realConfig;
        }

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config, IParameterSet parameters, ParameterSetter setter)
        {
            CoalesceMacroConfig realConfig = config as CoalesceMacroConfig;

            if (realConfig == null)
            {
                throw new InvalidCastException("Unable to cast config as a CoalesceMacroConfig");
            }

            object targetValue = null;

            if (vars.TryGetValue(realConfig.SourceVariableName, out object currentSourceValue) && !Equals(currentSourceValue ?? string.Empty, realConfig.DefaultValue ?? string.Empty))
            {
                targetValue = currentSourceValue;
            }
            else
            {
                if (!vars.TryGetValue(realConfig.FallbackVariableName, out targetValue))
                {
                    environmentSettings.Host.LogDiagnosticMessage("Unable to find a variable to fall back to called " + realConfig.FallbackVariableName, "Authoring", realConfig.SourceVariableName, realConfig.DefaultValue);
                    targetValue = realConfig.DefaultValue;
                }
            }

            Parameter pd = new Parameter
            {
                IsVariable = true,
                Name = config.VariableName
            };

            vars[config.VariableName] = targetValue?.ToString();
            setter(pd, targetValue?.ToString());
        }
    }
}
