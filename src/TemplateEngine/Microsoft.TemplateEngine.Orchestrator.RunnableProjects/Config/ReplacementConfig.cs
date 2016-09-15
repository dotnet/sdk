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
        public int Order => 10000;

        public string Key => "replacements";

        public Guid Id => new Guid("62DB7F1F-A10E-46F0-953F-A28A03A81CD1");

        public static IOperationProvider Setup(IReplacementTokens tokens, IParameterSet parameters)
        {
            ITemplateParameter param;
            if (parameters.TryGetParameterDefinition(tokens.Identity, out param))
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

        public IEnumerable<IOperationProvider> Process(IComponentManager componentManager, JObject rawConfiguration, IDirectory templateRoot, IVariableCollection variables, IParameterSet parameters)
        {
            foreach (JProperty property in rawConfiguration.Properties())
            {
                if (string.IsNullOrEmpty(property.Name))
                {
                    continue;
                }

                ITemplateParameter param;
                if (parameters.TryGetParameterDefinition(property.Value.ToString(), out param))
                {
                    Replacement r;
                    try
                    {
                        string val = (string)(parameters.ResolvedValues[param]);
                        r = new Replacement(property.Name, val, null);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        JObject v = property.Value as JObject;

                        if (v != null)
                        {
                            string id = v.ToString("id");
                            string replacement = v.ToString("replaceWith");
                            r = new Replacement(property.Name, replacement, id);
                        }
                        else
                        {
                            throw new Exception($"Unable to find a parameter value called \"{param.Name}\"", ex);
                        }
                    }

                    yield return r;
                }
            }
        }
    }
}