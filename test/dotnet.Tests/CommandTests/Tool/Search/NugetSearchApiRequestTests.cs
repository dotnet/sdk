// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.NugetSearch;

namespace dotnet.Tests.ToolSearchTests
{
    [TestClass]
    public class NugetSearchApiRequestTests
    {
        private readonly Uri _domainAndPathOverride = new("https://azuresearch-usnc.nuget.org/query");

        [TestMethod]
        public async Task WhenPassedInRequestParametersItCanConstructTheUrl()
        {
            (await NugetToolSearchApiRequest.ConstructUrl("mytool", 3, 4, true, _domainAndPathOverride))
                .AbsoluteUri
                .Should().Be(
                    "https://azuresearch-usnc.nuget.org/query?q=mytool&packageType=dotnettool&semVerLevel=2.0.0&skip=3&take=4&prerelease=true");
        }

        [TestMethod]
        public async Task WhenPassedWithoutParameterItCanConstructTheUrl()
        {
            (await NugetToolSearchApiRequest.ConstructUrl(domainAndPathOverride: _domainAndPathOverride))
                .AbsoluteUri
                .Should().Be(
                    "https://azuresearch-usnc.nuget.org/query?packageType=dotnettool&semVerLevel=2.0.0");
        }

        [TestMethod]
        public async Task WhenDomainAndPathOverrideAndServiceIndexUrlAreBothSuppliedTheOverrideTakesPrecedence()
        {
            // serviceIndexUrl points at a source that would require a network call to resolve; the override
            // should be used instead so this test stays fully offline/deterministic.
            (await NugetToolSearchApiRequest.ConstructUrl(
                "mytool",
                domainAndPathOverride: _domainAndPathOverride,
                serviceIndexUrl: "https://example.invalid/v3/index.json"))
                .AbsoluteUri
                .Should().Be(
                    "https://azuresearch-usnc.nuget.org/query?q=mytool&packageType=dotnettool&semVerLevel=2.0.0");
        }

        // Nothing listens on loopback port 1, so connecting there fails immediately (connection refused)
        // without any DNS lookup or timeout delay, keeping these tests fast and offline-safe while still
        // exercising a real network failure end-to-end.
        private const string UnreachableServiceIndexUrl = "http://127.0.0.1:1/v3/index.json";

        [TestMethod]
        public async Task WhenServiceIndexResolutionFailsWithARawNetworkExceptionConstructUrlTranslatesItToNugetSearchApiRequestException()
        {
            // Regression test: the managed (non-AOT) DomainAndPath used to let raw NuGet.Protocol /
            // HttpRequestException failures escape uncaught. ToolSearchCommand's per-feed loop only
            // catches NugetSearchApiRequestException, so an untranslated exception here would abort
            // the entire multi-feed search instead of being reported as a single failed feed.
            Func<Task> act = () => NugetToolSearchApiRequest.ConstructUrl(serviceIndexUrl: UnreachableServiceIndexUrl);

            await act.Should().ThrowAsync<NugetSearchApiRequestException>();
        }

        [TestMethod]
        public async Task WhenTheSourceIsUnreachableGetResultThrowsNugetSearchApiRequestExceptionNotARawException()
        {
            // Regression test for the public entry point ToolSearchCommand relies on: GetResult must
            // never leak a raw HttpRequestException/NuGet.Protocol exception for an unreachable source.
            INugetToolSearchApiRequest request = new NugetToolSearchApiRequest();
            Func<Task> act = () => request.GetResult(new NugetSearchApiParameter("mytool"), UnreachableServiceIndexUrl);

            await act.Should().ThrowAsync<NugetSearchApiRequestException>();
        }
    }
}
