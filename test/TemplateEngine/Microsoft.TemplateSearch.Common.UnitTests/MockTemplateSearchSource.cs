// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.Common.UnitTests
{
    public class MockTemplateSearchProviderFactory : ITemplateSearchProviderFactory
    {
        private readonly ITemplateSearchProvider _searchProvider;

        public MockTemplateSearchProviderFactory(Guid id, string displayName, MockTemplateSearchProvider searchProvider)
        {
            Id = id;
            DisplayName = displayName;
            _searchProvider = searchProvider;
            searchProvider.Factory = this;
        }

        public string DisplayName { get; }

        public Guid Id { get; }

        public ITemplateSearchProvider CreateProvider(IEngineEnvironmentSettings environmentSettings, IReadOnlyDictionary<string, Func<object, object>> additionalDataReaders)
        {
            return _searchProvider;
        }
    }

    public class MockTemplateSearchProvider : ITemplateSearchProvider
    {
        private bool _wasSearched;

        public bool WasSearched => _wasSearched;

        public IReadOnlyList<(ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)> Results { get; set; } = Array.Empty<(ITemplatePackageInfo, IReadOnlyList<ITemplateInfo>)>();

        public ITemplateSearchProviderFactory Factory { get; set; }

        Task<IReadOnlyList<(ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)>> ITemplateSearchProvider.SearchForTemplatePackagesAsync(
            Func<TemplatePackageSearchData, bool> packFilters,
            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> matchingTemplatesFilter,
            CancellationToken cancellationToken)
        {
            _wasSearched = true;
            return Task.FromResult(Results);
        }
    }
}
