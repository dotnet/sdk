// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions
{
    /// <summary>
    /// Represents a macro that can modify variable collection before template instantiation.
    /// </summary>
    public interface IMacro : IIdentifiedComponent
    {
        /// <summary>
        /// Gets macro type. The type identifies the macro and should be unique.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Evaluates the macro based on <paramref name="config"/>. The result is modification of variable collection <paramref name="vars"/>.
        /// </summary>
        void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config);
    }

    /// <summary>
    /// Represents a macro that can modify variable collection before template instantiation.
    /// </summary>
    public interface IMacro<T> : IMacro where T : IMacroConfig
    {
        /// <summary>
        /// Evaluates the macro based on <paramref name="config"/>. The result is modification of variable collection <paramref name="variables"/>.
        /// </summary>
        void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, T config);
    }
}
