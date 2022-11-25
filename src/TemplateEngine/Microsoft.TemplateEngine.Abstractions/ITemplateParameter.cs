// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Template parameter definition.
    /// </summary>
    public interface ITemplateParameter : IEquatable<ITemplateParameter>
    {
        [Obsolete("Use Description instead.")]
        string? Documentation { get; }

        /// <summary>
        /// Gets parameter description.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Gets parameter name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets parameter priority.
        /// </summary>
        [Obsolete("Use Precedence instead.")]
        TemplateParameterPriority Priority { get; }

        /// <summary>
        /// Indicates the precedence of the parameter.
        /// </summary>
        TemplateParameterPrecedence Precedence { get; }

        /// <summary>
        /// Gets parameter type.
        /// In Orchestrator.RunnableProjects the type is always 'parameter'.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Returns true when parameter is default name symbol.
        /// </summary>
        bool IsName { get; }

        /// <summary>
        /// Gets the default value to be used if the parameter is not passed for template instantiation.
        /// </summary>
        string? DefaultValue { get; }

        /// <summary>
        /// Gets data type of parameter (boolean, string, choice, etc).
        /// </summary>
        string DataType { get; }

        /// <summary>
        /// Gets collection of choices for choice <see cref="DataType"/>.
        /// <c>null</c> for other <see cref="DataType"/>s.
        /// </summary>
        IReadOnlyDictionary<string, ParameterChoice>? Choices { get; }

        /// <summary>
        /// Gets the friendly name of the parameter to be displayed to the user.
        /// This property is localized if localizations are provided.
        /// May contain accelerator key (_) which should be processed or removed.
        /// </summary>
        string? DisplayName { get; }

        /// <summary>
        /// Indicates whether parameter arity is allowed to be > 1.
        /// </summary>
        bool AllowMultipleValues { get; }

        /// <summary>
        /// Gets the default value to be used if the parameter is passed without value for template instantiation.
        /// </summary>
        string? DefaultIfOptionWithoutValue { get; }
    }
}
