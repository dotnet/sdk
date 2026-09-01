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
        private ITemplateSearchProviderFactory? _factory;

        public bool WasSearched { get; private set; }

        public IReadOnlyList<(ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)> Results { get; set; } = [];

        public ITemplateSearchProviderFactory Factory
        {
            get => _factory ?? throw new Exception($"{nameof(Factory)} is not set.");

            set => _factory = value;
        }

        Task<IReadOnlyList<(ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)>> ITemplateSearchProvider.SearchForTemplatePackagesAsync(
            Func<TemplatePackageSearchData, bool> packFilters,
            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> matchingTemplatesFilter,
            CancellationToken cancellationToken)
        {
            WasSearched = true;
            return Task.FromResult(Results);
        }
    }
}
