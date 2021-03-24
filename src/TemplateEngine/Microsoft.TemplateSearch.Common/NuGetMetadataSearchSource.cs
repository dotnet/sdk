using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackages;
using Microsoft.TemplateEngine.Edge;

namespace Microsoft.TemplateSearch.Common
{
    // Always inherit from this, don't make it non-abstract.
    // Making this be not abstract will cause problems with the registered components.
    public abstract class NuGetMetadataSearchSource : FileMetadataSearchSource
    {
        protected static readonly string _templateDiscoveryMetadataFile = "nugetTemplateSearchInfo.json";

        private readonly ISearchInfoFileProvider _searchInfoFileProvider;

        public NuGetMetadataSearchSource()
        {
            _searchInfoFileProvider = new BlobStoreSourceFileProvider();
        }

        public override string DisplayName => "NuGet.org";

        public async override Task<bool> TryConfigure(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IManagedTemplatePackage> existingTemplatePackages)
        {
            Paths paths = new Paths(environmentSettings);
            string searchMetadataFileLocation = Path.Combine(paths.User.BaseDir, _templateDiscoveryMetadataFile);

            if (!await _searchInfoFileProvider.TryEnsureSearchFileAsync(environmentSettings, paths, searchMetadataFileLocation))
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
