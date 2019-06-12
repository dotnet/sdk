using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateSearch.Common
{
    public abstract class FileMetadataSearchSource : ITemplateSearchSource
    {
        protected IFileMetadataTemplateSearchCache _searchCache;
        private ISearchPackFilter _packFilter;

        public abstract Task<bool> TryConfigure(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IInstallUnitDescriptor> existingInstallDescriptors);

        protected void Configure(IFileMetadataTemplateSearchCache searchCache, ISearchPackFilter packFilter)
        {
            _searchCache = searchCache;
            _packFilter = packFilter;
        }

        public abstract string DisplayName { get; }

        public Guid Id => new Guid("6EA368C4-8A56-444C-91D1-55150B296BF2");

        public Task<IReadOnlyList<ITemplateNameSearchResult>> CheckForTemplateNameMatchesAsync(string searchName)
        {
            if (_searchCache == null)
            {
                throw new Exception("Search Source is not configured");
            }

            IReadOnlyList<ITemplateInfo> templateMatches = _searchCache.GetNameMatchedTemplates(searchName);
            IReadOnlyList<string> templateIdentities = templateMatches.Select(t => t.Identity).ToList();
            IReadOnlyDictionary<string, PackInfo> templateToPackMap = _searchCache.GetTemplateToPackMapForTemplateIdentities(templateIdentities);

            List<ITemplateNameSearchResult> resultList = new List<ITemplateNameSearchResult>();

            foreach (ITemplateInfo candidateTemplateInfo in templateMatches)
            {
                if (!templateToPackMap.TryGetValue(candidateTemplateInfo.Identity, out PackInfo candidatePackInfo))
                {
                    // The pack was not found for the template. This can't realistically occur.
                    // The continue safeguards against the possibility that somehow the map got messed up.
                    continue;
                }

                if (_packFilter.ShouldPackBeFiltered(candidatePackInfo.Name, candidatePackInfo.Version))
                {
                    // something already installed says this should be filtered.
                    continue;
                }

                ITemplateNameSearchResult result = CreateNameSearchResult(candidateTemplateInfo, candidatePackInfo);

                resultList.Add(result);
            }

            return Task.FromResult((IReadOnlyList<ITemplateNameSearchResult>)resultList);
        }

        protected virtual TemplateNameSearchResult CreateNameSearchResult(ITemplateInfo candidateTemplateInfo, PackInfo candidatePackInfo)
        {
            return new TemplateNameSearchResult(candidateTemplateInfo, candidatePackInfo);
        }

        public Task<IReadOnlyDictionary<string, PackToTemplateEntry>> CheckForTemplatePackMatchesAsync(IReadOnlyList<string> packNameList)
        {
            if (_searchCache == null)
            {
                throw new Exception("Search Source is not configured");
            }

            IReadOnlyDictionary<string, PackToTemplateEntry> matchedPacks = _searchCache.GetInfoForNamedPacks(packNameList)
                                    .Where(packInfo => !_packFilter.ShouldPackBeFiltered(packInfo.Key, packInfo.Value.Version))
                                    .ToDictionary(packInfo => packInfo.Key, packInfo => packInfo.Value);

            return Task.FromResult(matchedPacks);
        }
    }
}
