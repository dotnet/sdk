// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Edge;

namespace Microsoft.TemplateSearch.Common
{
    internal class BlobStoreSourceFileProvider : ISearchInfoFileProvider
    {
        private const int CachedFileValidityInHours = 1;
        private const string ETagFileSuffix = ".etag";
        private const string ETagHeaderName = "ETag";
        private const string IfNoneMatchHeaderName = "If-None-Match";
        private static readonly Uri _searchMetadataUri = new Uri("https://go.microsoft.com/fwlink/?linkid=2087906&clcid=0x409");
        private static readonly string _localSourceSearchFileOverrideEnvVar = "DOTNET_NEW_SEARCH_FILE_OVERRIDE";
        private static readonly string _useLocalSearchFileIfPresentEnvVar = "DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY";

        public BlobStoreSourceFileProvider()
        {
        }

        public async Task<bool> TryEnsureSearchFileAsync(IEngineEnvironmentSettings environmentSettings, Paths paths, string metadataFileTargetLocation)
        {
            string localOverridePath = environmentSettings.Environment.GetEnvironmentVariable(_localSourceSearchFileOverrideEnvVar);
            if (!string.IsNullOrEmpty(localOverridePath))
            {
                if (paths.FileExists(localOverridePath))
                {
                    paths.Copy(localOverridePath, metadataFileTargetLocation);
                    return true;
                }

                return false;
            }

            string useLocalSearchFile = environmentSettings.Environment.GetEnvironmentVariable(_useLocalSearchFileIfPresentEnvVar);
            if (!string.IsNullOrEmpty(useLocalSearchFile))
            {
                // evn var is set, only use a local copy of the search file. Don't try to acquire one from blob storage.
                return TryUseLocalSearchFile(paths, metadataFileTargetLocation);
            }
            else
            {
                // prefer a search file from cloud storage.
                // only download the file if it's been long enough since the last time it was downloaded.
                if (ShouldDownloadFileFromCloud(environmentSettings, metadataFileTargetLocation))
                {
                    bool cloudResult = await TryAcquireFileFromCloudAsync(environmentSettings, paths, metadataFileTargetLocation);

                    if (cloudResult)
                    {
                        return true;
                    }
                }
                else
                {
                    // the file exists and is new enough to not download again.
                    return true;
                }

                // no cloud store file was available. Use a local file if possible.
                return TryUseLocalSearchFile(paths, metadataFileTargetLocation);
            }
        }

        private bool ShouldDownloadFileFromCloud(IEngineEnvironmentSettings environmentSettings, string metadataFileTargetLocation)
        {
            IPhysicalFileSystem fileSystem = environmentSettings.Host.FileSystem;
            if(fileSystem is IFileLastWriteTimeSource lastWriteTimeSource)
            {
                if (fileSystem.FileExists(metadataFileTargetLocation))
                {
                    DateTime utcNow = DateTime.UtcNow;
                    DateTime lastWriteTimeUtc = lastWriteTimeSource.GetLastWriteTimeUtc(metadataFileTargetLocation);
                    if(lastWriteTimeUtc.AddHours(CachedFileValidityInHours) > utcNow)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool TryUseLocalSearchFile(Paths paths, string metadataFileTargetLocation)
        {
            // A previously acquired file may already be setup.
            // It could either be from online storage, or shipped in-box.
            // If so, fallback to using it.
            if (paths.FileExists(metadataFileTargetLocation))
            {
                return true;
            }
            return false;
        }

        // Attempt to get the search metadata file from cloud storage and place it in the expected search location.
        // Return true on success, false on failure.
        // Implement If-None-Match/ETag headers to avoid re-downloading the same content over and over again.
        private async Task<bool> TryAcquireFileFromCloudAsync(IEngineEnvironmentSettings environmentSettings, Paths paths, string searchMetadataFileLocation)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string etagFileLocation = searchMetadataFileLocation + ETagFileSuffix;
                    if(paths.FileExists(etagFileLocation))
                    {
                        string etagValue = paths.ReadAllText(etagFileLocation);
                        client.DefaultRequestHeaders.Add(IfNoneMatchHeaderName, $"\"{etagValue}\"");
                    }
                    using (HttpResponseMessage response = client.GetAsync(_searchMetadataUri).Result)
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            string resultText = await response.Content.ReadAsStringAsync();
                            paths.WriteAllText(searchMetadataFileLocation, resultText);

                            IEnumerable<string> etagValues;
                            if(response.Headers.TryGetValues(ETagHeaderName, out etagValues))
                            {
                                if(etagValues.Count() == 1)
                                {
                                    paths.WriteAllText(etagFileLocation, etagValues.First());
                                }
                            }

                            return true;
                        }
                        else if(response.StatusCode == HttpStatusCode.NotModified)
                        {
                            IPhysicalFileSystem fileSystem = environmentSettings.Host.FileSystem;
                            if (fileSystem is IFileLastWriteTimeSource lastWriteTimeSource)
                            {
                                lastWriteTimeSource.SetLastWriteTimeUtc(searchMetadataFileLocation, DateTime.UtcNow);
                            }
                            return true;
                        }

                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
