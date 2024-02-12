// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    public abstract class ConditionedConfigurationElement
    {
        /// <summary>
        /// Stores the result of the condition evaluation. If the condition
        /// was not evaluated before, the value is null.
        /// </summary>
        private bool? _conditionResult;

        /// <summary>
        /// Gets the condition string to be evaluated so that configuration element is applied.
        /// Usually corresponds to "condition" JSON property.
        /// </summary>
        public string? Condition { get; internal init; }

        /// <summary>
        /// Stores the result of condition evaluation. <see cref="EvaluateCondition(ILogger, IVariableCollection)"/> should be done before accessing this property.
        /// </summary>
        /// <exception cref="InvalidOperationException">when the property is accessed prior to <see cref="EvaluateCondition(ILogger, IVariableCollection)"/> method is called.</exception>
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
        }

        /// <summary>
        /// Evaluates the condition <see cref="Condition"/>.
        /// </summary>
        /// <param name="logger">the logger to be used to log the messages during evaluation.</param>
        /// <param name="variables">the variable collection that will be used to evaluate the condition.</param>
        /// <returns>true if condition evaluates to true or not specified, otherwise false.</returns>
        public bool EvaluateCondition(ILogger logger, IVariableCollection variables)
        {
            bool conditionResult = string.IsNullOrEmpty(Condition) ||
                Cpp2StyleEvaluatorDefinition.EvaluateFromString(logger, Condition!, variables);
            _conditionResult = conditionResult;
            return conditionResult;
        }
    }
}
