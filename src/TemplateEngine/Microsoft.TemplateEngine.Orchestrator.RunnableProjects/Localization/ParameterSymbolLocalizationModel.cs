// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    internal class ParameterSymbolLocalizationModel : IParameterSymbolLocalizationModel
    {
        internal ParameterSymbolLocalizationModel(string name, string? displayName, string? description, IReadOnlyDictionary<string, ParameterChoiceLocalizationModel> choices)
        {
            Name = name;
            DisplayName = displayName;
            Description = description;
            Choices = choices;
        }

        public string Name { get; }

        public string? DisplayName { get; }

        public string? Description { get; }

        public IReadOnlyDictionary<string, ParameterChoiceLocalizationModel> Choices { get; }
    }
}
