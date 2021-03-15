// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IParameterSymbolLocalizationModel
    {
        string Name { get; }

        /// <summary>
        /// Gets the localized friendly name of the symbol to be displayed to the user.
        /// </summary>
        string? DisplayName { get; }

        string? Description { get; }

        IReadOnlyDictionary<string, ParameterChoiceLocalizationModel> Choices { get; }
    }
}
