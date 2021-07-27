// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    /// <summary>
    /// <see cref="TemplateSearchCoordinator"/> performs the search for the templates in registered search providers.
    /// </summary>
    public sealed class TemplateSearchCoordinator
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly IReadOnlyDictionary<string, Func<object, object>> _additionalDataReaders;
        private readonly IReadOnlyDictionary<string, ITemplateSearchProvider> _providers;

        public TemplateSearchCoordinator(
            IEngineEnvironmentSettings environmentSettings,
            IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders = null)
        {
            _environmentSettings = environmentSettings ?? throw new ArgumentNullException(nameof(environmentSettings));
            _additionalDataReaders = additionalDataReaders ?? new Dictionary<string, Func<object, object>>();
            Dictionary<string, ITemplateSearchProvider> configuredProviders = new Dictionary<string, ITemplateSearchProvider>();
            foreach (ITemplateSearchProviderFactory factory in _environmentSettings.Components.OfType<ITemplateSearchProviderFactory>())
            {
                configuredProviders.Add(factory.DisplayName, factory.CreateProvider(_environmentSettings, _additionalDataReaders));
            }
            _providers = configuredProviders;
        }

        /// <summary>
        /// Searches for the templates matching the filters in registered search providers.
        /// </summary>
        /// <param name="packFilter">The filter that defines if <see cref="TemplatePackageSearchData"/> is a match.</param>
        /// <param name="matchingTemplatesFilter">The filter that list of templates that are the match inside given <see cref="TemplatePackageSearchData"/>.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The list of <see cref="SearchResult"/> representing the result of the search for given <see cref="ITemplateSearchProvider"/>.</returns>
        public async Task<IReadOnlyList<SearchResult>> SearchAsync(
            Func<TemplatePackageSearchData, bool> packFilter,
            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> matchingTemplatesFilter,
            CancellationToken cancellationToken)
        {
            if (packFilter is null)
            {
                throw new ArgumentNullException(nameof(packFilter));
            }

            if (matchingTemplatesFilter is null)
            {
                throw new ArgumentNullException(nameof(matchingTemplatesFilter));
            }
            cancellationToken.ThrowIfCancellationRequested();

            List<SearchResult> results = new List<SearchResult>();

            foreach (ITemplateSearchProvider provider in _providers.Values)
            {
                try
                {
                    var providerResults = await provider.SearchForTemplatePackagesAsync(packFilter, matchingTemplatesFilter, cancellationToken).ConfigureAwait(false);
                    results.Add(new SearchResult(provider, true, hits: providerResults));
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _environmentSettings.Host.Logger.LogDebug("Search by provider {0} failed, detailes: {1}", provider.Factory.DisplayName, ex);
                    results.Add(new SearchResult(provider, false, ex.Message));
                }
            }
            return results;
        }
    }
}
