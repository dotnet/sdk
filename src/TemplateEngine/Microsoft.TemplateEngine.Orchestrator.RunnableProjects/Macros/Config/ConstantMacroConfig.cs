using System;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class ConstantMacroConfig : IMacroConfig
    {
        public Guid Id => new Guid("36640164-72B0-4F40-8630-D0CA029CBA8E");

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public ConstantMacroConfig(string variableName, string action)
        {
            VariableName = variableName;
            Type = "constant";
            Action = action;
        }

        public static ConstantMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");

            return new ConstantMacroConfig(variableName, action);
        }

        public IMacroConfig ConfigFromDeferredConfig(IMacroConfig rawConfig)
        {
            GeneratedSymbolDeferredMacroConfig deferredConfig = rawConfig as GeneratedSymbolDeferredMacroConfig;

            if (deferredConfig == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as a GeneratedSymbolDeferredMacroConfig");
            }

            string action;
            if (!deferredConfig.Parameters.TryGetValue("action", out action))
            {
                throw new ArgumentNullException("action");
            }

            return new ConstantMacroConfig(deferredConfig.VariableName, action);
        }
    }
}
