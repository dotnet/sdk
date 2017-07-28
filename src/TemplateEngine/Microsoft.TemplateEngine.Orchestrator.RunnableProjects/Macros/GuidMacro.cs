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
                    Name = config.VariableName + "-" + guidFormats[i]
                };

                vars[p.Name] = value;
                setter(p, value);
            }

            Parameter pd = new Parameter
            {
                IsVariable = true,
                Name = config.VariableName
            };

            vars[config.VariableName] = g.ToString("D");
            setter(pd, g.ToString("D"));
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

            IMacroConfig realConfig = new GuidMacroConfig(deferredConfig.VariableName, format);
            return realConfig;
        }
    }
}
