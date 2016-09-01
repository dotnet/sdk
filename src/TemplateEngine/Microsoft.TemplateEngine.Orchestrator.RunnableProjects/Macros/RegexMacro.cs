using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class RegexMacro : IMacro
    {
        public string Type => "regex";

        public void Evaluate(string variableName, IVariableCollection vars, JObject def, IParameterSet parameters, ParameterSetter setter)
        {
            string action = def.ToString("action");
            string value = null;

            switch (action)
            {
                case "replace":
                    string sourceVar = def.ToString("source");
                    JArray steps = def.Get<JArray>("steps");
                    object working;
                    if (!vars.TryGetValue(sourceVar, out working))
                    {
                        ITemplateParameter param;
                        object resolvedValue;
                        if (!parameters.TryGetParameterDefinition(sourceVar, out param) || !parameters.ResolvedValues.TryGetValue(param, out resolvedValue))
                        {
                            value = string.Empty;
                        }
                        else
                        {
                            value = (string)resolvedValue;
                        }
                    }
                    else
                    {
                        value = working?.ToString() ?? "";
                    }

                    if (steps != null)
                    {
                        foreach (JToken child in steps)
                        {
                            JObject map = (JObject)child;
                            string regex = map.ToString("regex");
                            string replaceWith = map.ToString("replacement");

                            value = Regex.Replace(value, regex, replaceWith);
                        }
                    }
                    break;
            }

            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            setter(p, value);
        }
    }
}