// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Xunit;

namespace Microsoft.TemplateSearch.Common.UnitTests
{
    public class FileMetadataTemplateSearchCacheReaderTests
    {
        [Fact]
        public void CanReadSearchMetadata()
        {
            string content = File.ReadAllText("NuGetTemplateSearchInfo.json");
            var config = new NuGetSearchCacheConfig("NuGetTemplateSearchInfo.json");
            Assert.True(FileMetadataTemplateSearchCacheReader.TryReadDiscoveryMetadata(content, config, out TemplateDiscoveryMetadata discoveryMetadata));
            Assert.Equal(2, discoveryMetadata.TemplateCache.Count);
            Assert.Equal(1, discoveryMetadata.PackToTemplateMap.Count);

            Assert.IsType<BlobStorageTemplateInfo>(discoveryMetadata.TemplateCache[0]);

            //can read tags
            Assert.Equal(2, discoveryMetadata.TemplateCache[0].Tags.Count);

            //can read parameters: 2 tags + 3 cache parameters
            Assert.Equal(5, discoveryMetadata.TemplateCache[0].Parameters.Count);
        }
    }
}
