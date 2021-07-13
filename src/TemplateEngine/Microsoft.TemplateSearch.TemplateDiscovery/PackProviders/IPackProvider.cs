// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    internal interface IPackProvider
    {
        string Name { get; }

        IAsyncEnumerable<IPackInfo> GetCandidatePacksAsync(CancellationToken token);

        Task<IDownloadedPackInfo?> DownloadPackageAsync(IPackInfo packinfo, CancellationToken token);

        Task<int> GetPackageCountAsync(CancellationToken token);

        Task DeleteDownloadedPacksAsync();
    }
}
