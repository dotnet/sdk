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
            EnvironmentSettings = environmentSettings;
            InputTemplateName = inputTemplateName;
            DefaultLanguage = defaultLanguage;
            MatchFilter = matchFilter;
            _isSearchPerformed = false;
        }

        protected IEngineEnvironmentSettings EnvironmentSettings { get; }
        protected string InputTemplateName { get; }
        protected string DefaultLanguage { get; }
        protected Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> MatchFilter { get; set; }
        private bool _isSearchPerformed;
        protected SearchResults SearchResults { get; set; }

        public async Task<SearchResults> SearchAsync()
        {
            await EnsureSearchResultsAsync().ConfigureAwait(false);
            return SearchResults;
        }

        protected async Task EnsureSearchResultsAsync()
        {
            if (_isSearchPerformed)
            {
                return;
            }

            TemplateSearcher searcher = new TemplateSearcher(EnvironmentSettings, DefaultLanguage, MatchFilter);
            IReadOnlyList<IManagedTemplatePackage> existingTemplatePackage;

            if (EnvironmentSettings.SettingsLoader is SettingsLoader settingsLoader)
            {
                existingTemplatePackage = (await settingsLoader.TemplatePackagesManager.GetTemplatePackagesAsync(false).ConfigureAwait(false)).OfType<IManagedTemplatePackage>().ToList();
            }
            else
            {
                existingTemplatePackage = new List<IManagedTemplatePackage>();
            }

            SearchResults = await searcher.SearchForTemplatesAsync(existingTemplatePackage, InputTemplateName).ConfigureAwait(false);

            _isSearchPerformed = true;
        }
    }
}
