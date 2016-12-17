using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class ReplacementConfig : IOperationConfig
    {
        public string Key => Replacement.OperationName;

        public Guid Id => new Guid("62DB7F1F-A10E-46F0-953F-A28A03A81CD1");

        public static IOperationProvider Setup(IReplacementTokens tokens, IParameterSet parameters)
        {
            if (parameters.TryGetParameterDefinition(tokens.VariableName, out ITemplateParameter param))
            {
                Replacement replacement;
                try
                {
                    string newValue = (string)(parameters.ResolvedValues[param]);
                    replacement = new Replacement(tokens.OriginalValue, newValue, null);
                }
                catch (KeyNotFoundException ex)
                {
                    throw new Exception($"Unable to find a parameter value called \"{param.Name}\"", ex);
                }

                return replacement;
            }
            else
            {
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