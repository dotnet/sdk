// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines a parameter localization model.
    /// </summary>
    public interface IParameterSymbolLocalizationModel
    {
        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the localized friendly name of the symbol to be displayed to the user.
        /// </summary>
        string? DisplayName { get; }

        /// <summary>
        /// Gets the localized description of the symbol.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Gets the localization models for choices of this symbol.
        /// </summary>
        IReadOnlyDictionary<string, ParameterChoiceLocalizationModel> Choices { get; }
    }
}
