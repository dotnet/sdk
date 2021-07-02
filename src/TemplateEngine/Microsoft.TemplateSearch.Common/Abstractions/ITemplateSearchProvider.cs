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
    public interface ITemplateSearchProvider
    {
        ITemplateSearchProviderFactory Factory { get; }

        Task<IReadOnlyList<(IPackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)>> SearchForTemplatePackagesAsync(
            Func<TemplatePackageSearchData, bool> packFilters,
            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> matchingTemplatesFilter,
            CancellationToken cancellationToken);
    }
}
