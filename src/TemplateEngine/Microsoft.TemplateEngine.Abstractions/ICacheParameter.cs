// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines the representation of a parameter in template cache.
    /// This doesn't include parameters with choices which are represented by <see cref="ICacheTag"/>.
    /// </summary>
    public interface ICacheParameter
    {
        /// <summary>
        /// Gets the type of the parameter.
        /// </summary>
        string? DataType { get; }

        /// <summary>
        /// Gets the default value to be used if the user did not provide a value.
        /// </summary>
        string? DefaultValue { get; }

        /// <summary>
        /// Gets the friendly name of the parameter to be displayed to the user.
        /// This property is localized if localizations are provided.
        /// </summary>
        string? DisplayName { get; }

        /// <summary>
        /// Gets the detailed description of the parameter to be displayed to the user.
        /// This property is localized if localizations are provided.
        /// </summary>
        string? Description { get; }
    }
}
