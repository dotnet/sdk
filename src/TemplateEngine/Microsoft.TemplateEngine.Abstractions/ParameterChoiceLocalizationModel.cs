// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Represents the data necessary for the localization of a <see cref="ParameterChoice"/>.
    /// </summary>
    public sealed class ParameterChoiceLocalizationModel
    {
        public ParameterChoiceLocalizationModel(string? displayName, string? description)
        {
            DisplayName = displayName;
            Description = description;
        }

        /// <summary>
        /// Gets the friendly name of the choice to be displayed to the user.
        /// </summary>
        public string? DisplayName { get; private set; }

        /// <summary>
        /// Gets the description of the choice to be displayed to the user.
        /// </summary>
        public string? Description { get; private set; }
    }
}
