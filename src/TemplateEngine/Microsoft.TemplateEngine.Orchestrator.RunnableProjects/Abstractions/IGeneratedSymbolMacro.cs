// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions
{
    /// <summary>
    /// An interface for a macro created via generated symbols.
    /// </summary>
    public interface IGeneratedSymbolMacro : IMacro
    {
        /// <summary>
        /// Creates a macro configuration based on the provided engine environment settings and generated symbol configuration.
        /// This method allows to instantiate generated macro during <seealso cref="GlobalRunConfig.Macros"></seealso> population.
        /// </summary>
        /// <param name="environmentSettings">The engine environment settings.</param>
        /// <param name="generatedSymbolConfig">The generated symbol configuration.</param>
        /// <returns>The macro configuration.</returns>
        IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig);
    }

    /// <summary>
    /// An interface for a typed macro created via generated symbols.
    /// </summary>
    /// <typeparam name="T">The type of macro configuration.</typeparam>
    public interface IGeneratedSymbolMacro<T> : IMacro<T> where T : IMacroConfig
    {
        /// <summary>
        /// Creates a macro configuration based on the provided engine environment settings and generated symbol configuration.
        /// Method is needed for supporting <seealso cref="IMacro.EvaluateConfig(IEngineEnvironmentSettings, IVariableCollection, IMacroConfig)"/> invocation.
        /// </summary>
        /// <param name="environmentSettings">The engine environment settings.</param>
        /// <param name="generatedSymbolConfig">The generated symbol configuration.</param>
        /// <returns>The typed macro configuration <typeparamref name="T"/>.</returns>
        T CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig);
    }

}
