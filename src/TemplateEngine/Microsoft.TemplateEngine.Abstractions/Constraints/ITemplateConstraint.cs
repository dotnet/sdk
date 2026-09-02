// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Constraints
{
    /// <summary>
    /// Constraint that can be used to filter out <see cref="ITemplateInfo"/> from further processing.
    /// </summary>
    public interface ITemplateConstraint
    {
        /// <summary>
        /// Gets the constraint type. Should be unique and match the definition in `template.json`  and type given in <see cref="ITemplateConstraintFactory"/>.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Gets the user friendly constraint name, that can be used in UI.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Evaluates if the constraint is met based on <paramref name="args"/>.
        /// </summary>
        TemplateConstraintResult Evaluate(string? args);
    }
}

