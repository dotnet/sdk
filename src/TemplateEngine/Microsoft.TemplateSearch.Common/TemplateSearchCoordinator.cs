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
        private bool _isSearchPerformed;

        public TemplateSearchCoordinator(IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, string inputTemplateName, string defaultLanguage, Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> matchFilter)
        {
            EnvironmentSettings = environmentSettings;
            TemplatePackagesManager = templatePackageManager;
            InputTemplateName = inputTemplateName;
            DefaultLanguage = defaultLanguage;
            MatchFilter = matchFilter;
            _isSearchPerformed = false;
        }

        protected IEngineEnvironmentSettings EnvironmentSettings { get; }

        protected TemplatePackageManager TemplatePackagesManager { get; }

        protected string InputTemplateName { get; }

        protected string DefaultLanguage { get; }

        protected Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> MatchFilter { get; set; }

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

            existingTemplatePackage = (await TemplatePackagesManager.GetTemplatePackagesAsync(false).ConfigureAwait(false)).OfType<IManagedTemplatePackage>().ToList();

            SearchResults = await searcher.SearchForTemplatesAsync(existingTemplatePackage, InputTemplateName).ConfigureAwait(false);

            _isSearchPerformed = true;
        }
    }
}
