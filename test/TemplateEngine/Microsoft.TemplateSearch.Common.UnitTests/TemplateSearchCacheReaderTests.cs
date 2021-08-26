// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
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
            Assert.Equal(2, parsedCache.TemplatePackages.Sum(p => p.Templates.Count));

            Assert.IsAssignableFrom<ITemplateInfo>(parsedCache.TemplatePackages[0].Templates[0]);

            //can read tags
            Assert.Equal(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).TagsCollection.Count);

            //can read parameters: 2 tags + 3 cache parameters
            Assert.Equal(2, ((ITemplateInfo)parsedCache.TemplatePackages[0].Templates[0]).Parameters.Count);
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
    }
}
