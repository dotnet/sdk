// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.NugetSearch;

namespace dotnet.Tests.ToolSearchTests
{
    [TestClass]
    public class NugetSearchApiRequestTests
    {
        private readonly Uri _domainAndPathOverride = new("https://azuresearch-usnc.nuget.org/query");

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task WhenPassedInRequestParametersItCanConstructTheUrl()
        {
            (await NugetToolSearchApiRequest.ConstructUrl("mytool", 3, 4, true, TestContext.CancellationToken, _domainAndPathOverride))
                .AbsoluteUri
                .Should().Be(
                    "https://azuresearch-usnc.nuget.org/query?q=mytool&packageType=dotnettool&semVerLevel=2.0.0&skip=3&take=4&prerelease=true");
        }

        [TestMethod]
        public async Task WhenPassedWithoutParameterItCanConstructTheUrl()
        {
            (await NugetToolSearchApiRequest.ConstructUrl(
                cancellationToken: TestContext.CancellationToken,
                domainAndPathOverride: _domainAndPathOverride))
                .AbsoluteUri
                .Should().Be(
                    "https://azuresearch-usnc.nuget.org/query?packageType=dotnettool&semVerLevel=2.0.0");
        }
    }
}
