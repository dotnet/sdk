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

        [TestMethod]
        public async Task InternalTimeoutIsReportedAsRetriableFailure()
        {
            Func<Task> action = async () => await NugetToolSearchApiRequest.ExecuteWithTimeout(
                async token =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return 0;
                },
                TestContext.CancellationToken,
                TimeSpan.FromMilliseconds(10));

            NugetSearchApiRequestException exception =
                await Assert.ThrowsExactlyAsync<NugetSearchApiRequestException>(action);

            exception.Message.Should().Contain("Retry");
        }

        [TestMethod]
        public async Task CallerCancellationIsNotReportedAsTimeout()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Func<Task> action = async () => await NugetToolSearchApiRequest.ExecuteWithTimeout(
                async token =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return 0;
                },
                cancellation.Token,
                TimeSpan.FromSeconds(1));

            await Assert.ThrowsExactlyAsync<TaskCanceledException>(action);
        }

        [TestMethod]
        public async Task UnrelatedCancellationIsNotReportedAsTimeout()
        {
            Func<Task> action = async () => await NugetToolSearchApiRequest.ExecuteWithTimeout<int>(
                _ => throw new OperationCanceledException(),
                TestContext.CancellationToken,
                TimeSpan.FromSeconds(1));

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(action);
        }

        [TestMethod]
        public async Task QueryTimeoutStartsAfterEndpointDiscoveryCompletes()
        {
            TimeSpan timeout = TimeSpan.FromMilliseconds(50);

            string result = await NugetToolSearchApiRequest.ExecuteRequest(
                async _ =>
                {
                    await Task.Delay(
                        timeout + TimeSpan.FromMilliseconds(50),
                        TestContext.CancellationToken);
                    return _domainAndPathOverride;
                },
                (_, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult("result");
                },
                TestContext.CancellationToken,
                timeout);

            result.Should().Be("result");
        }
    }
}
