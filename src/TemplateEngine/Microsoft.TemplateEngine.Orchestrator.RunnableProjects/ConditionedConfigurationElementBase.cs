// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal abstract class ConditionedConfigurationElementBase : IConditionedConfigurationElement
    {
        private bool _conditionResult;

        public string Condition { get; set; }

        public bool ConditionResult
        {
            get
            {
                if (!IsConditionEvaluated)
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

        public void EvaluateCondition(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables)
        {
            if (string.IsNullOrEmpty(Condition)
                || Cpp2StyleEvaluatorDefinition.EvaluateFromString(environmentSettings, Condition, variables))
            {
                ConditionResult = true;
            }
            else
            {
                ConditionResult = false;
            }
        }
    }
}
