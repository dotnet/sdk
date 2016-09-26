using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CreationPathModel : ICreationPathModel
    {
        public CreationPathModel()
        {
            IsConditionEvaluated = false;
        }

        public string PathOriginal { get; set; }

        public string PathResolved { get; set; }

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

        public static IReadOnlyList<ICreationPathModel> ListFromJArray(JArray jsonData)
        {
            List<ICreationPathModel> modelList = new List<ICreationPathModel>();

            if (jsonData == null)
            {
                return modelList;
            }

            foreach (JToken pathInfo in jsonData)
            {
                ICreationPathModel pathModel = new CreationPathModel()
                {
                    PathOriginal = pathInfo.ToString("path"),
                    Condition = pathInfo.ToString("condition")
                };

                modelList.Add(pathModel);
            }

            return modelList;
        }
    }
}
