// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions
{
    /// <summary>
    /// Represents a configuration for the macro that configures dependencies on other symbols.
    /// </summary>
    public interface IMacroConfigDependency
    {
        /// <summary>
        /// Gets the set of symbol names required by the macro.
        /// ResolveSymbolDependencies method should be called prior to accessing the property.
        /// Before it the property won't be populated.
        /// </summary>
        /// <exception cref="ArgumentException">when <see cref="ResolveSymbolDependencies"/> is not called for the property population.</exception>
        HashSet<string> Dependencies { get; }

        /// <summary>
        /// Resolves the macro dependencies out of the provided list of symbols.
        /// As the result of method execution, <see cref="Dependencies" /> should be populated.
        /// </summary>
        /// <param name="symbols">The list of symbols that exist in configuration.
        /// The method should identify which of those symbols are the dependencies for given macro.</param>
        void ResolveSymbolDependencies(IReadOnlyList<string> symbols);
    }

}
