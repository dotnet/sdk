// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions
{
    /// <summary>
    /// An interface for macros created that can create the config from other config (deferred config).
    /// </summary>
    [Obsolete("Use IGeneratedSymbolConfig instead.")]
    public interface IDeferredMacro : IMacro
    {
        /// <summary>
        /// Creates <see cref="IMacroConfig"/> from <paramref name="rawConfig"/>.
        /// </summary>
        /// <remarks>
        /// Deprecated as <see cref="IMacro"/> can process only own configuration.
        /// </remarks>
        [Obsolete("Use IMacro{T}.Evaluate or IGeneratedSymbolConfig.Evaluate instead for generated symbol instead.")]
        IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IMacroConfig rawConfig);
    }
}
