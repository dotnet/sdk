using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateSearch.Common
{
    public abstract class TemplateSearchCoordinator : ITemplateSearchCoordinator
    {
        public TemplateSearchCoordinator(IEngineEnvironmentSettings environmentSettings, string inputTemplateName, string defaultLanguage)
        {
            _environmentSettings = environmentSettings;
            _inputTemplateName = inputTemplateName;
            _defaultLanguage = defaultLanguage;
            _isSearchPerformed = false;
        }

        protected readonly IEngineEnvironmentSettings _environmentSettings;
        protected readonly string _inputTemplateName;
        protected readonly string _defaultLanguage;
        private bool _isSearchPerformed;
        protected SearchResults _searchResults;

        public async Task<bool> CoordinateSearchAsync()
        {
            await EnsureSearchResults();

            return HandleSearchResults();
        }

        // return true if there were any search results, false otherwise.
        protected abstract bool HandleSearchResults();

        protected abstract Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> MatchFilter { get; }

        protected async Task EnsureSearchResults()
        {
            if (_isSearchPerformed)
            {
                return;
            }

            TemplateSearcher searcher = new TemplateSearcher(_environmentSettings, _defaultLanguage, MatchFilter);
            IReadOnlyList<IInstallUnitDescriptor> existingInstallDescriptors;

            if (_environmentSettings.SettingsLoader is SettingsLoader settingsLoader)
            {
                existingInstallDescriptors = settingsLoader.InstallUnitDescriptorCache.Descriptors.Values.ToList();
            }
            else
            {
                existingInstallDescriptors = new List<IInstallUnitDescriptor>();
            }

            _searchResults = await searcher.SearchForTemplatesAsync(existingInstallDescriptors, _inputTemplateName);

            _isSearchPerformed = true;
        }
    }
}
