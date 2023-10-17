// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateSearch.TemplateDiscovery.NuGet;
using Newtonsoft.Json.Linq;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace Microsoft.TemplateSearch.TemplateDiscovery.IntegrationTests
{
    public class NuGetTests
    {
        [Fact]
        public async Task CanReadPackageInfo()
        {
            string nuGetOrgFeed = "https://api.nuget.org/v3/index.json";
            var repository = Repository.Factory.GetCoreV3(nuGetOrgFeed);
            ServiceIndexResourceV3 indexResource = repository.GetResource<ServiceIndexResourceV3>();
            IReadOnlyList<ServiceIndexEntry> searchResources = indexResource.GetServiceEntries("SearchQueryService");
            string queryString = $"{searchResources[0].Uri}?q=Microsoft.DotNet.Common.ProjectTemplates.5.0&skip=0&take=10&prerelease=true&semVerLevel=2.0.0";
            Uri queryUri = new Uri(queryString);
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(queryUri, CancellationToken.None))
            {
                if (response.IsSuccessStatusCode)
                {
                    string responseText = await response.Content.ReadAsStringAsync(CancellationToken.None);

                    NuGetPackageSearchResult resultsForPage = NuGetPackageSearchResult.FromJObject(JObject.Parse(responseText));
                    Assert.Equal(1, resultsForPage.TotalHits);
                    Assert.Single(resultsForPage.Data);

                    var packageInfo = resultsForPage.Data[0];

                    Assert.Equal("Microsoft.DotNet.Common.ProjectTemplates.5.0", packageInfo.Name);
                    Assert.NotEmpty(packageInfo.Version);
                    Assert.True(packageInfo.TotalDownloads > 0);
                    Assert.True(packageInfo.Reserved);
                    Assert.Contains("Microsoft", packageInfo.Owners);
                    packageInfo.Description.Should().NotBeNullOrEmpty();
                    packageInfo.IconUrl.Should().NotBeNullOrEmpty();
                }
                else
                {
                    Assert.Fail("HTTP request failed.");
                }
            }
        }
    }
}
