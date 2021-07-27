// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateSearch.Common.Abstractions;
using Xunit;

namespace Microsoft.TemplateSearch.Common.UnitTests
{
    public class TemplateSearchCoordinatorTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public TemplateSearchCoordinatorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        private static readonly ITemplatePackageInfo _fooPackInfo = new PackInfo("fooPack", "1.0.0");

        private static readonly ITemplatePackageInfo _barPackInfo = new PackInfo("barPack", "2.0.0");

        private static readonly ITemplatePackageInfo _redPackInfo = new PackInfo("redPack", "1.1");

        private static readonly ITemplatePackageInfo _bluePackInfo = new PackInfo("bluePack", "2.1");

        private static readonly ITemplatePackageInfo _greenPackInfo = new PackInfo("greenPack", "3.0.0");

        [Fact]
        public async Task TwoSourcesAreBothSearched()
        {
            var provider1 = new MockTemplateSearchProvider();
            var provider2 = new MockTemplateSearchProvider();
            _engineEnvironmentSettings.Components.AddComponent(
                typeof(ITemplateSearchProviderFactory),
                new MockTemplateSearchProviderFactory(Guid.NewGuid(), "provider1", provider1));

            _engineEnvironmentSettings.Components.AddComponent(
                 typeof(ITemplateSearchProviderFactory),
                 new MockTemplateSearchProviderFactory(Guid.NewGuid(), "provider2", provider2));

            var searchCoordinator = new TemplateSearchCoordinator(_engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, p => p.Templates.ToList(), default).ConfigureAwait(false);

            Assert.Equal(2, searchResult.Count());
            Assert.Single(searchResult, r => r.Provider.Factory.DisplayName == "provider1");
            Assert.Single(searchResult, r => r.Provider.Factory.DisplayName == "provider2");

            Assert.True(provider2.WasSearched);
        }

        [Fact]
        public async Task SourcesCorrectlyReturnResults()
        {
            List<MockTemplateSearchProvider> createdProviders = new List<MockTemplateSearchProvider>();
            var sourcesToSetup = GetMockNameSearchResults();
            foreach (var source in sourcesToSetup)
            {
                var provider = new MockTemplateSearchProvider();
                createdProviders.Add(provider);
                provider.Results = source.Value;
                _engineEnvironmentSettings.Components.AddComponent(
                     typeof(ITemplateSearchProviderFactory),
                     new MockTemplateSearchProviderFactory(Guid.NewGuid(), source.Key, provider));
            }

            const string templateName = "foo";

            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> filter =
                templatePack => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(_engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);
            Assert.Equal(2, searchResult.Count);

            var searchResultDictionary = searchResult.ToDictionary(r => r.Provider.Factory.DisplayName);

            Assert.True(searchResultDictionary.ContainsKey("source one"));
            Assert.True(searchResultDictionary.ContainsKey("source two"));

            Assert.Equal(3, searchResultDictionary["source two"].SearchHits.Count);
            Assert.Equal(2, searchResultDictionary["source one"].SearchHits.Count);

            Assert.True(createdProviders.All(p => p.WasSearched));
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<(ITemplatePackageInfo PackageInfo, IReadOnlyList<ITemplateInfo> MatchedTemplates)>> GetMockNameSearchResults()
        {
            Dictionary<string, IReadOnlyList<(ITemplatePackageInfo, IReadOnlyList<ITemplateInfo>)>> dataForSources = new();

            ITemplateInfo sourceOneTemplateOne = new MockTemplateInfo("foo1", name: "MockFooTemplateOne", identity: "Mock.Foo.1").WithDescription("Mock Foo template one");
            ITemplateInfo sourceOneTemplateTwo = new MockTemplateInfo("foo2", name: "MockFooTemplateTwo", identity: "Mock.Foo.2").WithDescription("Mock Foo template two");
            ITemplateInfo sourceOneTemplateThree = new MockTemplateInfo("bar1", name: "MockBarTemplateOne", identity: "Mock.Bar.1").WithDescription("Mock Bar template one");

            var packOne = (_fooPackInfo, (IReadOnlyList<ITemplateInfo>)new[] { sourceOneTemplateOne, sourceOneTemplateTwo });
            var packTwo = (_barPackInfo, new[] { sourceOneTemplateThree });

            dataForSources["source one"] = new[] { packOne, packTwo };

            ITemplateInfo sourceTwoTemplateOne = new MockTemplateInfo("red", name: "MockRedTemplate", identity: "Mock.Red.1").WithDescription("Mock red template");
            ITemplateInfo sourceTwoTemplateTwo = new MockTemplateInfo("blue", name: "MockBlueTemplate", identity: "Mock.Blue.1").WithDescription("Mock blue template");
            ITemplateInfo sourceTwoTemplateThree = new MockTemplateInfo("green", name: "MockGreenTemplate", identity: "Mock.Green.1").WithDescription("Mock green template");

            var red = (_redPackInfo, (IReadOnlyList<ITemplateInfo>)new[] { sourceTwoTemplateOne });
            var blue = (_bluePackInfo, new[] { sourceTwoTemplateTwo });
            var green = (_greenPackInfo, new[] { sourceTwoTemplateThree });

            dataForSources["source two"] = new[] { red, blue, green };

            return dataForSources;
        }
    }
}
