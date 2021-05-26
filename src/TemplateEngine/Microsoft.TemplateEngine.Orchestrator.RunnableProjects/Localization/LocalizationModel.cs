// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    internal class LocalizationModel : ILocalizationModel
    {
        public LocalizationModel(
            string? name,
            string? description,
            string? author,
            IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> parameterSymbols,
            IReadOnlyDictionary<string, IPostActionLocalizationModel> postActions)
        {
            Name = name;
            Description = description;
            Author = author;
            ParameterSymbols = parameterSymbols ?? throw new ArgumentNullException(nameof(parameterSymbols));
            PostActions = postActions ?? throw new ArgumentNullException(nameof(postActions));
        }

        /// <inheritdoc/>
        public string? Author { get; }

        /// <inheritdoc/>
        public string? Name { get; }

        /// <inheritdoc/>
        public string? Description { get; }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, IPostActionLocalizationModel> PostActions { get; }
    }
}
