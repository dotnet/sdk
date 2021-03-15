// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Represents a choice that can be assigned to a parameter.
    /// </summary>
    public sealed class ParameterChoice
    {
        public ParameterChoice(string? displayName, string? description)
        {
            DisplayName = displayName;
            Description = description;
        }

        /// <summary>
        /// Gets or sets the friendly name of the choice to be displayed to the user.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the description of the choice to be displayed to the user.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Localizes the choice with the given localization model.
        /// </summary>
        public void Localize(ParameterChoiceLocalizationModel localizationModel)
        {
            DisplayName = localizationModel.DisplayName ?? DisplayName;
            Description = localizationModel.Description ?? Description;
        }
    }
}
