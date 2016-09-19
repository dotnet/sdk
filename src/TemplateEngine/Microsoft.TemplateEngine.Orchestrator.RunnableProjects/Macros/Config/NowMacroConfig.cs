using System;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class NowMacroConfig : IMacroConfig
    {
        public Guid Id => new Guid("6FE91009-D873-4506-B383-398F26FBF05A");

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public bool Utc { get; private set; }

        public NowMacroConfig(string variableName, string action, bool utc)
        {
            VariableName = variableName;
            Type = "now";
            Action = action;
            Utc = utc;
        }

        public static NowMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");
            bool utc = config.ToBool("utc");

            return new NowMacroConfig(variableName, action, utc);
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

            bool utc;
            string utcString;
            if (deferredConfig.Parameters.TryGetValue("utc", out utcString))
            {
                utc = Convert.ToBoolean(utcString);
            }
            else
            {
                utc = false;
            }

            return new NowMacroConfig(deferredConfig.VariableName, action, utc);
        }
    }
}