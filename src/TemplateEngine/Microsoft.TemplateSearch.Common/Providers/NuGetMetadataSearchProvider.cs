// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.Common.Providers
{
    internal class NuGetMetadataSearchProvider : ITemplateSearchProvider
    {
        private const string TemplateDiscoveryMetadataFile = "nugetTemplateSearchInfo.json";
        private readonly BlobStoreSourceFileProvider _searchInfoFileProvider;
        private readonly IReadOnlyDictionary<string, Func<object, object>> _additionalDataReaders = new Dictionary<string, Func<object, object>>();
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private TemplateSearchCache? _searchCache;

        internal NuGetMetadataSearchProvider(
            ITemplateSearchProviderFactory factory,
            IEngineEnvironmentSettings environmentSettings,
            IReadOnlyDictionary<string, Func<object, object>> additionalDataReaders)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _environmentSettings = environmentSettings ?? throw new ArgumentNullException(nameof(environmentSettings));
            _additionalDataReaders = additionalDataReaders ?? throw new ArgumentNullException(nameof(additionalDataReaders));
            _searchInfoFileProvider = new BlobStoreSourceFileProvider(_environmentSettings);
        }

        public ITemplateSearchProviderFactory Factory { get; }

        public async Task<IReadOnlyList<(ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)>> SearchForTemplatePackagesAsync(
            Func<TemplatePackageSearchData, bool> packFilter,
            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> matchingTemplatesFilter,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_searchCache == null)
            {
                _searchCache = await ConfigureAsync(cancellationToken).ConfigureAwait(false);
            }

            IEnumerable<TemplatePackageSearchData> filteredPackages = _searchCache.TemplatePackages.Where(package => packFilter(package));

            return filteredPackages
                .Select<TemplatePackageSearchData, (ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)>(package => (package, matchingTemplatesFilter(package)))
                .Where(result => result.MatchedTemplates.Any())
                .ToList();
        }

        private async Task<TemplateSearchCache> ConfigureAsync(CancellationToken cancellationToken)
        {
            string searchMetadataFileLocation = Path.Combine(_environmentSettings.Paths.HostVersionSettingsDir, TemplateDiscoveryMetadataFile);
            string metadataLocation = await _searchInfoFileProvider.GetSearchFileAsync(searchMetadataFileLocation, cancellationToken).ConfigureAwait(false);
            return TemplateSearchCache.FromJObject(_environmentSettings.Host.FileSystem.ReadObject(metadataLocation), _environmentSettings.Host.Logger, _additionalDataReaders);
        }
    }
}
