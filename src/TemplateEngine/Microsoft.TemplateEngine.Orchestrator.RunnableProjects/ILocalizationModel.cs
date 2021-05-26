// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    /// <summary>
    /// Represents the data model that contains the localized strings for a template.
    /// </summary>
    internal interface ILocalizationModel
    {
        /// <summary>
        /// Gets the localized author name.
        /// </summary>
        string? Author { get; }

        /// <summary>
        /// Gets the localized template name.
        /// </summary>
        string? Name { get; }

        /// <summary>
        /// Gets the localized template description.
        /// </summary>
        string? Description { get; }

        /// <summary>
        /// Gets the localization models for the parameter symbols defined in this template.
        /// </summary>
        IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; }

        /// <summary>
        /// Gets the localization models for the post actions defined in this template.
        /// The keys represent the id of the post actions.
        /// </summary>
        IReadOnlyDictionary<string, IPostActionLocalizationModel> PostActions { get; }
    }
}
