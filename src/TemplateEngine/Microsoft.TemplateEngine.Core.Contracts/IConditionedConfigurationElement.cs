// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IConditionedConfigurationElement
    {
        /// <summary>
        /// Gets the condition string to be evaluated.
        /// </summary>
        string? Condition { get; }

        /// <summary>
        /// Gets the cached result of the evaluation.
        /// Make sure <see cref="EvaluateCondition(IEngineEnvironmentSettings, IVariableCollection)"/> is called
        /// at least once before accessing this variable. Otherwise, <see cref="InvalidOperationException"/> will be thrown.
        /// </summary>
        bool ConditionResult { get; }

        /// <summary>
        /// Evaluates the condition and returns the result.
        /// </summary>
        /// <param name="environmentSettings">Settings to be used for evaluation.</param>
        /// <param name="variables">Defined variables that may be referenced from the condition.</param>
        /// <returns>True if the condition has evaluated to true or the condition string is null/empty.
        /// False, otherwise.</returns>
        bool EvaluateCondition(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables);
    }
}
