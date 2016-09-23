using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CustomFileGlobModel : ICustomFileGlobModel
    {
        public string Glob { get; set; }

        public IReadOnlyList<ICustomOperationModel> Operations { get; set; }

        // TODO: reference to built-in conditional config ???

        public IVariableConfig VariableFormat { get; set; }

        public string FlagPrefix { get; set; }

        public string Condition { get; set; }

        public bool ConditionEvaluation { get; set; }

        public static CustomFileGlobModel FromJObject(JObject globData, string globName)
        {
            // setup the variable config
            IVariableConfig variableConfig;
            JToken variableData;
            if (globData.TryGetValue(nameof(VariableFormat), System.StringComparison.OrdinalIgnoreCase, out variableData))
            {
                variableConfig = VariableConfig.FromJObject((JObject)variableData);
            }
            else
            {
                variableConfig = VariableConfig.DefaultVariableSetup();
            }

            // setup the custom operations
            List<ICustomOperationModel> customOpsForGlob = new List<ICustomOperationModel>();
            JToken operationData;

            if (globData.TryGetValue("Operations", System.StringComparison.OrdinalIgnoreCase, out operationData))
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
