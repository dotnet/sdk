// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Represents a localization file for a template.
    /// </summary>
    public interface ILocalizationLocator
    {
        /// <summary>
        /// Gets the locale of the localizations.
        /// </summary>
        string Locale { get; }

        /// <summary>
        /// Gets the location on disk where the localization file resides.
        /// </summary>
        string ConfigPlace { get; }

        /// <summary>
        /// Gets the identity of the template that the localizations in this object belong to.
        /// </summary>
        string Identity { get; }

        /// <summary>
        /// Gets the localized author name.
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Gets the localized template name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the localized template description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the localization models for the parameter symbols defined in this template.
        /// </summary>
        IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; }
    }
}
