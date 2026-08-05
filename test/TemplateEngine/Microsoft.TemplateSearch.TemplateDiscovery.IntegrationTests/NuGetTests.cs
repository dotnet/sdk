// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.TemplateSearch.TemplateDiscovery.NuGet;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Microsoft.TemplateSearch.TemplateDiscovery.IntegrationTests
{
    [TestClass]
    public class NuGetTests
    {
        [TestMethod]
        public async Task CanReadPackageInfo()
        {
            string nuGetOrgFeed = "https://api.nuget.org/v3/index.json";
            var repository = Repository.Factory.GetCoreV3(nuGetOrgFeed);
            ServiceIndexResourceV3 indexResource = repository.GetResource<ServiceIndexResourceV3>(TestContext.Current!.CancellationToken)
                ?? throw new InvalidOperationException("Failed to get ServiceIndexResourceV3.");
            IReadOnlyList<ServiceIndexEntry> searchResources = indexResource.GetServiceEntries("SearchQueryService");
            string queryString = $"{searchResources[0].Uri}?q=Microsoft.DotNet.Common.ProjectTemplates.5.0&skip=0&take=10&prerelease=true&semVerLevel=2.0.0";
            Uri queryUri = new Uri(queryString);
            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(queryUri, TestContext.Current!.CancellationToken);
            if (response.IsSuccessStatusCode)
            {
                string responseText = await response.Content.ReadAsStringAsync(TestContext.Current!.CancellationToken);

                NuGetPackageSearchResult resultsForPage = NuGetPackageSearchResult.FromJObject(JsonNode.Parse(responseText)!.AsObject());
                Assert.AreEqual(1, resultsForPage.TotalHits);
                Assert.ContainsSingle(resultsForPage.Data);

                var packageInfo = resultsForPage.Data[0];

                Assert.AreEqual("Microsoft.DotNet.Common.ProjectTemplates.5.0", packageInfo.Name);
                Assert.IsNotEmpty(packageInfo.Version);
                Assert.IsGreaterThan(0, packageInfo.TotalDownloads);
                Assert.IsTrue(packageInfo.Reserved);
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
