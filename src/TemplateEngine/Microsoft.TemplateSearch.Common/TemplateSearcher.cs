// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateSearch.Common
{
    public class TemplateSearcher
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;

        private readonly string _defaultLanguage;

        private Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> _matchFilter;

        public TemplateSearcher(IEngineEnvironmentSettings environmentSettings, string defaultLanguage, Func<IReadOnlyList<ITemplateNameSearchResult>, IReadOnlyList<ITemplateMatchInfo>> matchFilter)
        {
            _environmentSettings = environmentSettings;
            _defaultLanguage = defaultLanguage;
            _matchFilter = matchFilter;
        }

        // Search all of the registered sources.
        public async Task<SearchResults> SearchForTemplatesAsync(IReadOnlyList<IManagedTemplatePackage> existingTemplatePackages, string inputTemplateName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<TemplateSourceSearchResult> matchesForAllSources = new List<TemplateSourceSearchResult>();
            bool anySearchersConfigured = false;

            foreach (ITemplateSearchSource searchSource in _environmentSettings.Components.OfType<ITemplateSearchSource>())
            {
                if (!await searchSource.TryConfigureAsync(_environmentSettings, existingTemplatePackages, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                anySearchersConfigured = true;

                TemplateSourceSearchResult matchesForSource = await GetBestMatchesForSourceAsync(searchSource, inputTemplateName, cancellationToken).ConfigureAwait(false);

                if (matchesForSource.PacksWithMatches.Count > 0)
                {
                    matchesForAllSources.Add(matchesForSource);
                }
            }

            return new SearchResults(matchesForAllSources, anySearchersConfigured);
        }

        // If needed, tweak the return logic - we may want fewer, or different constraints on what is considered a "match" than is used for installed templates.
        private async Task<TemplateSourceSearchResult> GetBestMatchesForSourceAsync(ITemplateSearchSource searchSource, string templateName, CancellationToken cancellationToken)
        {
            IReadOnlyList<ITemplateNameSearchResult> nameMatches = await searchSource.CheckForTemplateNameMatchesAsync(templateName, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<ITemplateMatchInfo> templateMatches = _matchFilter(nameMatches);

            TemplateSourceSearchResult results = new TemplateSourceSearchResult(searchSource.DisplayName);

            if (templateMatches.Count == 0)
            {
                return results;
            }

            // Map the identities of the templateMatches to the corresponding pack info
            HashSet<string> matchedTemplateIdentities = new HashSet<string>(templateMatches.Select(t => t.Info.Identity));
            IReadOnlyDictionary<string, PackInfo> templateIdentityToPackInfoMap = nameMatches.Where(m => matchedTemplateIdentities.Contains(m.Template.Identity))
                .ToDictionary(
                    x => x.Template.Identity,
                    x => x.PackInfo);

            foreach (ITemplateMatchInfo match in templateMatches)
            {
                if (!templateIdentityToPackInfoMap.TryGetValue(match.Info.Identity, out PackInfo packInfo))
                {
                    // this can't realistically happen. The templateMatches will always be a subset of the nameMatches, and thus will always be in the map.
                    throw new Exception("Unexpected error searching for templates");
                }

                results.AddMatchForPack(packInfo, match);
            }

            return results;
        }
    }
}
