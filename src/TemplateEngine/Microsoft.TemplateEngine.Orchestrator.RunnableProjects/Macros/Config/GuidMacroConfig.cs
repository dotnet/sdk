using System;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class GuidMacroConfig : IMacroConfig
    {
        public Guid Id => new Guid("DC307993-C42F-48EB-9190-B31AA082638D");

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public string Format { get; private set; }

        public static readonly string DefaultFormats = "ndbpxNDPBX";

        public GuidMacroConfig(string variableName, string action, string format)
        {
            VariableName = variableName;
            Type = "guid";
            Action = action;
            Format = format;
        }

        public static GuidMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");
            string format = config.ToString("format");

            return new GuidMacroConfig(variableName, action, format);
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

            string format;
            if (!deferredConfig.Parameters.TryGetValue("format", out format))
            {
                throw new ArgumentNullException("format");
            }

            return new GuidMacroConfig(deferredConfig.VariableName, action, format);
        }
    }
}
