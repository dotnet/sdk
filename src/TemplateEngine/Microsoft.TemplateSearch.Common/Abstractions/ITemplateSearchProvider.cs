// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common.Abstractions
{
    /// <summary>
    /// The template search provider searches for the templates at the source.
    /// </summary>
    public interface ITemplateSearchProvider
    {
        /// <summary>
        /// Gets <see cref="ITemplateSearchProviderFactory"/> that created the provider.
        /// </summary>
        ITemplateSearchProviderFactory Factory { get; }

        /// <summary>
        /// Searches the source for the available templates that matches the filters.
        /// </summary>
        /// <param name="packFilters">The filter that defines if <see cref="TemplatePackageSearchData"/> is a match.</param>
        /// <param name="matchingTemplatesFilter">The filter that list of templates that are the match inside given <see cref="TemplatePackageSearchData"/>.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The list of packages and matching templates for the source.</returns>
        Task<IReadOnlyList<(IPackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)>> SearchForTemplatePackagesAsync(
            Func<TemplatePackageSearchData, bool> packFilters,
            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> matchingTemplatesFilter,
            CancellationToken cancellationToken);
    }
}
