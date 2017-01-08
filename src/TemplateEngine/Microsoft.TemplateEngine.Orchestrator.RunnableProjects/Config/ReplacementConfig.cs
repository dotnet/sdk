using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class ReplacementConfig : IOperationConfig
    {
        public string Key => Replacement.OperationName;

        public Guid Id => new Guid("62DB7F1F-A10E-46F0-953F-A28A03A81CD1");

        public static IOperationProvider Setup(IReplacementTokens tokens, IParameterSet parameters)
        {
            if (RuntimeValueUtil.TryGetRuntimeValue(parameters, tokens.VariableName, out object newValueObject))
            {
                string newValue = newValueObject.ToString();
                return new Replacement(tokens.OriginalValue, newValue, null);
            }
            else
            {
                EngineEnvironmentSettings.Host.LogMessage($"Couldn't find a parameter called {tokens.VariableName}");
                return null;
            }
        }

        public IEnumerable<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration, IDirectory templateRoot)
        {
            string original = rawConfiguration.ToString("original");
            string replacement = rawConfiguration.ToString("replacement");
            string id = rawConfiguration.ToString("id");

            yield return new Replacement(original, replacement, id);
        }
    }
}