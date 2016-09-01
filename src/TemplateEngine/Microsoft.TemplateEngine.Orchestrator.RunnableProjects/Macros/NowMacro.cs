using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class NowMacro : IMacro
    {
        public string Type => "now";

        public void Evaluate(string variableName, IVariableCollection vars, JObject def, IParameterSet parameters, ParameterSetter setter)
        {
            string format = def.ToString("action");
            bool utc = def.ToBool("utc");
            DateTime time = utc ? DateTime.UtcNow : DateTime.Now;
            string value = time.ToString(format);
            Parameter p = new Parameter
            {
                IsVariable = true,
                Name = variableName
            };

            setter(p, value);
        }
    }
}