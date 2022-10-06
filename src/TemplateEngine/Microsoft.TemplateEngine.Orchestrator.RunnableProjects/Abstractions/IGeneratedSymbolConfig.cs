// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions
{
    /// <summary>
    /// Represents the configuration of <see cref="IGeneratedSymbolMacro"/>.
    /// </summary>
    public interface IGeneratedSymbolConfig : IMacroConfig
    {
        /// <summary>
        /// Gets data type of the variable to be created.
        /// </summary>
        string DataType { get; }

        /// <summary>
        /// Gets the collection of additional macro parameters, where key is a parameter name, and value is a JSON value serialized to string.
        /// </summary>
        IReadOnlyDictionary<string, string> Parameters { get; }
    }
}
