// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.TemplateSearch.TemplateDiscovery.NuGet
{
    internal class NuGetPackProvider : IPackProvider
    {
        private const string NuGetOrgFeed = "https://api.nuget.org/v3/index.json";
        private const string DownloadPackageFileNameFormat = "{0}.{1}.nupkg";
        private const string DownloadedPacksDir = "DownloadedPacks";
        private readonly string _packageTempPath;
        private readonly int _pageSize;
        private readonly bool _runOnlyOnePage;
        private readonly SourceRepository _repository;
        private readonly SourceCacheContext _cacheContext = new SourceCacheContext();
        private readonly FindPackageByIdResource _downloadResource;
        private string _searchUriFormat;

        internal NuGetPackProvider(string name, string query, DirectoryInfo packageTempBasePath, int pageSize, bool runOnlyOnePage, bool includePreviewPacks)
        {
            Name = name;
            _pageSize = pageSize;
            _runOnlyOnePage = runOnlyOnePage;
            _packageTempPath = Path.GetFullPath(Path.Combine(packageTempBasePath.FullName, DownloadedPacksDir, Name));
            _repository = Repository.Factory.GetCoreV3(NuGetOrgFeed);
            ServiceIndexResourceV3 indexResource = _repository.GetResource<ServiceIndexResourceV3>();
            IReadOnlyList<ServiceIndexEntry> searchResources = indexResource.GetServiceEntries("SearchQueryService");
            _downloadResource = _repository.GetResource<FindPackageByIdResource>();

            if (!searchResources.Any())
            {
                throw new Exception($"{NuGetOrgFeed} does not support search API (SearchQueryService)");
            }

            _searchUriFormat = $"{searchResources[0].Uri}?{query}&skip={{0}}&take={{1}}&prerelease={includePreviewPacks}";

            if (Directory.Exists(_packageTempPath))
            {
                throw new Exception($"temp storage path for NuGet packages already exists: {_packageTempPath}");
            }
            else
            {
                Directory.CreateDirectory(_packageTempPath);
            }
        }

        public string Name { get; private set; }

        public async IAsyncEnumerable<ITemplatePackageInfo> GetCandidatePacksAsync([EnumeratorCancellation] CancellationToken token)
        {
            int skip = 0;
            bool done = false;
            int packCount = 0;

            do
            {
                string queryString = string.Format(_searchUriFormat, skip, _pageSize);
                Uri queryUri = new Uri(queryString);
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(queryUri, token).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

                        NuGetPackageSearchResult resultsForPage = NuGetPackageSearchResult.FromJObject(JObject.Parse(responseText));

                        if (resultsForPage.Data.Count > 0)
                        {
                            skip += _pageSize;
                            packCount += resultsForPage.Data.Count;
                            foreach (NuGetPackageSourceInfo sourceInfo in resultsForPage.Data)
                            {
                                yield return sourceInfo;
                            }
                        }
                        else
                        {
                            done = true;
                        }
                    }
                    else
                    {
                        done = true;
                    }
                }
            }
            while (!done && !_runOnlyOnePage);
        }

        public async Task<IDownloadedPackInfo?> DownloadPackageAsync(ITemplatePackageInfo packinfo, CancellationToken token)
        {
            string packageFileName = string.Format(DownloadPackageFileNameFormat, packinfo.Name, packinfo.Version);
            string outputPackageFileNameFullPath = Path.Combine(_packageTempPath, packageFileName);

            try
            {
                using Stream packageStream = File.Create(outputPackageFileNameFullPath);
                if (await _downloadResource.CopyNupkgToStreamAsync(
                    packinfo.Name,
                    new NuGetVersion(packinfo.Version),
                    packageStream,
                    _cacheContext,
                    NullLogger.Instance,
                    token).ConfigureAwait(false))
                {
                    return new NuGetPackInfo(packinfo, outputPackageFileNameFullPath);
                }
                else
                {
                    throw new Exception($"Download failed: {nameof(_downloadResource.CopyNupkgToStreamAsync)} returned false.");
                }
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to download package {packinfo.Name} {packinfo.Version}, reason: {e}.");
                return null;
            }
        }

        public async Task<int> GetPackageCountAsync(CancellationToken token)
        {
            string queryString = string.Format(_searchUriFormat, 0, _pageSize);
            Uri queryUri = new Uri(queryString);
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(queryUri, token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                string responseText = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                NuGetPackageSearchResult resultsForPage = NuGetPackageSearchResult.FromJObject(JObject.Parse(responseText));
                return resultsForPage.TotalHits;
            }
        }

        public async Task DeleteDownloadedPacksAsync()
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Directory.Delete(_packageTempPath, true);
                    return;
                }
                catch (IOException)
                {
                    Console.WriteLine($"Failed to remove {_packageTempPath}, retrying in 1 sec");
                }
                await Task.Delay(1000).ConfigureAwait(false);
            }
            Console.WriteLine($"Failed to remove {_packageTempPath}, remove it manually.");
        }

    }
}
