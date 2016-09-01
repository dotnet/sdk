using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal class MacrosConfig : IOperationConfig
    {
        private static readonly IReadOnlyDictionary<string, IMacro> Macros;

        static MacrosConfig()
        {
            Dictionary<string, IMacro> macros = new Dictionary<string, IMacro>();

            IMacro constant = new ConstantMacro();
            macros[constant.Type] = constant;

            IMacro random = new RandomMacro();
            macros[random.Type] = random;

            IMacro now = new NowMacro();
            macros[now.Type] = now;

            IMacro evaluate = new EvaluateMacro();
            macros[evaluate.Type] = evaluate;

            IMacro guid = new GuidMacro();
            macros[guid.Type] = guid;

            IMacro regex = new RegexMacro();
            macros[regex.Type] = regex;

            Macros = macros;
        }

        public int Order => -10000;

        public string Key => "macros";

        public IEnumerable<IOperationProvider> Process(JObject rawConfiguration, IDirectory templateRoot, IVariableCollection variables, IParameterSet parameters)
        {
            ParameterSetter setter = (p, value) =>
            {
                ((RunnableProjectGenerator.ParameterSet) parameters).AddParameter(p);
                parameters.ResolvedValues[p] = value;
            };

            foreach (JProperty property in rawConfiguration.Properties())
            {
                string variableName = property.Name;
                JObject def = (JObject)property.Value;
                string macroType = def["type"].ToString();

                IMacro macroObject;
                if (Macros.TryGetValue(macroType, out macroObject))
                {
                    macroObject.Evaluate(variableName, variables, def, parameters, setter);
                }
            }

            return Empty<IOperationProvider>.List.Value;
        }
    }
}