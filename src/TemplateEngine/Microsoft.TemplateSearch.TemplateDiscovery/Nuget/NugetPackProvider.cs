// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    internal class NugetPackProvider : IPackProvider
    {
        // {PackageId}.{Version}.nupkg
        private const string DownloadUrlFormat = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg";

        private const string DownloadPackageFileNameFormat = "{0}.{1}.nupkg";
        private const string DownloadedPacksDir = "DownloadedPacks";
        private readonly string _packageTempPath;
        private readonly int _pageSize;
        private readonly bool _runOnlyOnePage;
        private string _searchUriFormat;

        internal NugetPackProvider(string name, string query, string packageTempBasePath, int pageSize, bool runOnlyOnePage, bool includePreviewPacks)
        {
            Name = name;
            _pageSize = pageSize;
            _runOnlyOnePage = runOnlyOnePage;
            _packageTempPath = Path.GetFullPath(Path.Combine(packageTempBasePath, DownloadedPacksDir, Name));
            _searchUriFormat = $"https://api-v2v3search-0.nuget.org/query?{query}&skip={{0}}&take={{1}}&prerelease={includePreviewPacks}";

            if (Directory.Exists(_packageTempPath))
            {
                throw new Exception($"temp storage path for nuget packages already exists");
            }
            else
            {
                Directory.CreateDirectory(_packageTempPath);
            }
        }

        public string Name { get; private set; }

        public async IAsyncEnumerable<IPackInfo> GetCandidatePacksAsync([EnumeratorCancellation] CancellationToken token)
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

                        NugetPackageSearchResult resultsForPage = NugetPackageSearchResult.FromJObject(JObject.Parse(responseText));

                        if (resultsForPage.Data.Count > 0)
                        {
                            skip += _pageSize;
                            packCount += resultsForPage.Data.Count;
                            foreach (NugetPackageSourceInfo sourceInfo in resultsForPage.Data)
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

        public async Task<IDownloadedPackInfo?> DownloadPackageAsync(IPackInfo packinfo, CancellationToken token)
        {
            string downloadUrl = string.Format(DownloadUrlFormat, packinfo.Id, packinfo.Version);
            string packageFileName = string.Format(DownloadPackageFileNameFormat, packinfo.Id, packinfo.Version);
            string outputPackageFileNameFullPath = Path.Combine(_packageTempPath, packageFileName);

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    byte[] packageBytes = await client.GetByteArrayAsync(downloadUrl, token).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(outputPackageFileNameFullPath, packageBytes, token).ConfigureAwait(false);
                    return new NugetPackInfo(packinfo, outputPackageFileNameFullPath);
                }
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to download package {packinfo.Id} {packinfo.Version}, reason: {e.ToString()}.");
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
                NugetPackageSearchResult resultsForPage = NugetPackageSearchResult.FromJObject(JObject.Parse(responseText));
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
