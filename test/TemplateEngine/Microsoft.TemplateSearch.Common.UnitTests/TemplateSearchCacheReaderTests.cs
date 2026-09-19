// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.Common.Providers;

namespace Microsoft.TemplateSearch.Common.UnitTests
{
    [TestClass]
    public class TemplateSearchCacheReaderTests
    {
        private static readonly Lazy<EnvironmentSettingsHelper> s_environmentSettingsHelper =
            new(() => new EnvironmentSettingsHelper());

        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public TemplateSearchCacheReaderTests()
        {
            _environmentSettingsHelper = s_environmentSettingsHelper.Value;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (s_environmentSettingsHelper.IsValueCreated)
            {
                s_environmentSettingsHelper.Value.Dispose();
            }
        }

        [TestMethod]
        public void CanReadSearchMetadata()
        {
            var environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string content = File.ReadAllText("NuGetTemplateSearchInfo.json");
            JsonObject cache = JExtensions.ParseJsonObject(content);

            var parsedCache = TemplateSearchCache.FromJObject(cache, environmentSettings.Host.Logger);

            Assert.ContainsSingle(parsedCache.TemplatePackages);
            Assert.AreEqual(2, parsedCache.TemplatePackages.Sum(p => p.Templates.Count));

            Assert.IsInstanceOfType<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);

            //can read tags
            Assert.HasCount(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).TagsCollection);

            //can read parameters
            Assert.HasCount(5, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).ParameterDefinitions);
        }

        [TestMethod]
        public void CanReadSearchMetadata_V2()
        {
            var environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string content = File.ReadAllText("NuGetTemplateSearchInfo_v2.json");
            JsonObject cache = JExtensions.ParseJsonObject(content);

            var parsedCache = TemplateSearchCache.FromJObject(cache, environmentSettings.Host.Logger);

            Assert.ContainsSingle(parsedCache.TemplatePackages);
            Assert.AreEqual(3, parsedCache.TemplatePackages.Sum(p => p.Templates.Count));

            Assert.IsInstanceOfType<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);

            //can read tags
            Assert.HasCount(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).TagsCollection);

            //can read parameters: 2 tags + 3 cache parameters
            Assert.HasCount(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).ParameterDefinitions);

            Assert.HasCount(3, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[2]).ParameterDefinitions);
            Assert.ContainsSingle(((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[2]).ParameterDefinitions.Where(p => p.DataType == "choice"));
            Assert.AreEqual(3, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[2]).ParameterDefinitions.Single(p => p.DataType == "choice").Choices?.Count);
        }

        [TestMethod]
        public void CanSkipInvalidEntriesSearchMetadata()
        {
            var environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            string content = File.ReadAllText("NuGetTemplateSearchInfoWithInvalidData.json");
            JsonObject cache = JExtensions.ParseJsonObject(content);

            var parsedCache = TemplateSearchCache.FromJObject(cache, environmentSettings.Host.Logger);

            Assert.ContainsSingle(parsedCache.TemplatePackages);
            Assert.AreEqual(1, parsedCache.TemplatePackages.Sum(p => p.Templates.Count));

            Assert.IsInstanceOfType<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);
            Assert.AreEqual("Microsoft.AzureFunctions.ProjectTemplate.CSharp.3.x", ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).Identity);
        }

        [TestMethod]
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
            var jObj = JExtensions.ParseJsonObject(content);
            Assert.IsNotNull(TemplateSearchCache.FromJObject(jObj, environmentSettings.Host.Logger, null));
        }

        [TestMethod]
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
            var jObj = JExtensions.ParseJsonObject(content);
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.IsTrue(LegacySearchCacheReader.TryReadDiscoveryMetadata(jObj, environmentSettings.Host.Logger, null, out _));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [TestMethod]
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

            JsonObject jobj = cache.ToJObject();

            TemplateSearchCache deserializedCache = TemplateSearchCache.FromJObject(jobj, NullLogger.Instance);

            Assert.AreEqual("2.0", deserializedCache.Version);
            Assert.ContainsSingle(deserializedCache.TemplatePackages);
            Assert.ContainsSingle(deserializedCache.TemplatePackages[0].Templates);

            Assert.AreEqual("pack", deserializedCache.TemplatePackages[0].Name);
            Assert.AreEqual("packVer", deserializedCache.TemplatePackages[0].Version);
            Assert.AreEqual("description", deserializedCache.TemplatePackages[0].Description);
            Assert.AreEqual("https://icon", deserializedCache.TemplatePackages[0].IconUrl);

            var templateToTest = deserializedCache.TemplatePackages[0].Templates[0];

            Assert.AreEqual("shortName", templateToTest.ShortNameList[0]);
            Assert.AreEqual("Full Name", templateToTest.Name);
            Assert.AreEqual("test.identity", templateToTest.Identity);
            Assert.AreEqual("test.group.identity", templateToTest.GroupIdentity);
            Assert.AreEqual(100, templateToTest.Precedence);
            Assert.AreEqual("test author", templateToTest.Author);
            Assert.HasCount(2, templateToTest.Classifications);

            Assert.AreEqual("my test description", templateToTest.Description);
            Assert.AreEqual("CSharp", templateToTest.TagsCollection["language"]);

            Assert.HasCount(3, templateToTest.ParameterDefinitions);

            Assert.ContainsSingle(p => p.DataType == "choice", templateToTest.ParameterDefinitions);
            Assert.AreEqual(3, templateToTest.ParameterDefinitions.Single(p => p.DataType == "choice").Choices?.Count);
            Assert.IsTrue(templateToTest.ParameterDefinitions.Single(p => p.DataType == "choice").Choices?.ContainsKey("var1"));

            Assert.HasCount(2, ((ITemplateInfo)templateToTest).PostActions);
            Assert.AreSequenceEqual(new[] { postAction1, postAction2 }, ((ITemplateInfo)templateToTest).PostActions);
        }
    }
}
