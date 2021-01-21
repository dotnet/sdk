using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackProvider : IPackProvider
    {
        private static readonly string SearchUrlFormat = "https://api-v2v3search-0.nuget.org/query?q=template&skip={0}&take={1}&prerelease={2}";
        // {PackageId}.{Version}.nupkg
        private static readonly string DownloadUrlFormat = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/{0}.{1}.nupkg";
        private static readonly string DownloadPackageFileNameFormat = "{0}.{1}.nupkg";

        private readonly string _packageTempPath;
        private readonly int _pageSize;
        private readonly bool _runOnlyOnePage;
        private readonly bool _includePreviewPacks;

        private static readonly string DownloadedPacksDir = "DownloadedPacks";

        public NugetPackProvider(string packageTempBasePath, int pageSize, bool runOnlyOnePage, bool includePreviewPacks)
        {
            _pageSize = pageSize;
            _runOnlyOnePage = runOnlyOnePage;
            _packageTempPath = Path.Combine(packageTempBasePath, DownloadedPacksDir);
            _includePreviewPacks = includePreviewPacks;

            if (Directory.Exists(_packageTempPath))
            {
                throw new Exception($"temp storage path for nuget packages already exists");
            }
            else
            {
                Directory.CreateDirectory(_packageTempPath);
            }
        }

        public async IAsyncEnumerable<IInstalledPackInfo> GetCandidatePacksAsync()
        {
            int skip = 0;
            bool done = false;
            int packCount = 0;

            do
            {
                string queryString = string.Format(SearchUrlFormat, skip, _pageSize, _includePreviewPacks);
                Uri queryUri = new Uri(queryString);
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(queryUri).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        NugetPackageSearchResult resultsForPage = JsonConvert.DeserializeObject<NugetPackageSearchResult>(responseText);

                        if (resultsForPage.Data.Count > 0)
                        {
                            skip += _pageSize;
                            packCount += resultsForPage.Data.Count;

                            foreach (NugetPackageSourceInfo sourceInfo in resultsForPage.Data)
                            {
                                string packageFilePath = await DownloadPackageAsync(sourceInfo).ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(packageFilePath))
                                {
                                    NugetPackInfo packInfo = new NugetPackInfo()
                                    {
                                        VersionedPackageIdentity = sourceInfo.VersionedPackageIdentity,
                                        Id = sourceInfo.Id,
                                        Version = sourceInfo.Version,
                                        Path = packageFilePath,
                                        TotalDownloads = sourceInfo.TotalDownloads

                                    };

                                    yield return packInfo;
                                }
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
            } while (!done && !_runOnlyOnePage);

        }

        private async Task<string> DownloadPackageAsync(NugetPackageSourceInfo packinfo)
        {
            string downloadUrl = string.Format(DownloadUrlFormat, packinfo.Id, packinfo.Version);
            string packageFileName = string.Format(DownloadPackageFileNameFormat, packinfo.Id, packinfo.Version);
            string outputPackageFileNameFullPath = Path.Combine(_packageTempPath, packageFileName);

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    byte[] packageBytes = await client.GetByteArrayAsync(downloadUrl).ConfigureAwait(false);
                    File.WriteAllBytes(outputPackageFileNameFullPath, packageBytes);
                    return outputPackageFileNameFullPath;
                }
            }
            catch
            {
                Console.WriteLine($"Failed to download package {packinfo.Id} {packinfo.Version}");
                return null;
            }
        }

        public async Task<int> GetPackageCountAsync()
        {
            string queryString = string.Format(SearchUrlFormat, 0, _pageSize, _includePreviewPacks);
            Uri queryUri = new Uri(queryString);
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(queryUri).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                NugetPackageSearchResult resultsForPage = JsonConvert.DeserializeObject<NugetPackageSearchResult>(responseText);
                return resultsForPage.TotalHits;
            }
        }

        public void DeleteDownloadedPacks()
        {
            Directory.Delete(_packageTempPath, true);
        }
    }
}
