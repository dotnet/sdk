using System;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config
{
    public class EvaluateMacroConfig : IMacroConfig
    {
        public Guid Id => new Guid("06ECDEA1-187C-4AB9-B479-AE551B83DD05");

        public string VariableName { get; private set; }

        public string Type { get; private set; }

        public string Action { get; private set; }

        public string Evaluator { get; set; }

        public EvaluateMacroConfig(string variableName, string action, string evaluator)
        {
            VariableName = variableName;
            Type = "evaluate";
            Action = action;
            Evaluator = evaluator;
        }

        public static EvaluateMacroConfig FromJObject(JObject config, string variableName)
        {
            string action = config.ToString("action");
            string evaluator = config.ToString("evaluator");

            return new EvaluateMacroConfig(variableName, action, evaluator);
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

            string evaluator;
            if (!deferredConfig.Parameters.TryGetValue("evaluator", out evaluator))
            {
                throw new ArgumentNullException("evaluator");
            }

            return new EvaluateMacroConfig(deferredConfig.VariableName, action, evaluator);
        }
    }
}