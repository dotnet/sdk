// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
        private readonly ILogger _logger;
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Uri[] _searchMetadataUris =
        {
            new Uri("https://go.microsoft.com/fwlink/?linkid=2087906&clcid=0x409"), //v1 search cache
            new Uri("https://go.microsoft.com/fwlink/?linkid=2168770&clcid=0x409") //v2 search cache
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
            _logger = _environmentSettings.Host.LoggerFactory.CreateLogger<NuGetMetadataSearchProvider>();
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
                _logger.LogDebug("Initializing search cache...");
                string metadataLocation = await GetSearchFileAsync(cancellationToken).ConfigureAwait(false);
                _searchCache = TemplateSearchCache.FromJObject(_environmentSettings.Host.FileSystem.ReadObject(metadataLocation), _logger, _additionalDataReaders);
                _logger.LogDebug("Search cache was successfully setup.");
            }

            IEnumerable<TemplatePackageSearchData> filteredPackages = _searchCache.TemplatePackages.Where(package => packFilter(package));
            _logger.LogDebug("Retrieved {0} packages matching package search criteria.", filteredPackages.Count());

            List<(ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)> matchingTemplates = filteredPackages
                .Select<TemplatePackageSearchData, (ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)>(package => (package, matchingTemplatesFilter(package)))
                .Where(result => result.MatchedTemplates.Any())
                .ToList();

            _logger.LogDebug("Retrieved {0} packages matching template search criteria.", matchingTemplates.Count);
            return matchingTemplates;
        }

        internal async Task<string> GetSearchFileAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? localOverridePath = _environmentSettings.Environment.GetEnvironmentVariable(LocalSourceSearchFileOverrideEnvVar);
            if (!string.IsNullOrEmpty(localOverridePath))
            {
                _logger.LogDebug("{0} is set to {1}, the search file will be loaded from this location instead.", LocalSourceSearchFileOverrideEnvVar, localOverridePath);
                if (_environmentSettings.Host.FileSystem.FileExists(localOverridePath!))
                {
                    return localOverridePath!;
                }
                _logger.LogDebug("Failed to load search cache from defined location: file {0} does not exist.", localOverridePath);
                throw new Exception(string.Format(LocalizableStrings.BlobStoreSourceFileProvider_Exception_LocalCacheDoesNotExist, localOverridePath));
            }

            string preferredMetadataLocation = Path.Combine(_environmentSettings.Paths.HostVersionSettingsDir, TemplateDiscoveryMetadataFile);
            _logger.LogDebug("Search cache file location: {0}.", preferredMetadataLocation);
            string? useLocalSearchFile = _environmentSettings.Environment.GetEnvironmentVariable(UseLocalSearchFileIfPresentEnvVar);
            if (!string.IsNullOrEmpty(useLocalSearchFile))
            {
                _logger.LogDebug("{0} is set to {1}, downloading of the search cache will be skipped.", UseLocalSearchFileIfPresentEnvVar, useLocalSearchFile);
                // evn var is set, only use a local copy of the search file. Don't try to acquire one from blob storage.
                if (_environmentSettings.Host.FileSystem.FileExists(preferredMetadataLocation))
                {
                    return preferredMetadataLocation;
                }
                else
                {
                    _logger.LogDebug("Failed to load existing search cache: file {0} does not exist.", preferredMetadataLocation);
                    throw new Exception(string.Format(LocalizableStrings.BlobStoreSourceFileProvider_Exception_LocalCacheDoesNotExist, preferredMetadataLocation));
                }
            }
            else
            {
                _logger.LogDebug("Updating the search cache...");
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
            _logger.LogDebug("Checking the age of search cache...");
            if (_environmentSettings.Host.FileSystem.FileExists(metadataFileTargetLocation))
            {
                DateTime utcNow = DateTime.UtcNow;
                DateTime lastWriteTimeUtc = _environmentSettings.Host.FileSystem.GetLastWriteTimeUtc(metadataFileTargetLocation);
                _logger.LogDebug("The search cache was updated on {0}", lastWriteTimeUtc);
                if (lastWriteTimeUtc.AddHours(CachedFileValidityInHours) > utcNow)
                {
                    _logger.LogDebug("The search cache was updated less than {0} hours ago, the update will be skipped.", CachedFileValidityInHours);
                    return false;
                }
                _logger.LogDebug("The search cache was updated more than {0} hours ago, and needs to be updated.", CachedFileValidityInHours);
                return true;
            }
            _logger.LogDebug("The search cache file {0} doesn't exist, and needs to be created.", metadataFileTargetLocation);
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
                _logger.LogDebug("Retrieving cache file from {0} ...", searchMetadataUri);
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
                        client.DefaultRequestHeaders.Add(IfNoneMatchHeaderName, "");
                        using (HttpResponseMessage response = await client.GetAsync(searchMetadataUri, cancellationToken).ConfigureAwait(false))
                        {
                            _logger.LogDebug(GetResponseDetails(response));
                            if (response.IsSuccessStatusCode)
                            {
                                string resultText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                _environmentSettings.Host.FileSystem.WriteAllText(searchMetadataFileLocation, resultText);
                                _logger.LogDebug("Search cache file was successfully downloaded to {0}.", searchMetadataFileLocation);
                                IEnumerable<string> etagValues;
                                if (response.Headers.TryGetValues(ETagHeaderName, out etagValues))
                                {
                                    if (etagValues.Count() == 1)
                                    {
                                        _environmentSettings.Host.FileSystem.WriteAllText(etagFileLocation, etagValues.First());
                                    }
                                    _logger.LogDebug("ETag {0} was written to {1}.", etagValues.First(), etagFileLocation);
                                }
                                return;
                            }
                            else if (response.StatusCode == HttpStatusCode.NotModified)
                            {
                                _logger.LogDebug("Search cache file is not modified, updating the last modified date to now.");
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
                    _logger.LogDebug("Failed to download {0}, details: {1}", searchMetadataUri, e);
                    exceptionsOccurred.Add(e);
                }
            }

            if (_environmentSettings.Host.FileSystem.FileExists(searchMetadataFileLocation))
            {
                _logger.LogDebug("Failed to update search cache, {0} will be used instead.", searchMetadataFileLocation);
                _environmentSettings.Host.Logger.LogWarning(LocalizableStrings.BlobStoreSourceFileProvider_Warning_LocalCacheWillBeUsed);
            }
            else
            {
                _logger.LogDebug("Failed to update search cache from all known locations.");
                throw new AggregateException(LocalizableStrings.BlobStoreSourceFileProvider_Exception_FailedToUpdateCache, exceptionsOccurred);
            }
        }

        private string GetResponseDetails(HttpResponseMessage response)
        {
            StringBuilder message = new StringBuilder();

            message.AppendLine($"Status code: {response.StatusCode}");
            message.AppendLine("Headers:");
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
            {
                message.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
            {
                message.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
            return message.ToString();
        }
    }
}
