// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions
{
    /// <summary>
    /// An interface for macro created via generated symbols.
    /// </summary>
    public interface IGeneratedSymbolMacro : IMacro
    {
        /// <summary>
        /// Evaluates macro defined via generated symbol (<see cref="IGeneratedSymbolConfig"/>).
        /// The result of macro evaluation is modification of variable collection <paramref name="variables"/>.
        /// </summary>
        void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IGeneratedSymbolConfig generatedSymbolConfig);
    }

    /// <summary>
    /// An interface for macro created via generated symbols, that can create config from generated symbol config (<see cref="IGeneratedSymbolConfig"/>).
    /// </summary>
    /// <typeparam name="T">The type of macro config.</typeparam>
    public interface IGeneratedSymbolMacro<T> : IGeneratedSymbolMacro, IMacro<T>
        where T : IMacroConfig
    {
        /// <summary>
        /// Creates macro config from <see cref="IGeneratedSymbolConfig"/>.
        /// </summary>
        T CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig);
    }
}
