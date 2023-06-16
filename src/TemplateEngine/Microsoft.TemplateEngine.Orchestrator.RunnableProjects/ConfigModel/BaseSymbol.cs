// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel
{
    public abstract class BaseSymbol
    {
        private protected BaseSymbol(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or whitespace.", nameof(name));
            }
            Name = name;
        }

        private protected BaseSymbol(BaseSymbol clone)
        {
            Name = clone.Name;
        }

        /// <summary>
        /// Gets the name of the symbol.
        /// Corresponds to key that defines the symbol in "symbols" JSON object.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type of the symbol.
        /// Corresponds to "type" JSON property.
        /// </summary>
        public abstract string Type { get; }

        /// <summary>
        /// Indicates that the symbol is implicit and was created by generator itself.
        /// Those symbols are not part of JSON.
        /// </summary>
        internal bool IsImplicit { get; init; }
    }
}
