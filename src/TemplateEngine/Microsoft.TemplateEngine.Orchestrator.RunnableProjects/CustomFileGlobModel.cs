using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CustomFileGlobModel : ICustomFileGlobModel
    {
        public CustomFileGlobModel()
        {
            IsConditionEvaluated = false;
        }

        public string Glob { get; set; }

        public IReadOnlyList<ICustomOperationModel> Operations { get; set; }

        // TODO: reference to built-in conditional config ???

        public IVariableConfig VariableFormat { get; set; }

        public string FlagPrefix { get; set; }

        public string Condition { get; set; }

        private bool _conditionResult;

        public bool ConditionResult
        {
            get
            {
                if (! IsConditionEvaluated)
                {
                    throw new InvalidOperationException("ConditionResult access attempted prior to evaluation");
                }

                return _conditionResult;
            }
            private set
            {
                _conditionResult = value;
                IsConditionEvaluated = true;
            }
        }

        // tracks whether or not the condition has been evaluated.
        // will cause ConditionResult.get to throw if evaluation hasn't happened.
        private bool IsConditionEvaluated { get; set; }

        public void EvaluateCondition(IVariableCollection variables)
        {
            if (string.IsNullOrEmpty(Condition)
                || CppStyleEvaluatorDefinition.EvaluateFromString(Condition, variables))
            {
                ConditionResult = true;
            }
            else
            {
                ConditionResult = false;
            }
        }

        public static CustomFileGlobModel FromJObject(JObject globData, string globName)
        {
            // setup the variable config
            IVariableConfig variableConfig;
            if (globData.TryGetValue(nameof(VariableFormat), System.StringComparison.OrdinalIgnoreCase, out JToken variableData))
            {
                variableConfig = VariableConfig.FromJObject((JObject)variableData);
            }
            else
            {
                variableConfig = VariableConfig.DefaultVariableSetup();
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
