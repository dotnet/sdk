// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateSearch.Common
{
    public class TemplateSearchCoordinator
    {
        public TemplateSearchCoordinator(IEngineEnvironmentSettings environmentSettings, string inputTemplateName, string defaultLanguage, Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> matchFilter)
        {
            _environmentSettings = environmentSettings;
            _inputTemplateName = inputTemplateName;
            _defaultLanguage = defaultLanguage;
            _matchFilter = matchFilter;
            _isSearchPerformed = false;
        }

        protected IEngineEnvironmentSettings _environmentSettings { get; }
        protected string _inputTemplateName { get; }
        protected string _defaultLanguage { get; }
        protected Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> _matchFilter { get; set; }
        private bool _isSearchPerformed;
        protected SearchResults _searchResults { get; set; }

        public async Task<SearchResults> SearchAsync()
        {
            await EnsureSearchResultsAsync().ConfigureAwait(false);
            return _searchResults;
        }

        protected async Task EnsureSearchResultsAsync()
        {
            if (_isSearchPerformed)
            {
                return;
            }

            TemplateSearcher searcher = new TemplateSearcher(_environmentSettings, _defaultLanguage, _matchFilter);
            IReadOnlyList<IManagedTemplatePackage> existingTemplatePackage;

            if (_environmentSettings.SettingsLoader is SettingsLoader settingsLoader)
            {
                existingTemplatePackage = (await settingsLoader.TemplatePackagesManager.GetTemplatePackagesAsync(false).ConfigureAwait(false)).OfType<IManagedTemplatePackage>().ToList();
            }
            else
            {
                existingTemplatePackage = new List<IManagedTemplatePackage>();
            }

            _searchResults = await searcher.SearchForTemplatesAsync(existingTemplatePackage, _inputTemplateName).ConfigureAwait(false);

            _isSearchPerformed = true;
        }
    }
}
