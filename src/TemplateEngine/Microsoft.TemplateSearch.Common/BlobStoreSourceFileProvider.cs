// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    internal class BlobStoreSourceFileProvider
    {
        private const int CachedFileValidityInHours = 1;
        private const string ETagFileSuffix = ".etag";
        private const string ETagHeaderName = "ETag";
        private const string IfNoneMatchHeaderName = "If-None-Match";
        private const string _localSourceSearchFileOverrideEnvVar = "DOTNET_NEW_SEARCH_FILE_OVERRIDE";
        private const string _useLocalSearchFileIfPresentEnvVar = "DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY";
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Uri[] _searchMetadataUris =
        {
            new Uri("https://go.microsoft.com/fwlink/?linkid=2087906&clcid=0x409"),         //v1
            //link TBD                                                                      //v2
        };

        internal BlobStoreSourceFileProvider(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
        }

        internal async Task<string> GetSearchFileAsync(string preferredMetadataLocation, CancellationToken cancellationToken)
        {
            string? localOverridePath = _environmentSettings.Environment.GetEnvironmentVariable(_localSourceSearchFileOverrideEnvVar);
            if (!string.IsNullOrEmpty(localOverridePath))
            {
                if (_environmentSettings.Host.FileSystem.FileExists(localOverridePath!))
                {
                    return localOverridePath!;
                }
                throw new Exception($"Local search cache {localOverridePath} does not exist.");
            }

            string? useLocalSearchFile = _environmentSettings.Environment.GetEnvironmentVariable(_useLocalSearchFileIfPresentEnvVar);
            if (!string.IsNullOrEmpty(useLocalSearchFile))
            {
                // evn var is set, only use a local copy of the search file. Don't try to acquire one from blob storage.
                if (_environmentSettings.Host.FileSystem.FileExists(preferredMetadataLocation))
                {
                    return preferredMetadataLocation;
                }
                else
                {
                    throw new Exception($"Local search cache {preferredMetadataLocation} does not exist.");
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
        /// <returns></returns>
        private async Task AcquireFileFromCloudAsync(string searchMetadataFileLocation, CancellationToken cancellationToken)
        {
            foreach (Uri searchMetadataUri in _searchMetadataUris)
            {
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
                }
            }
            if (_environmentSettings.Host.FileSystem.FileExists(searchMetadataFileLocation))
            {
                _environmentSettings.Host.Logger.LogWarning(
                    "Failed to update search cache file from locations {0}. The previously downloaded search cache will be used instead.",
                    string.Join(", ", _searchMetadataUris.Select(uri => uri.ToString())));
            }
            else
            {
                throw new Exception($"Failed to update search cache file from locations {string.Join(", ", _searchMetadataUris.Select(uri => uri.ToString()))}");
            }
        }
    }
}
