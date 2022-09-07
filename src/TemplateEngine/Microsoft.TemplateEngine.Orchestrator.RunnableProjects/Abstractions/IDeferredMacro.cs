// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions
{
    /// <summary>
    /// An interface for macros created that can create the config from other config (deffered config).
    /// </summary>
    [Obsolete("Use IGeneratedSymbolConfig{T} instead.")]
    public interface IDeferredMacro : IMacro
    {
        /// <summary>
        /// Creates <see cref="IMacroConfig"/> from <paramref name="rawConfig"/>.
        /// </summary>
        /// <remarks>
        /// Deprecated as <see cref="IMacro"/> can process only own configuration. Use generic version of interface and <see cref="IGeneratedSymbolMacro{T}.CreateConfig(IEngineEnvironmentSettings, IGeneratedSymbolConfig)"/> instead.
        /// </remarks>
        [Obsolete("Use IGeneratedSymbolConfig{T}.Evaluate or IGeneratedSymbolConfig{T}.CreateConfig instead")]
        IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig);
    }
}
