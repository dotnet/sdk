// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal abstract class ConditionedConfigurationElementBase : IConditionedConfigurationElement
    {
        /// <summary>
        /// Stores the result of the condition evaluation. If the condition
        /// was not evaluated before, the value is null.
        /// </summary>
        private bool? _conditionResult;

        /// <summary>
        /// Gets or sets the condition string to be evaluated.
        /// </summary>
        public string? Condition { get; set; }

        /// <inheritdoc/>
        public bool ConditionResult
        {
            get
            {
                if (!_conditionResult.HasValue)
                {
                    throw new InvalidOperationException("ConditionResult access attempted prior to evaluation.");
                }

                return _conditionResult.Value;
            }

            private set
            {
                _conditionResult = value;
            }
        }

        /// <inheritdoc/>
        public bool EvaluateCondition(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables)
        {
            return ConditionResult = string.IsNullOrEmpty(Condition) ||
                Cpp2StyleEvaluatorDefinition.EvaluateFromString(environmentSettings, Condition, variables);
        }
    }
}
