// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// Model, representing the data of a symbol.
    /// </summary>
    internal interface ISymbolModel
    {
        /// <summary>
        /// Gets the type of the symbol.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Gets the name of the host property or the environment variable which will provide the value of this symbol.
        /// </summary>
        string? Binding { get; }

        /// <summary>
        /// Gets the text that should be replaced by the value of this symbol.
        /// </summary>
        string? Replaces { get; }

        /// <summary>
        /// Gets the replacement contexts that determine when this symbol is allowed to do replacement operations.
        /// </summary>
        IReadOnlyList<IReplacementContext> ReplacementContexts { get; }

        /// <summary>
        /// Gets the part of the file name that should be replaced with the value of this symbol.
        /// </summary>
        string? FileRename { get; }
    }
}
