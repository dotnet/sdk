using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CustomFileGlobModel : ConditionedConfigurationElementBase, ICustomFileGlobModel
    {
        public string Glob { get; set; }

        public IReadOnlyList<ICustomOperationModel> Operations { get; set; }

        // TODO: reference to built-in conditional config ???

        public IVariableConfig VariableFormat { get; set; }

        public string FlagPrefix { get; set; }

        internal static CustomFileGlobModel FromJObject(JObject globData, string globName)
        {
            // setup the variable config
            IVariableConfig variableConfig;
            if (globData.TryGetValue(nameof(VariableFormat), System.StringComparison.OrdinalIgnoreCase, out JToken variableData))
            {
                variableConfig = VariableConfig.FromJObject((JObject)variableData);
            }
            else
            {
                variableConfig = VariableConfig.DefaultVariableSetup(null);
            }

            // setup the custom operations
            List<ICustomOperationModel> customOpsForGlob = new List<ICustomOperationModel>();
            if (globData.TryGetValue("Operations", StringComparison.OrdinalIgnoreCase, out JToken operationData))
            {
                foreach (JObject operationConfig in (JArray)operationData)
                {
                    customOpsForGlob.Add(CustomOperationModel.FromJObject(operationConfig));
                }
            }

            CustomFileGlobModel globModel = new CustomFileGlobModel()
            {
                Glob = globName,
                Operations = customOpsForGlob,
                VariableFormat = variableConfig,
                FlagPrefix = globData.ToString(nameof(FlagPrefix)),
                Condition = globData.ToString(nameof(Condition))
            };

            return globModel;
        }
    }
}
