// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.NugetSearch;

namespace dotnet.Tests.ToolSearchTests
{
    public class NugetSearchApiRequestTests
    {
        private readonly Uri _domainAndPathOverride = new("https://azuresearch-usnc.nuget.org/query");

        [Fact]
        public async Task WhenPassedInRequestParametersItCanConstructTheUrl()
        {
            (await NugetToolSearchApiRequest.ConstructUrl("mytool", 3, 4, true, _domainAndPathOverride))
                .AbsoluteUri
                .Should().Be(
                    "https://azuresearch-usnc.nuget.org/query?q=mytool&packageType=dotnettool&semVerLevel=2.0.0&skip=3&take=4&prerelease=true");
        }

        [Fact]
        public async Task WhenPassedWithoutParameterItCanConstructTheUrl()
        {
            (await NugetToolSearchApiRequest.ConstructUrl(domainAndPathOverride: _domainAndPathOverride))
                .AbsoluteUri
                .Should().Be(
                    "https://azuresearch-usnc.nuget.org/query?packageType=dotnettool&semVerLevel=2.0.0");
        }
    }
}
