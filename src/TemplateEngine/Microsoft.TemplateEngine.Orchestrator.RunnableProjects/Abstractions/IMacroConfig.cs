// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions
{
    /// <summary>
    /// Base interface for <see cref="IMacro"/> configurations.
    /// </summary>
    public interface IMacroConfig
    {
        /// <summary>
        /// Gets the variable name for this <see cref="IMacro"/>.
        /// </summary>
        string VariableName { get; }

        /// <summary>
        /// Gets <see cref="IMacro"/> type.
        /// </summary>
        string Type { get; }
    }
}
