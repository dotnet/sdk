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
        public LocalizationModel() : this(
            new Dictionary<string, IParameterSymbolLocalizationModel>(),
            new List<IPostActionLocalizationModel>(),
            new List<IFileLocalizationModel>())
        { }

        public LocalizationModel(
            IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> parameterSymbols,
            IReadOnlyList<IPostActionLocalizationModel?> postActions,
            IReadOnlyList<IFileLocalizationModel> fileLocalizations)
        {
            ParameterSymbols = parameterSymbols ?? throw new ArgumentNullException(nameof(parameterSymbols));
            PostActions = postActions ?? throw new ArgumentNullException(nameof(postActions));
            FileLocalizations = fileLocalizations ?? throw new ArgumentNullException(nameof(fileLocalizations));
        }

        public string? Author { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Identity { get; set; }

        public IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> ParameterSymbols { get; set; }

        public IReadOnlyList<IPostActionLocalizationModel?> PostActions { get; set; }

        public IReadOnlyList<IFileLocalizationModel> FileLocalizations { get; set; }
    }
}
