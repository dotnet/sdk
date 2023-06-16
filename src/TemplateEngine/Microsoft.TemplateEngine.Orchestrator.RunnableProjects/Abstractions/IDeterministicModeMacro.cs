// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions
{
    /// <summary>
    /// A macro that supports deterministic mode (for test purposes).
    /// </summary>
    internal interface IDeterministicModeMacro : IMacro
    {
        /// <summary>
        /// Evaluates the macro based on <paramref name="config"/>. The result is modification of variable collection <paramref name="variables"/>.
        /// The evaluation is performed deterministically, i.e. different external factors cannot impact the result and the recurrent evaluation is guaranteed to provide same result.
        /// </summary>
        void EvaluateConfigDeterministically(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IMacroConfig config);
    }

    /// <summary>
    /// A macro that supports deterministic mode (for test purposes).
    /// </summary>
    internal interface IDeterministicModeMacro<T> : IMacro<T> where T : IMacroConfig
    {
        /// <summary>
        /// Evaluates the macro based on <paramref name="config"/>. The result is modification of variable collection <paramref name="variables"/>.
        /// The evaluation is performed deterministically, i.e. different external factors cannot impact the result and the recurrent evaluation is guaranteed to provide same result.
        /// </summary>
        void EvaluateDeterministically(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, T config);
    }
}
