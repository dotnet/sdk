// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.Common.Providers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateSearch.Common.UnitTests
{
    public class TemplateSearchCacheReaderTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public TemplateSearchCacheReaderTests(EnvironmentSettingsHelper helper)
        {
            _environmentSettingsHelper = helper;
        }

        [Fact]
        public void CanReadSearchMetadata()
        {
            var environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string content = File.ReadAllText("NuGetTemplateSearchInfo.json");
            JObject cache = JObject.Parse(content);

            var parsedCache = TemplateSearchCache.FromJObject(cache, environmentSettings.Host.Logger);

            Assert.Single(parsedCache.TemplatePackages);
            Assert.Equal(2, parsedCache.TemplatePackages.Sum(p => p.Templates.Count));

            Assert.IsAssignableFrom<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);

            //can read tags
            Assert.Equal(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).TagsCollection.Count);

            //can read parameters
            Assert.Equal(5, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).ParameterDefinitions.Count);
        }

        [Fact]
        public void CanReadSearchMetadata_V2()
        {
            var environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string content = File.ReadAllText("NuGetTemplateSearchInfo_v2.json");
            JObject cache = JObject.Parse(content);

            var parsedCache = TemplateSearchCache.FromJObject(cache, environmentSettings.Host.Logger);

            Assert.Single(parsedCache.TemplatePackages);
            Assert.Equal(3, parsedCache.TemplatePackages.Sum(p => p.Templates.Count));

            Assert.IsAssignableFrom<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);

            //can read tags
            Assert.Equal(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).TagsCollection.Count);

            //can read parameters: 2 tags + 3 cache parameters
            Assert.Equal(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).ParameterDefinitions.Count);

            Assert.Equal(3, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[2]).ParameterDefinitions.Count);
            Assert.Equal(1, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[2]).ParameterDefinitions.Count(p => p.DataType == "choice"));
            Assert.Equal(3, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[2]).ParameterDefinitions.Single(p => p.DataType == "choice").Choices?.Count);
        }

        [Fact]
        public void CanSkipInvalidEntriesSearchMetadata()
        {
            var environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string content = File.ReadAllText("NuGetTemplateSearchInfoWithInvalidData.json");
            JObject cache = JObject.Parse(content);

            var parsedCache = TemplateSearchCache.FromJObject(cache, environmentSettings.Host.Logger);

            Assert.Single(parsedCache.TemplatePackages);
            Assert.Equal(1, parsedCache.TemplatePackages.Sum(p => p.Templates.Count));

            Assert.IsAssignableFrom<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);
            Assert.Equal("Microsoft.AzureFunctions.ProjectTemplate.CSharp.3.x", ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).Identity);
        }

        [Fact]
        public async Task CanReadSearchMetadata_FromBlob()
        {
            var environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var sourceFileProvider = new NuGetMetadataSearchProvider(
                A.Fake<ITemplateSearchProviderFactory>(),
                environmentSettings,
                new Dictionary<string, Func<object, object>>());
            async Task<string> Search() => await sourceFileProvider.GetSearchFileAsync(default);
            await TestUtils.AttemptSearch<string, HttpRequestException>(3, TimeSpan.FromSeconds(10), Search);
            string content = environmentSettings.Host.FileSystem.ReadAllText(Path.Combine(environmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json"));
            var jObj = JObject.Parse(content);
            Assert.NotNull(TemplateSearchCache.FromJObject(jObj, environmentSettings.Host.Logger, null));
        }

        [Fact]
        public async Task CanReadLegacySearchMetadata_FromBlob()
        {
            var environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var sourceFileProvider = new NuGetMetadataSearchProvider(
                A.Fake<ITemplateSearchProviderFactory>(),
                environmentSettings,
                new Dictionary<string, Func<object, object>>(),
                new[] { "https://go.microsoft.com/fwlink/?linkid=2087906&clcid=0x409" });  //v1 search cache
            async Task<string> Search() => await sourceFileProvider.GetSearchFileAsync(default);
            await TestUtils.AttemptSearch<string, HttpRequestException>(3, TimeSpan.FromSeconds(10), Search);
            string content = environmentSettings.Host.FileSystem.ReadAllText(Path.Combine(environmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json"));
            var jObj = JObject.Parse(content);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.True(LegacySearchCacheReader.TryReadDiscoveryMetadata(jObj, environmentSettings.Host.Logger, null, out _));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Fact]
        public void CanReadSearchMetadata_V2_E2E()
        {
            Guid postAction1 = Guid.NewGuid();
            Guid postAction2 = Guid.NewGuid();

            ITemplateInfo mockTemplate = new MockTemplateInfo("shortName", "Full Name", "test.identity", "test.group.identity", 100, "test author")
                .WithClassifications("test", "asset")
                .WithDescription("my test description")
                .WithTag("language", "CSharp")
                .WithParameters("param1", "param2")
                .WithChoiceParameter("choice", "var1", "var2", "var3")
                .WithPostActions(postAction1, postAction2);

            TemplateSearchData template = new TemplateSearchData(mockTemplate);

            var mockPackage = A.Fake<ITemplatePackageInfo>();
            A.CallTo(() => mockPackage.Name).Returns("pack");
            A.CallTo(() => mockPackage.Version).Returns("packVer");
            A.CallTo(() => mockPackage.Description).Returns("description");
            A.CallTo(() => mockPackage.IconUrl).Returns("https://icon");

            TemplatePackageSearchData package = new TemplatePackageSearchData(mockPackage, new[] { template });
            TemplateSearchCache cache = new TemplateSearchCache(new[] { package });

            JObject jobj = cache.ToJObject();

            TemplateSearchCache deserializedCache = TemplateSearchCache.FromJObject(jobj, NullLogger.Instance);

            Assert.Equal("2.0", deserializedCache.Version);
            Assert.Single(deserializedCache.TemplatePackages);
            Assert.Single(deserializedCache.TemplatePackages[0].Templates);

            Assert.Equal("pack", deserializedCache.TemplatePackages[0].Name);
            Assert.Equal("packVer", deserializedCache.TemplatePackages[0].Version);
            Assert.Equal("description", deserializedCache.TemplatePackages[0].Description);
            Assert.Equal("https://icon", deserializedCache.TemplatePackages[0].IconUrl);

            var templateToTest = deserializedCache.TemplatePackages[0].Templates[0];

            Assert.Equal("shortName", templateToTest.ShortNameList[0]);
            Assert.Equal("Full Name", templateToTest.Name);
            Assert.Equal("test.identity", templateToTest.Identity);
            Assert.Equal("test.group.identity", templateToTest.GroupIdentity);
            Assert.Equal(100, templateToTest.Precedence);
            Assert.Equal("test author", templateToTest.Author);
            Assert.Equal(2, templateToTest.Classifications.Count);

            Assert.Equal("my test description", templateToTest.Description);
            Assert.Equal("CSharp", templateToTest.TagsCollection["language"]);

            Assert.Equal(3, templateToTest.ParameterDefinitions.Count);

            Assert.Single(templateToTest.ParameterDefinitions.Where(p => p.DataType == "choice"));
            Assert.Equal(3, templateToTest.ParameterDefinitions.Single(p => p.DataType == "choice").Choices?.Count);
            Assert.True(templateToTest.ParameterDefinitions.Single(p => p.DataType == "choice").Choices?.ContainsKey("var1"));

            Assert.Equal(2, ((ITemplateInfo)templateToTest).PostActions.Count);
            Assert.Equal(new[] { postAction1, postAction2 }, ((ITemplateInfo)templateToTest).PostActions);
        }
    }
}
