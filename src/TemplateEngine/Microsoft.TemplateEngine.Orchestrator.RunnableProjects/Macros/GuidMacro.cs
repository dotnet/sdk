using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class GuidMacro : IMacro
    {
        public Guid Id => new Guid("10919008-4E13-4FA8-825C-3B4DA855578E");

        public string Type => "guid";

        public void EvaluateConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            GuidMacroConfig config = rawConfig as GuidMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as GuidMacroConfig");
            }

            switch (config.Action)
            {
                case "new":
                    if (config.Format != null)
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

                    break;
            }
        }

        public void EvaluateDeferredConfig(IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            JToken actionToken;
            if (!deferredConfig.Parameters.TryGetValue("action", out actionToken))
            {
                throw new ArgumentNullException("action");
            }
            string action = actionToken.ToString();

            JToken formatToken;
            if (!deferredConfig.Parameters.TryGetValue("format", out formatToken))
            {
                throw new ArgumentNullException("format");
            }
            string format = formatToken.ToString();

            IMacroConfig realConfig = new GuidMacroConfig(deferredConfig.VariableName, action, format);
            EvaluateConfig(vars, realConfig, parameters, setter);
        }

        public void Evaluate(string variableName, IVariableCollection vars, JObject def, IParameterSet parameters, ParameterSetter setter)
        {
            switch (def.ToString("action"))
            {
                case "new":
                    string fmt = def.ToString("format");
                    if (fmt != null)
                    {
                        Guid g = Guid.NewGuid();
                        string value = char.IsUpper(fmt[0]) ? g.ToString(fmt[0].ToString()).ToUpperInvariant() : g.ToString(fmt[0].ToString()).ToLowerInvariant();
                        Parameter p = new Parameter
                        {
                            IsVariable = true,
                            Name = variableName
                        };

                        setter(p, value);
                    }
                    else
                    {
                        Guid g = Guid.NewGuid();
                        for (int i = 0; i < GuidMacroConfig.DefaultFormats.Length; ++i)
                        {
                            Parameter p = new Parameter
                            {
                                IsVariable = true,
                                Name = variableName + "-" + GuidMacroConfig.DefaultFormats[i]
                            };

                            string rplc = char.IsUpper(GuidMacroConfig.DefaultFormats[i]) 
                                ? g.ToString(GuidMacroConfig.DefaultFormats[i].ToString()).ToUpperInvariant() 
                                : g.ToString(GuidMacroConfig.DefaultFormats[i].ToString()).ToLowerInvariant();
                            setter(p, rplc);
                        }

                        Parameter pd = new Parameter
                        {
                            IsVariable = true,
                            Name = variableName
                        };

                        setter(pd, g.ToString("D"));
                    }

                    break;
            }
        }
    }
}