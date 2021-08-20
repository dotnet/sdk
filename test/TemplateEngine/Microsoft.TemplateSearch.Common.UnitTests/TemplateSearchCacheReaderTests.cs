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
    public class TemplateSearchCacheReaderTests
    {
        [Fact]
        public void CanReadSearchMetadata()
        {
            using EnvironmentSettingsHelper environmentSettingsHelper = new EnvironmentSettingsHelper();
            var environmentSettings = environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string content = File.ReadAllText("NuGetTemplateSearchInfo.json");
            JObject cache = JObject.Parse(content);

            var parsedCache = TemplateSearchCache.FromJObject(cache, environmentSettings.Host.Logger);

            Assert.Equal(1, parsedCache.TemplatePackages.Count);
            Assert.Equal(2, parsedCache.TemplatePackages.Sum(p => p.Templates.Count)); 

            Assert.IsAssignableFrom<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);

            //can read tags
            Assert.Equal(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).TagsCollection.Count);

            //can read parameters
            Assert.Equal(5, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).Parameters.Count);
        }

        [Fact]
        public void CanReadSearchMetadata_V2()
        {
            using EnvironmentSettingsHelper environmentSettingsHelper = new EnvironmentSettingsHelper();
            var environmentSettings = environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string content = File.ReadAllText("NuGetTemplateSearchInfo_v2.json");
            JObject cache = JObject.Parse(content);

            var parsedCache = TemplateSearchCache.FromJObject(cache, environmentSettings.Host.Logger);

            Assert.Equal(1, parsedCache.TemplatePackages.Count);
            Assert.Equal(3, parsedCache.TemplatePackages.Sum(p => p.Templates.Count));

            Assert.IsAssignableFrom<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);

            //can read tags
            Assert.Equal(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).TagsCollection.Count);

            //can read parameters: 2 tags + 3 cache parameters
            Assert.Equal(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).Parameters.Count);

            Assert.Equal(3, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[2]).Parameters.Count);
            Assert.Equal(1, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[2]).Parameters.Where(p => p.DataType == "choice").Count());
            Assert.Equal(3, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[2]).Parameters.Single(p => p.DataType == "choice").Choices?.Count);
        }

        [Fact]
        public void CanSkipInvalidEntriesSearchMetadata()
        {
            using EnvironmentSettingsHelper environmentSettingsHelper = new EnvironmentSettingsHelper();
            var environmentSettings = environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string content = File.ReadAllText("NuGetTemplateSearchInfoWithInvalidData.json");
            JObject cache = JObject.Parse(content);

            var parsedCache = TemplateSearchCache.FromJObject(cache, environmentSettings.Host.Logger);

            Assert.Equal(1, parsedCache.TemplatePackages.Count);
            Assert.Equal(1, parsedCache.TemplatePackages.Sum(p => p.Templates.Count));

            Assert.IsAssignableFrom<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);
            Assert.Equal("Microsoft.AzureFunctions.ProjectTemplate.CSharp.3.x", ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).Identity);
        }

        [Fact]
        public async Task CanReadSearchMetadata_FromBlob()
        {
            using EnvironmentSettingsHelper environmentSettingsHelper = new EnvironmentSettingsHelper();
            var environmentSettings = environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var sourceFileProvider = new NuGetMetadataSearchProvider(
                A.Fake<ITemplateSearchProviderFactory>(),
                environmentSettings,
                new Dictionary<string, Func<object, object>>());
            await sourceFileProvider.GetSearchFileAsync(default).ConfigureAwait(false);
            string content = environmentSettings.Host.FileSystem.ReadAllText(Path.Combine(environmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json"));
            var jObj = JObject.Parse(content);
            Assert.NotNull(TemplateSearchCache.FromJObject(jObj, environmentSettings.Host.Logger, null));
        }

        [Fact]
        public async Task CanReadLegacySearchMetadata_FromBlob()
        {
            using EnvironmentSettingsHelper environmentSettingsHelper = new EnvironmentSettingsHelper();
            var environmentSettings = environmentSettingsHelper.CreateEnvironment(virtualize: true);
            var sourceFileProvider = new NuGetMetadataSearchProvider(
                A.Fake<ITemplateSearchProviderFactory>(),
                environmentSettings,
                new Dictionary<string, Func<object, object>>(),
                new[] { "https://go.microsoft.com/fwlink/?linkid=2087906&clcid=0x409" });  //v1 search cache
            await sourceFileProvider.GetSearchFileAsync(default).ConfigureAwait(false);
            string content = environmentSettings.Host.FileSystem.ReadAllText(Path.Combine(environmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json"));
            var jObj = JObject.Parse(content);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.True(LegacySearchCacheReader.TryReadDiscoveryMetadata(jObj, environmentSettings.Host.Logger, null, out _));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Fact]
        public void CanReadSearchMetadata_V2_E2E()
        {
            ITemplateInfo mockTemplate = new MockTemplateInfo("shortName", "Full Name", "test.identity", "test.group.identity", 100, "test author")
                .WithClassifications("test", "asset")
                .WithDescription("my test description")
                .WithTag("language", "CSharp")
                .WithParameters("param1", "param2")
                .WithChoiceParameter("choice", "var1", "var2", "var3");

            TemplateSearchData template = new TemplateSearchData(mockTemplate);

            var mockPackage = A.Fake<ITemplatePackageInfo>();
            A.CallTo(() => mockPackage.Name).Returns("pack");
            A.CallTo(() => mockPackage.Version).Returns("packVer");

            TemplatePackageSearchData package = new TemplatePackageSearchData(mockPackage, new[] { template });
            TemplateSearchCache cache = new TemplateSearchCache(new[] { package });

            JObject jobj = cache.ToJObject();

            TemplateSearchCache deserializedCache = TemplateSearchCache.FromJObject(jobj, NullLogger.Instance);

            Assert.Equal("2.0", deserializedCache.Version);
            Assert.Single(deserializedCache.TemplatePackages);
            Assert.Single(deserializedCache.TemplatePackages[0].Templates);

            Assert.Equal("pack", deserializedCache.TemplatePackages[0].Name);
            Assert.Equal("packVer", deserializedCache.TemplatePackages[0].Version);

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

            Assert.Equal(3, templateToTest.Parameters.Count);

            Assert.Single(templateToTest.Parameters.Where(p => p.DataType == "choice"));
            Assert.Equal(3, templateToTest.Parameters.Single(p => p.DataType == "choice").Choices?.Count);
            Assert.True(templateToTest.Parameters.Single(p => p.DataType == "choice").Choices?.ContainsKey("var1"));
        }
    }
}
