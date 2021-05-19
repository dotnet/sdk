// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateSearch.Common
{
    // Always inherit from this, don't make it non-abstract.
    // Making this be not abstract will cause problems with the registered components.
    public abstract class NuGetMetadataSearchSource : FileMetadataSearchSource
    {
        protected static readonly string _templateDiscoveryMetadataFile = "nugetTemplateSearchInfo.json";

        private readonly ISearchInfoFileProvider _searchInfoFileProvider = new BlobStoreSourceFileProvider();

        public override string DisplayName => "NuGet.org";

        public async override Task<bool> TryConfigureAsync(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IManagedTemplatePackage> existingTemplatePackages, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string searchMetadataFileLocation = Path.Combine(environmentSettings.Paths.HostVersionSettingsDir, _templateDiscoveryMetadataFile);

            if (!await _searchInfoFileProvider.TryEnsureSearchFileAsync(environmentSettings, searchMetadataFileLocation).ConfigureAwait(false))
            {
                return false;
            }

            IFileMetadataTemplateSearchCache searchCache = CreateSearchCache(environmentSettings);
            NupkgHigherVersionInstalledPackFilter packFilter = new NupkgHigherVersionInstalledPackFilter(existingTemplatePackages);
            Configure(searchCache, packFilter);

            return true;
        }

        protected virtual IFileMetadataTemplateSearchCache CreateSearchCache(IEngineEnvironmentSettings environmentSettings)
        {
            return new FileMetadataTemplateSearchCache(environmentSettings, _templateDiscoveryMetadataFile);
        }
    }
}
