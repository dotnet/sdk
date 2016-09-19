using System;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class RandomMacroConfig : IMacroConfig
    {
        public Guid Id => new Guid("BE13A293-A810-4FD7-AD80-22B4C2F6848A");

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public int Low { get; private set; }

        public int High { get; private set; }

        public RandomMacroConfig(string variableName, string action, int low, int? high)
        {
            VariableName = variableName;
            Type = "random";
            Action = action;
            Low = low;
            High = high ?? int.MaxValue;
        }

        public static RandomMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");
            int low = config.ToInt32("low");
            int high = config.ToInt32("high", int.MaxValue);

            return new RandomMacroConfig(variableName, action, low, high);
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

            string lowString;
            string highString;
            int low;
            int high;

            if (!deferredConfig.Parameters.TryGetValue("low", out lowString))
            {
                throw new ArgumentNullException("low");
            }
            else
            {
                low = Convert.ToInt32(lowString);
            }

            if (!deferredConfig.Parameters.TryGetValue("high", out highString))
            {
                high = int.MaxValue;
            }
            else
            {
                high = Convert.ToInt32(highString);
            }

            return new RandomMacroConfig(deferredConfig.VariableName, action, low, high);
        }
    }
}

    
