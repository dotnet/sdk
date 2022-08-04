// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.SymbolModel
{
    internal abstract class BaseSymbol
    {
        protected BaseSymbol(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace.", nameof(name));
            }
            Name = name;
        }

        protected BaseSymbol(BaseSymbol clone)
        {
            Name = clone.Name;
        }

        /// <summary>
        /// Gets the name of the symbol.
        /// </summary>
        internal string Name { get; }

        /// <summary>
        /// Gets the type of the symbol.
        /// </summary>
        internal abstract string Type { get; }

    }
}
