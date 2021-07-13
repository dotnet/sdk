// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateSearch.Common.UnitTests
{
    public class FileMetadataTemplateSearchCacheReaderTests
    {
        [Fact]
        public void CanReadSearchMetadata()
        {
            using EnvironmentSettingsHelper environmentSettingsHelper = new EnvironmentSettingsHelper();
            string content = File.ReadAllText("NuGetTemplateSearchInfo.json");
            var config = new NuGetSearchCacheConfig("NuGetTemplateSearchInfo.json");
            Assert.True(FileMetadataTemplateSearchCacheReader.TryReadDiscoveryMetadata(
                environmentSettingsHelper.CreateEnvironment(virtualize: true),
                content,
                config,
                out TemplateDiscoveryMetadata discoveryMetadata));
            Assert.Equal(2, discoveryMetadata.TemplateCache.Count);
            Assert.Equal(1, discoveryMetadata.PackToTemplateMap.Count);

            Assert.IsType<BlobStorageTemplateInfo>(discoveryMetadata.TemplateCache[0]);

            //can read tags
            Assert.Equal(2, discoveryMetadata.TemplateCache[0].TagsCollection.Count);

            //can read parameters: 2 tags + 3 cache parameters
            Assert.Equal(5, discoveryMetadata.TemplateCache[0].Parameters.Count);
        }

        [Fact]
        public void CanReadSearchMetadata_V2()
        {
            using EnvironmentSettingsHelper environmentSettingsHelper = new EnvironmentSettingsHelper();
            string content = File.ReadAllText("NuGetTemplateSearchInfo_v2.json");
            var config = new NuGetSearchCacheConfig("NuGetTemplateSearchInfo_v2.json");
            Assert.True(FileMetadataTemplateSearchCacheReader.TryReadDiscoveryMetadata(
                environmentSettingsHelper.CreateEnvironment(virtualize: true),
                content,
                config,
                out TemplateDiscoveryMetadata discoveryMetadata));
        }

        [Fact]
        public void CanSkipInvalidEntriesSearchMetadata()
        {
            using EnvironmentSettingsHelper environmentSettingsHelper = new EnvironmentSettingsHelper();
            string content = File.ReadAllText("NuGetTemplateSearchInfoWithInvalidData.json");
            var config = new NuGetSearchCacheConfig("NuGetTemplateSearchInfoWithInvalidData.json");
            Assert.True(FileMetadataTemplateSearchCacheReader.TryReadDiscoveryMetadata(
                environmentSettingsHelper.CreateEnvironment(virtualize: true),
                content,
                config,
                out TemplateDiscoveryMetadata discoveryMetadata));
            Assert.Equal(1, discoveryMetadata.TemplateCache.Count);
            Assert.Equal(1, discoveryMetadata.PackToTemplateMap.Count);

            Assert.IsType<BlobStorageTemplateInfo>(discoveryMetadata.TemplateCache[0]);
            Assert.Equal("Microsoft.AzureFunctions.ProjectTemplate.CSharp.3.x", discoveryMetadata.TemplateCache[0].Identity);
        }
    }
}
