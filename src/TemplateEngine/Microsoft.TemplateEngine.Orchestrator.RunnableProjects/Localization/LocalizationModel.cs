// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    /// <summary>
    /// Represents the data model that contains the localized strings for a template.
    /// </summary>
    internal class LocalizationModel
    {
        public LocalizationModel(
            string? name,
            string? description,
            string? author,
            IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> parameterSymbols,
            IReadOnlyDictionary<string, PostActionLocalizationModel> postActions)
        {
            Name = name;
            Description = description;
            Author = author;
            ParameterSymbols = parameterSymbols ?? throw new ArgumentNullException(nameof(parameterSymbols));
            PostActions = postActions ?? throw new ArgumentNullException(nameof(postActions));
        }

        /// <summary>
        /// Gets the localized author name.
        /// </summary>
        public string? Author { get; }

        /// <summary>
        /// Gets the localized template name.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets the localized template description.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Gets the localization models for the parameter symbols defined in this template.
        /// </summary>
        public IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; }

        /// <summary>
        /// Gets the localization models for the post actions defined in this template.
        /// The keys represent the id of the post actions.
        /// </summary>
        public IReadOnlyDictionary<string, PostActionLocalizationModel> PostActions { get; }
    }
}
