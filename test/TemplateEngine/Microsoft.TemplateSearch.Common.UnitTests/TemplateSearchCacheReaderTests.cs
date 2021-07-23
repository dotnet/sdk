// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.TestHelper;
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
    }
}
