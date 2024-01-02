// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions.Constraints
{
    /// <summary>
    /// Represents information about template constraint definition in template cache <see cref="ITemplateInfo"/>.
    /// </summary>
    public class TemplateConstraintInfo
    {
        /// <summary>
        /// Creates a new instance of <see cref="TemplateConstraintInfo"/>.
        /// </summary>
        /// <param name="type">Constraint type, matches the type defined in <see cref="ITemplateConstraint"/> implementation and template.json.</param>
        /// <param name="args">Arguments for constraint evaluation.</param>
        /// <exception cref="ArgumentException">when <paramref name="type"/> is null or whitespace.</exception>
        public TemplateConstraintInfo(string type, string? args)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException($"'{nameof(type)}' cannot be null or whitespace.", nameof(type));
            }

            Type = type;
            Args = args;
        }

        /// <summary>
        /// Gets the constraint type.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Gets the constraint arguments.
        /// </summary>
        public string? Args { get; }
    }
}
