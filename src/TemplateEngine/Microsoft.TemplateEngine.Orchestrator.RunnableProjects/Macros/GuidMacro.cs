using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    public class GuidMacro : IMacro, IDeferredMacro
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

            if (! string.IsNullOrEmpty(config.Format))
            {
                Guid g = Guid.NewGuid();
                string value = char.IsUpper(config.Format[0]) ? g.ToString(config.Format[0].ToString()).ToUpperInvariant() : g.ToString(config.Format[0].ToString()).ToLowerInvariant();
                Parameter p = new Parameter
                {
                    IsVariable = true,
                    Name = config.VariableName
                };
                setter(p, value);
            }
            else
            {
                Guid g = Guid.NewGuid();
                string guidFormats = GuidMacroConfig.DefaultFormats;
                for (int i = 0; i < guidFormats.Length; ++i)
                {
                    string value = char.IsUpper(guidFormats[i]) ? g.ToString(guidFormats[i].ToString()).ToUpperInvariant() : g.ToString(guidFormats[i].ToString()).ToLowerInvariant();
                    Parameter p = new Parameter
                    {
                        IsVariable = true,
                        Name = config.VariableName + "-" + guidFormats[i]
                    };

                    setter(p, value);
                }

                Parameter pd = new Parameter
                {
                    IsVariable = true,
                    Name = config.VariableName
                };

                setter(pd, g.ToString("D"));
            }
        }

        public void EvaluateDeferredConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            if (!deferredConfig.Parameters.TryGetValue("format", out JToken formatToken))
            {
                throw new ArgumentNullException("format");
            }
            string format = formatToken?.ToString();

            IMacroConfig realConfig = new GuidMacroConfig(deferredConfig.VariableName, format);
            EvaluateConfig(environmentSettings, vars, realConfig, parameters, setter);
        }
    }
}