// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.Common.Providers
{
    internal class NuGetMetadataSearchProvider : ITemplateSearchProvider
    {
        private const string TemplateDiscoveryMetadataFile = "nugetTemplateSearchInfo.json";
        private const int CachedFileValidityInHours = 1;
        private const string ETagFileSuffix = ".etag";
        private const string ETagHeaderName = "ETag";
        private const string IfNoneMatchHeaderName = "If-None-Match";
        private const string LocalSourceSearchFileOverrideEnvVar = "DOTNET_NEW_SEARCH_FILE_OVERRIDE";
        private const string UseLocalSearchFileIfPresentEnvVar = "DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY";

        private readonly IReadOnlyDictionary<string, Func<object, object>> _additionalDataReaders = new Dictionary<string, Func<object, object>>();
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Uri[] _searchMetadataUris =
        {
            new Uri("https://go.microsoft.com/fwlink/?linkid=2087906&clcid=0x409"),         //v1
            //link TBD                                                                      //v2
        };

        private TemplateSearchCache? _searchCache;

        internal NuGetMetadataSearchProvider(
            ITemplateSearchProviderFactory factory,
            IEngineEnvironmentSettings environmentSettings,
            IReadOnlyDictionary<string, Func<object, object>> additionalDataReaders)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _environmentSettings = environmentSettings ?? throw new ArgumentNullException(nameof(environmentSettings));
            _additionalDataReaders = additionalDataReaders ?? throw new ArgumentNullException(nameof(additionalDataReaders));
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
                string metadataLocation = await GetSearchFileAsync(cancellationToken).ConfigureAwait(false);
                _searchCache = TemplateSearchCache.FromJObject(_environmentSettings.Host.FileSystem.ReadObject(metadataLocation), _environmentSettings.Host.Logger, _additionalDataReaders);
            }

            IEnumerable<TemplatePackageSearchData> filteredPackages = _searchCache.TemplatePackages.Where(package => packFilter(package));

            return filteredPackages
                .Select<TemplatePackageSearchData, (ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)>(package => (package, matchingTemplatesFilter(package)))
                .Where(result => result.MatchedTemplates.Any())
                .ToList();
        }

        internal async Task<string> GetSearchFileAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? localOverridePath = _environmentSettings.Environment.GetEnvironmentVariable(LocalSourceSearchFileOverrideEnvVar);
            if (!string.IsNullOrEmpty(localOverridePath))
            {
                if (_environmentSettings.Host.FileSystem.FileExists(localOverridePath!))
                {
                    return localOverridePath!;
                }
                throw new Exception(string.Format(LocalizableStrings.BlobStoreSourceFileProvider_Exception_LocalCacheDoesNotExist, localOverridePath));
            }

            string preferredMetadataLocation = Path.Combine(_environmentSettings.Paths.HostVersionSettingsDir, TemplateDiscoveryMetadataFile);
            string? useLocalSearchFile = _environmentSettings.Environment.GetEnvironmentVariable(UseLocalSearchFileIfPresentEnvVar);
            if (!string.IsNullOrEmpty(useLocalSearchFile))
            {
                // evn var is set, only use a local copy of the search file. Don't try to acquire one from blob storage.
                if (_environmentSettings.Host.FileSystem.FileExists(preferredMetadataLocation))
                {
                    return preferredMetadataLocation;
                }
                else
                {
                    throw new Exception(string.Format(LocalizableStrings.BlobStoreSourceFileProvider_Exception_LocalCacheDoesNotExist, preferredMetadataLocation));
                }
            }
            else
            {
                // prefer a search file from cloud storage.
                // only download the file if it's been long enough since the last time it was downloaded.
                if (ShouldDownloadFileFromCloud(preferredMetadataLocation))
                {
                    await AcquireFileFromCloudAsync(preferredMetadataLocation, cancellationToken).ConfigureAwait(false);
                }
                return preferredMetadataLocation;
            }
        }

        private bool ShouldDownloadFileFromCloud(string metadataFileTargetLocation)
        {
            if (_environmentSettings.Host.FileSystem.FileExists(metadataFileTargetLocation))
            {
                DateTime utcNow = DateTime.UtcNow;
                DateTime lastWriteTimeUtc = _environmentSettings.Host.FileSystem.GetLastWriteTimeUtc(metadataFileTargetLocation);
                if (lastWriteTimeUtc.AddHours(CachedFileValidityInHours) > utcNow)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Attempt to get the search metadata file from cloud storage and place it in the expected search location.
        /// Return true on success, false on failure.
        /// Implement If-None-Match/ETag headers to avoid re-downloading the same content over and over again.
        /// </summary>
        /// <param name="searchMetadataFileLocation"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task AcquireFileFromCloudAsync(string searchMetadataFileLocation, CancellationToken cancellationToken)
        {
            List<Exception> exceptionsOccurred = new List<Exception>();
            foreach (Uri searchMetadataUri in _searchMetadataUris)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        string etagFileLocation = searchMetadataFileLocation + ETagFileSuffix;
                        if (_environmentSettings.Host.FileSystem.FileExists(etagFileLocation))
                        {
                            string etagValue = _environmentSettings.Host.FileSystem.ReadAllText(etagFileLocation);
                            client.DefaultRequestHeaders.Add(IfNoneMatchHeaderName, $"\"{etagValue}\"");
                        }
                        using (HttpResponseMessage response = await client.GetAsync(searchMetadataUri, cancellationToken).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                string resultText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                _environmentSettings.Host.FileSystem.WriteAllText(searchMetadataFileLocation, resultText);

                                IEnumerable<string> etagValues;
                                if (response.Headers.TryGetValues(ETagHeaderName, out etagValues))
                                {
                                    if (etagValues.Count() == 1)
                                    {
                                        _environmentSettings.Host.FileSystem.WriteAllText(etagFileLocation, etagValues.First());
                                    }
                                }
                                return;
                            }
                            else if (response.StatusCode == HttpStatusCode.NotModified)
                            {
                                _environmentSettings.Host.FileSystem.SetLastWriteTimeUtc(searchMetadataFileLocation, DateTime.UtcNow);
                                return;
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    _environmentSettings.Host.Logger.LogDebug("Failed to download {0}, details: {1}", searchMetadataUri, e);
                    exceptionsOccurred.Add(e);
                }
            }

            if (_environmentSettings.Host.FileSystem.FileExists(searchMetadataFileLocation))
            {
                _environmentSettings.Host.Logger.LogWarning(LocalizableStrings.BlobStoreSourceFileProvider_Warning_LocalCacheWillBeUsed);
            }
            else
            {
                throw new AggregateException(LocalizableStrings.BlobStoreSourceFileProvider_Exception_FailedToUpdateCache, exceptionsOccurred);
            }
        }
    }
}
