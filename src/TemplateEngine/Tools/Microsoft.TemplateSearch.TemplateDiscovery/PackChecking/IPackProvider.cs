// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    internal interface IPackProvider
    {
        string Name { get; }

        IAsyncEnumerable<ITemplatePackageInfo> GetCandidatePacksAsync(CancellationToken token);

        Task<IDownloadedPackInfo> DownloadPackageAsync(ITemplatePackageInfo packinfo, CancellationToken token);

        Task<int> GetPackageCountAsync(CancellationToken token);

        Task DeleteDownloadedPacksAsync();

        Task<(ITemplatePackageInfo PackageInfo, bool Removed)> GetPackageInfoAsync(string packageIdentifier, CancellationToken cancellationToken);

    }
}
