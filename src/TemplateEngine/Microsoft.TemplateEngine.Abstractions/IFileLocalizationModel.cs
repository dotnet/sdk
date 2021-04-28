// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Model type that contains the string-replace operations to be performed
    /// on a file in order to localize it.
    /// </summary>
    public interface IFileLocalizationModel
    {
        /// <summary>
        /// Gets the globbing pattern to determine the files
        /// that these localizations will be applied to.
        /// </summary>
        string File { get; }

        /// <summary>
        /// Gets the dictionary containing the localized strings as values
        /// where the keys are the string to be replaced.
        /// </summary>
        IReadOnlyDictionary<string, string> Localizations { get; }
    }
}
