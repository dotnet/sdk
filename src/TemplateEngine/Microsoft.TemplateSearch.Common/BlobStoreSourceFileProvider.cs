using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;

namespace Microsoft.TemplateSearch.Common
{
    internal class BlobStoreSourceFileProvider : ISearchInfoFileProvider
    {
        private static readonly Uri _searchMetadataUri = new Uri("https://go.microsoft.com/fwlink/?linkid=2087906&clcid=0x409");
        private static readonly string _localSourceSearchFileOVerrideEnvVar = "DOTNET_NEW_SEARCH_FILE_OVERRIDE";

        public BlobStoreSourceFileProvider()
        {
        }

        public async Task<bool> TryEnsureSearchFileAsync(IEngineEnvironmentSettings environmentSettings, Paths paths, string metadataFileTargetLocation)
        {
            string localOverridePath = environmentSettings.Environment.GetEnvironmentVariable(_localSourceSearchFileOVerrideEnvVar);
            if (!string.IsNullOrEmpty(localOverridePath))
            {
                if (paths.FileExists(localOverridePath))
                {
                    paths.Copy(localOverridePath, metadataFileTargetLocation);
                    return true;
                }

                return false;
            }

            bool cloudResult = await TryAcquireFileFromCloudAsync(paths, metadataFileTargetLocation);

            if (cloudResult)
            {
                return true;
            }

            // A previously acquired file may already be setup.
            // It could either be from online storage, or shipped in-box.
            // If so, fallback to using it.
            if (paths.FileExists(metadataFileTargetLocation))
            {
                return true;
            }

            // use the in-box shipped file. It's probably very stale, but better than nothing.
            if (paths.FileExists(paths.User.NuGetScrapedTemplateSearchFile))
            {
                paths.Copy(paths.User.NuGetScrapedTemplateSearchFile, metadataFileTargetLocation);
                return true;
            }

            return false;
        }

        // Attempt to get the search metadata file from cloud storage and place it in the expected search location.
        // Return true on success, false on failure.
        private async Task<bool> TryAcquireFileFromCloudAsync(Paths paths, string searchMetadataFileLocation)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = client.GetAsync(_searchMetadataUri).Result)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string resultText = await response.Content.ReadAsStringAsync();
                        paths.WriteAllText(searchMetadataFileLocation, resultText);

                        return true;
                    }

                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
