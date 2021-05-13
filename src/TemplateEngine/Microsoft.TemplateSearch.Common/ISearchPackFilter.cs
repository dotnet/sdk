// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.Common
{
    /// <summary>
    /// Defines the filter for search results from package sources.
    /// </summary>
    public interface ISearchPackFilter
    {
        /// <summary>
        /// Defines whether packages should be filtered when returning search results.
        /// </summary>
        /// <param name="candidatePackName">package name.</param>
        /// <param name="candidatePackVersion">package version.</param>
        /// <returns>true - if the package should be excluded from search results; false - if the package should be included.</returns>
        bool ShouldPackBeFiltered(string candidatePackName, string candidatePackVersion);
    }
}
