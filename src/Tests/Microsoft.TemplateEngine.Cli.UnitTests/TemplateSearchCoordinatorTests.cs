// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.Common.Providers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TemplateSearchCoordinatorTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private EnvironmentSettingsHelper _environmentSettingsHelper;
       
        public TemplateSearchCoordinatorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        private static readonly ITemplatePackageInfo _packOneInfo = new MockTemplatePackageInfo("PackOne", "1.0.0");

        private static readonly ITemplatePackageInfo _packTwoInfo = new MockTemplatePackageInfo("PackTwo", "1.6.0");

        private static readonly ITemplatePackageInfo _packThreeInfo = new MockTemplatePackageInfo("PackThree", "2.1");

        private static readonly ITemplateInfo _fooOneTemplate =
            new MockTemplateInfo("foo1", name: "MockFooTemplateOne", identity: "Mock.Foo.1", groupIdentity: "Mock.Foo", author: "TestAuthor")
                .WithClassifications("CSharp", "Library")
                .WithDescription("Mock Foo template one")
                .WithChoiceParameter("Framework", "netcoreapp3.0", "netcoreapp3.1")
                .WithTag("language", "C#")
                .WithTag("type", "project");

        private static readonly ITemplateInfo _fooTwoTemplate =
            new MockTemplateInfo("foo2", name: "MockFooTemplateTwo", identity: "Mock.Foo.2", groupIdentity: "Mock.Foo")
                .WithClassifications("CSharp", "Console")
                .WithDescription("Mock Foo template two")
                .WithChoiceParameter("Framework", "netcoreapp2.0", "netcoreapp2.1", "netcoreapp3.1")
                .WithTag("language", "C#");

        private static readonly ITemplateInfo _barCSharpTemplate =
            new MockTemplateInfo("barC", name: "MockBarCsharpTemplate", identity: "Mock.Bar.1.Csharp", groupIdentity: "Mock.Bar")
                .WithClassifications("CSharp")
                .WithDescription("Mock Bar CSharp template")
                .WithTag("language", "C#");

        private static readonly ITemplateInfo _barFSharpTemplate =
            new MockTemplateInfo("barF", name: "MockBarFSharpTemplate", identity: "Mock.Bar.1.FSharp", groupIdentity: "Mock.Bar")
                .WithClassifications("FSharp")
                .WithDescription("Mock Bar FSharp template")
                .WithTag("language", "F#");

        [Fact]
        public async Task CacheSearchNameMatchTest()
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v1FileLocation = SetupDiscoveryMetadata(cacheLocation, false);
            string v2FileLocation = SetupTemplateCache(cacheLocation, false);

            var environment = A.Fake<IEnvironment>();
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: environment,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search foo");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            foreach (var location in new[] { v1FileLocation, v2FileLocation })
            {
                A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(location);
                var searchResults = await searchCoordinator.SearchAsync(
                    factory.GetPackFilter(args),
                    CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                    default).ConfigureAwait(false);

                Assert.Equal(1, searchResults.Count);
                Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
                var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
                Assert.Equal(2, nugetSearchResults.SearchHits.Count);
                Assert.Single(nugetSearchResults.SearchHits, pack => pack.PackageInfo.Name.Equals(_packOneInfo.Name)).MatchedTemplates.Where(t => string.Equals(t.Name, _fooOneTemplate.Name));
                Assert.Single(nugetSearchResults.SearchHits, pack => pack.PackageInfo.Name.Equals(_packTwoInfo.Name)).MatchedTemplates.Where(t => string.Equals(t.Name, _fooTwoTemplate.Name));
            }
        }

        // check that the symbol name-value correctly matches.
        // The _fooOneTemplate is a non-match because of a framework choice param value mismatch.
        // But the _fooTwoTemplate matches because the framework choice is valid for that template.
#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact (Skip = "Fails due to matching on template options is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public async Task CacheSearchCliSymbolNameFilterTest()
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v1FileLocation = SetupDiscoveryMetadata(cacheLocation, true);
            string v2FileLocation = SetupTemplateCache(cacheLocation, true);

            var environment = A.Fake<IEnvironment>();
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: environment,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search foo --framework netcoreapp2.0");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            foreach (var location in new[] { v1FileLocation, v2FileLocation })
            {
                A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(location);
                var searchResults = await searchCoordinator.SearchAsync(
                    factory.GetPackFilter(args),
                    CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                    default).ConfigureAwait(false);

                Assert.Equal(1, searchResults.Count);
                Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
                var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
                Assert.Equal(1, nugetSearchResults.SearchHits.Count);
                Assert.Single(nugetSearchResults.SearchHits, pack => pack.PackageInfo.Name.Equals(_packTwoInfo.Name)).MatchedTemplates.Where(t => string.Equals(t.Name, _fooTwoTemplate.Name));
            }
        }

        // test that an invalid symbol makes the search be a non-match
#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Fails due to matching on template options is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public async Task CacheSearchCliSymbolNameMismatchFilterTest()
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v1FileLocation = SetupDiscoveryMetadata(cacheLocation, true);
            string v2FileLocation = SetupTemplateCache(cacheLocation, true);

            var environment = A.Fake<IEnvironment>();
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: environment,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search foo --tfm netcoreapp2.0");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            foreach (var location in new[] { v1FileLocation, v2FileLocation })
            {
                A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(location);
                var searchResults = await searchCoordinator.SearchAsync(
                    factory.GetPackFilter(args),
                    CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                    default).ConfigureAwait(false);

                Assert.Equal(1, searchResults.Count);
                Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
                var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
                Assert.Equal(0, nugetSearchResults.SearchHits.Count);
            }
        }

        // Tests that the input language causes the correct match filtering.
        [Fact]
        public async Task CacheSearchLanguageFilterTest()
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v1FileLocation = SetupDiscoveryMetadata(cacheLocation, false);
            string v2FileLocation = SetupTemplateCache(cacheLocation, false);

            var environment = A.Fake<IEnvironment>();
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: environment,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search bar --language F#");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            foreach (var location in new[] { v1FileLocation, v2FileLocation })
            {
                A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(location);
                var searchResults = await searchCoordinator.SearchAsync(
                    factory.GetPackFilter(args),
                    CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                    default).ConfigureAwait(false);

                Assert.Equal(1, searchResults.Count);
                Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
                var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
                Assert.Equal(1, nugetSearchResults.SearchHits.Count);
                Assert.Equal(1, nugetSearchResults.SearchHits.First().MatchedTemplates.Count);
                Assert.Equal(_packThreeInfo.Name, nugetSearchResults.SearchHits.First().PackageInfo.Name);
                Assert.Equal(_barFSharpTemplate.Name, nugetSearchResults.SearchHits.First().MatchedTemplates.First().Name);
            }
        }

        [Theory]
        [InlineData("", "test", 1)]
        [InlineData("foo", "test", 1)]
        [InlineData("", "Wrong", 0)]
        public async Task CacheSearchAuthorFilterTest(string commandTemplate, string commandAuthor, int matchCount)
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v1FileLocation = SetupDiscoveryMetadata(cacheLocation, false);
            string v2FileLocation = SetupTemplateCache(cacheLocation, false);

            var environment = A.Fake<IEnvironment>();
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: environment,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search {commandTemplate} --author {commandAuthor}");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            foreach (var location in new[] { v1FileLocation, v2FileLocation })
            {
                A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(location);
                var searchResults = await searchCoordinator.SearchAsync(
                    factory.GetPackFilter(args),
                    CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                    default).ConfigureAwait(false);

                Assert.Equal(1, searchResults.Count);
                Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
                var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
                Assert.Equal(matchCount, nugetSearchResults.SearchHits.Count);
            }
        }

        [Theory]
        [InlineData("", "project", 1)]
        [InlineData("foo", "project", 1)]
        [InlineData("", "Wrong", 0)]
        public async Task CacheSearchTypeFilterTest(string commandTemplate, string commandType, int matchCount)
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v1FileLocation = SetupDiscoveryMetadata(cacheLocation, false);
            string v2FileLocation = SetupTemplateCache(cacheLocation, false);

            var environment = A.Fake<IEnvironment>();
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: environment,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search {commandTemplate} --type {commandType}");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            foreach (var location in new[] { v1FileLocation, v2FileLocation })
            {
                A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(location);
                var searchResults = await searchCoordinator.SearchAsync(
                    factory.GetPackFilter(args),
                    CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                    default).ConfigureAwait(false);

                Assert.Equal(1, searchResults.Count);
                Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
                var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
                Assert.Equal(matchCount, nugetSearchResults.SearchHits.Count);
            }
        }

        [Theory]
        [InlineData("", "Three", 1, 2)]
        [InlineData("barC", "Three", 1, 2)]
        [InlineData("foo", "Three", 0, 0)]
        [InlineData("", "Wrong", 0, 0)]
        public async Task CacheSearchPackageFilterTest(string commandTemplate, string commandPackage, int packMatchCount, int templateMatchCount)
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v1FileLocation = SetupDiscoveryMetadata(cacheLocation, false);
            string v2FileLocation = SetupTemplateCache(cacheLocation, false);

            var environment = A.Fake<IEnvironment>();
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: environment,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search {commandTemplate} --package {commandPackage}");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            foreach (var location in new[] { v1FileLocation, v2FileLocation })
            {
                A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(location);
                var searchResults = await searchCoordinator.SearchAsync(
                    factory.GetPackFilter(args),
                    CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                    default).ConfigureAwait(false);

                Assert.Equal(1, searchResults.Count);
                Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
                var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
                Assert.Equal(packMatchCount, nugetSearchResults.SearchHits.Count);
                if (packMatchCount != 0)
                {
                    Assert.Equal(templateMatchCount, nugetSearchResults.SearchHits.Single(res => res.PackageInfo.Name == _packThreeInfo.Name).MatchedTemplates.Count);
                }
            }
        }

        [Theory]
        [InlineData("", "CSharp", 3, 3)]
        [InlineData("bar", "FSharp", 1, 1)]
        [InlineData("foo", "Library", 1, 1)]
        [InlineData("", "Wrong", 0, 0)]
        [InlineData("", "Lib", 0, 0)]
        public async Task CacheSearchTagFilterTest(string commandTemplate, string commandTag, int packMatchCount, int templateMatchCount)
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v1FileLocation = SetupDiscoveryMetadata(cacheLocation, false);
            string v2FileLocation = SetupTemplateCache(cacheLocation, false);

            var environment = A.Fake<IEnvironment>();
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: environment,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search {commandTemplate} --tag {commandTag}");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            foreach (var location in new[] { v1FileLocation, v2FileLocation })
            {
                A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(location);
                var searchResults = await searchCoordinator.SearchAsync(
                    factory.GetPackFilter(args),
                    CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                    default).ConfigureAwait(false);

                Assert.Equal(1, searchResults.Count);
                Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
                var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
                Assert.Equal(packMatchCount, nugetSearchResults.SearchHits.Count);
                if (packMatchCount != 0)
                {
                    Assert.Equal(templateMatchCount, nugetSearchResults.SearchHits.Sum(res => res.MatchedTemplates.Count));
                }
            }
        }

        [Fact]
        public async Task CacheSearchLanguageMismatchFilterTest()
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v1FileLocation = SetupDiscoveryMetadata(cacheLocation, false);
            string v2FileLocation = SetupTemplateCache(cacheLocation, false);

            var environment = A.Fake<IEnvironment>();
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: environment,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search bar --language VB");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            foreach (var location in new[] { v1FileLocation, v2FileLocation })
            {
                A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(location);
                var searchResults = await searchCoordinator.SearchAsync(
                    factory.GetPackFilter(args),
                    CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                    default).ConfigureAwait(false);

                Assert.Equal(1, searchResults.Count);
                Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
                var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
                Assert.Equal(0, nugetSearchResults.SearchHits.Count);
            }
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Not relevant due to matching on template options is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public async Task CacheSkipInvalidTemplatesTest()
        {
            string cacheLocation = TestUtils.CreateTemporaryFolder();
            string v2FileLocation = SetupInvalidTemplateCache(cacheLocation);

            var environment = A.Fake<IEnvironment>();

            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true, environment: environment);
            var templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);

            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => engineEnvironmentSettings.Host, _ => new TelemetryLogger(null, false));
            var parseResult = myCommand.Parse($"new search --unknown");
            SearchCommandArgs args = new SearchCommandArgs((SearchCommand)parseResult.CommandResult.Command, parseResult);

            var templatePackages = await templatePackageManager.GetManagedTemplatePackagesAsync(false, default).ConfigureAwait(false);
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(engineEnvironmentSettings);
            CliSearchFiltersFactory factory = new CliSearchFiltersFactory(templatePackages);

            A.CallTo(() => environment.GetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE")).Returns(v2FileLocation);
            var searchResults = await searchCoordinator.SearchAsync(
                factory.GetPackFilter(args),
                CliSearchFiltersFactory.GetMatchingTemplatesFilter(args),
                default).ConfigureAwait(false);

            Assert.Equal(1, searchResults.Count);
            Assert.Single(searchResults, result => result.Provider.Factory.DisplayName == "NuGet.org");
            var nugetSearchResults = searchResults.Single(result => result.Provider.Factory.DisplayName == "NuGet.org");
            Assert.Equal(0, nugetSearchResults.SearchHits.Count);
        }

        [Theory]
        [InlineData(12489, 3198, 1)]
        [InlineData(3198, 12489, -1)]
        [InlineData(124, 3198, -1)]
        [InlineData(3198, 124, 1)]
        [InlineData(0, 0, 0)]
        [InlineData(-10, 0, 0)]
        [InlineData(987, 0, 1)]
        [InlineData(0, 10, -1)]
        [InlineData(987, 1, 0)]
        [InlineData(123, 345, 0)]
        public void TestCompare(long x, long y, int expectedOutcome)
        {
            Assert.Equal(expectedOutcome, CliTemplateSearchCoordinator.SearchResultTableRow.TotalDownloadsComparer.Compare(x, y));
        }

#pragma warning disable CS0618 // Type or member is obsolete
        private static string SetupDiscoveryMetadata(string fileLocation, bool includehostData = false)

        {
            const string version = "1.0.0.0";

            List<ITemplateInfo> templateCache = new List<ITemplateInfo>();

            templateCache.Add(_fooOneTemplate);
            templateCache.Add(_fooTwoTemplate);
            templateCache.Add(_barCSharpTemplate);
            templateCache.Add(_barFSharpTemplate);

            Dictionary<string, PackToTemplateEntry> packToTemplateMap = new Dictionary<string, PackToTemplateEntry>();

            List<TemplateIdentificationEntry> packOneTemplateInfo = new List<TemplateIdentificationEntry>()
            {
                new TemplateIdentificationEntry(_fooOneTemplate.Identity, _fooOneTemplate.GroupIdentity)
            };
            packToTemplateMap[_packOneInfo.Name] = new PackToTemplateEntry(_packOneInfo.Version ?? "", packOneTemplateInfo);

            List<TemplateIdentificationEntry> packTwoTemplateInfo = new List<TemplateIdentificationEntry>()
            {
                new TemplateIdentificationEntry(_fooTwoTemplate.Identity, _fooTwoTemplate.GroupIdentity)
            };
            packToTemplateMap[_packTwoInfo.Name] = new PackToTemplateEntry(_packTwoInfo.Version ?? "", packTwoTemplateInfo);

            List<TemplateIdentificationEntry> packThreeTemplateInfo = new List<TemplateIdentificationEntry>()
            {
                new TemplateIdentificationEntry(_barCSharpTemplate.Identity, _barCSharpTemplate.GroupIdentity),
                new TemplateIdentificationEntry(_barFSharpTemplate.Identity, _barFSharpTemplate.GroupIdentity)
            };
            packToTemplateMap[_packThreeInfo.Name] = new PackToTemplateEntry(_packThreeInfo.Version ?? "", packThreeTemplateInfo);

            Dictionary<string, object> additionalData = new Dictionary<string, object>();

            if (includehostData)
            {
                Dictionary<string, string> frameworkParamSymbolInfo = new Dictionary<string, string>()
                {
                    { "longName", "framework" },
                    { "shortName", "f" }
                };

                HostSpecificTemplateData fooTemplateHostData = new MockHostSpecificTemplateData(
                    new Dictionary<string, IReadOnlyDictionary<string, string>>()
                    {
                        { "Framework", frameworkParamSymbolInfo }
                    }
                );

                Dictionary<string, HostSpecificTemplateData> cliHostData = new Dictionary<string, HostSpecificTemplateData>()
                {
                    { _fooOneTemplate.Identity, fooTemplateHostData },
                    { _fooTwoTemplate.Identity, fooTemplateHostData }
                };

                additionalData[CliHostSearchCacheData.DataName] = cliHostData;
            }

            TemplateDiscoveryMetadata discoveryMetadata = new TemplateDiscoveryMetadata(version, templateCache, packToTemplateMap, additionalData);
            string targetPath = Path.Combine(fileLocation, "searchCacheV1.json");
            File.WriteAllText(targetPath, discoveryMetadata.ToJObject().ToString());
            return Path.Combine(fileLocation, "searchCacheV1.json");
        }
#pragma warning restore CS0618 // Type or member is obsolete

        private static string SetupTemplateCache(string fileLocation, bool includehostData = false)
        {
            Dictionary<string, string> frameworkParamSymbolInfo = new Dictionary<string, string>()
                {
                    { "longName", "framework" },
                    { "shortName", "f" }
                };

            HostSpecificTemplateData fooTemplateHostData = new MockHostSpecificTemplateData(
                new Dictionary<string, IReadOnlyDictionary<string, string>>()
                {
                        { "Framework", frameworkParamSymbolInfo }
                }
            );

            Dictionary<string, object> additionalData = new Dictionary<string, object>()
            {
                { CliHostSearchCacheData.DataName, fooTemplateHostData }
            };
            List<ITemplateInfo> templateCache = new List<ITemplateInfo>();

            templateCache.Add(_fooOneTemplate);
            templateCache.Add(_fooTwoTemplate);
            templateCache.Add(_barCSharpTemplate);
            templateCache.Add(_barFSharpTemplate);

            var fooOneTemplateData = new TemplateSearchData(_fooOneTemplate, includehostData ? additionalData : null);
            var fooTwoTemplateData = new TemplateSearchData(_fooTwoTemplate, includehostData ? additionalData : null);
            var barCSharpTemplateData = new TemplateSearchData(_barCSharpTemplate, null);
            var barFSharpTemplateData = new TemplateSearchData(_barFSharpTemplate, null);

            var packOne = new TemplatePackageSearchData(_packOneInfo, new[] { fooOneTemplateData });
            var packTwo = new TemplatePackageSearchData(_packTwoInfo, new[] { fooTwoTemplateData });
            var packThree = new TemplatePackageSearchData(_packThreeInfo, new[] { barCSharpTemplateData, barFSharpTemplateData });

            var cache = new TemplateSearchCache(new[] { packOne, packTwo, packThree });

            JObject toSerialize = JObject.FromObject(cache);
            string targetPath = Path.Combine(fileLocation, "searchCacheV2.json");
            File.WriteAllText(targetPath, toSerialize.ToString());
            return targetPath;
        }

        private static string SetupInvalidTemplateCache(string fileLocation)
        {
            var packOne = new TemplatePackageSearchData(new MockTemplatePackageInfo("PackOne", "1.0.0"), new[] { new TemplateSearchData(new MockTemplateInfo("foo", "foo", "foo").WithParameters("Config type", "Main type", "unknown")) });
            var cache = new TemplateSearchCache(new[] { packOne });

            JObject toSerialize = JObject.FromObject(cache);
            string targetPath = Path.Combine(fileLocation, "searchCacheV2.json");
            File.WriteAllText(targetPath, toSerialize.ToString());
            return targetPath;
        }
    }
}
