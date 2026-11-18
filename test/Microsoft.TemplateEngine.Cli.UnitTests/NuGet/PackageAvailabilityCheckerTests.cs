// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.TestHelper;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Cli.UnitTests.NuGet
{
    [TestClass]
    public class PackageAvailabilityCheckerTests : BaseTest
    {
        private static readonly PackageAvailabilityCandidate s_candidate = new("Pack.One", "1.0.0");

        [TestMethod]
        public async Task GetAvailablePackagesAsync_NoSources_FailsWithoutQuerying()
        {
            PackageAvailabilityChecker checker = new(sources: Array.Empty<PackageSource>());

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsFalse(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_NoCandidates_IsVacuousSuccess()
        {
            List<string> failures = new();
            PackageSource source = new("https://example.test/index.json", "feed1");
            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                reportFeedFailure: failures.Add,
                resourceFactory: (_, _) => throw new InvalidOperationException("Should not query any feed when there are no candidates."));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                Array.Empty<PackageAvailabilityCandidate>(),
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
            Assert.IsEmpty(failures);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task GetAvailablePackagesAsync_SingleFeed_SucceedsWhetherOrNotPackageIsFound(bool packageFound)
        {
            FindPackageByIdResource resource = CreateResource(packageFound ? ["1.0.0"] : []);
            PackageSource source = new("https://example.test/index.json", "feed1");
            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                resourceFactory: CreateResourceFactory(resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.AreEqual(packageFound, result.AvailablePackages.Contains(s_candidate));
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_UnparseableVersion_IsSkippedWithoutFailingTheFeed()
        {
            PackageAvailabilityCandidate candidate = new("Pack.One", "not-a-version");
            FindPackageByIdResource resource = CreateResource("1.0.0");
            PackageSource source = new("https://example.test/index.json", "feed1");
            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                resourceFactory: CreateResourceFactory(resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task GetAvailablePackagesAsync_FeedFailure_IsReportedAndOtherFeedsStillTried(bool resourceUnavailable)
        {
            List<string> failures = new();
            PackageSource unavailableSource = new("https://example.test/unavailable.json", "unavailable-feed");
            PackageSource workingSource = new("https://example.test/index.json", "working-feed");
            FindPackageByIdResource unavailableResource = A.Fake<FindPackageByIdResource>();
            A.CallTo(() => unavailableResource.GetAllVersionsAsync(
                    A<string>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Throws(new InvalidOperationException("simulated package endpoint failure"));
            FindPackageByIdResource workingResource = CreateResource("1.0.0");

            Dictionary<string, FindPackageByIdResource?> resources = new()
            {
                [unavailableSource.Source] = resourceUnavailable ? null : unavailableResource,
                [workingSource.Source] = workingResource,
            };

            PackageAvailabilityChecker checker = new(
                sources: new[] { unavailableSource, workingSource },
                reportFeedFailure: failures.Add,
                resourceFactory: (source, _) => Task.FromResult(resources[source.Source]));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.Contains(s_candidate, result.AvailablePackages);
            Assert.ContainsSingle(failure => failure.Contains(unavailableSource.Name, StringComparison.Ordinal), failures);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task GetAvailablePackagesAsync_AllQueriesFail_ReturnsFailureAndReportsFeedOnce(bool resourceUnavailable)
        {
            List<string> failures = new();
            FindPackageByIdResource resource = A.Fake<FindPackageByIdResource>();
            A.CallTo(() => resource.GetAllVersionsAsync(
                    A<string>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Throws(new InvalidOperationException("simulated package endpoint failure"));
            PackageSource source = new("https://example.test/index.json", "feed");
            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                reportFeedFailure: failures.Add,
                resourceFactory: CreateResourceFactory(resourceUnavailable ? null : resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[]
                {
                    s_candidate,
                    new PackageAvailabilityCandidate("Pack.Two", "1.0.0"),
                    new PackageAvailabilityCandidate("Pack.Three", "1.0.0"),
                },
                CancellationToken.None);

            Assert.IsFalse(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
            Assert.HasCount(1, failures);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_BoundsConcurrentPackageQueries()
        {
            PackageSource source = new("https://example.test/index.json", "feed");
            BlockingVersionResource blockingResource = new(expectedFirstBatchSize: 4);
            FindPackageByIdResource resource = A.Fake<FindPackageByIdResource>();
            A.CallTo(() => resource.GetAllVersionsAsync(
                    A<string>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .ReturnsLazily(() => blockingResource.GetVersionsAsync());
            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                resourceFactory: CreateResourceFactory(resource));
            PackageAvailabilityCandidate[] candidates = Enumerable.Range(1, 6)
                .Select(index => new PackageAvailabilityCandidate($"Pack.{index}", "1.0.0"))
                .ToArray();

            Task<PackageAvailabilityResult> query = checker.GetAvailablePackagesAsync(
                candidates,
                TestContext.CancellationToken);

            try
            {
                await blockingResource.FirstBatchStarted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
                await Task.Delay(100, TestContext.CancellationToken);
                Assert.AreEqual(4, blockingResource.StartedRequestCount);
            }
            finally
            {
                blockingResource.ReleaseRequests();
            }

            PackageAvailabilityResult result = await query;
            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.AreEqual(4, blockingResource.MaximumConcurrentRequests);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_GroupsVersionsByPackageId()
        {
            PackageAvailabilityCandidate secondVersion = new("Pack.One", "2.0.0");
            FindPackageByIdResource resource = CreateResource("1.0.0", "2.0.0");
            PackageSource source = new("https://example.test/index.json", "feed");

            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                resourceFactory: CreateResourceFactory(resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate, secondVersion },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.Contains(s_candidate, result.AvailablePackages);
            Assert.Contains(secondVersion, result.AvailablePackages);
            A.CallTo(() => resource.GetAllVersionsAsync(
                    "Pack.One", A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        [DataRow("Some.Other.*", false)]
        [DataRow("Pack.*", true)]
        public async Task GetAvailablePackagesAsync_HonorsPackageSourceMapping(string pattern, bool expectedAvailable)
        {
            FindPackageByIdResource resource = CreateResource("1.0.0");
            PackageSource mappedSource = new("https://example.test/mapped.json", "mapped-feed");
            PackageAvailabilityChecker checker = new(
                sources: new[] { mappedSource },
                sourceMapping: PackageSourceMapping.GetPackageSourceMapping(CreateSourceMappingSettings(pattern)),
                resourceFactory: CreateResourceFactory(resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.AreEqual(expectedAvailable, result.AvailablePackages.Contains(s_candidate));
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_UnmappedFeedDoesNotMaskMappedFeedFailure()
        {
            FindPackageByIdResource resource = A.Fake<FindPackageByIdResource>();
            A.CallTo(() => resource.GetAllVersionsAsync(
                    A<string>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Throws(new InvalidOperationException("simulated package endpoint failure"));
            PackageAvailabilityChecker checker = new(
                sources:
                [
                    new("https://example.test/mapped.json", "mapped-feed"),
                    new("https://example.test/unmapped.json", "unmapped-feed"),
                ],
                sourceMapping: PackageSourceMapping.GetPackageSourceMapping(CreateSourceMappingSettings()),
                resourceFactory: CreateResourceFactory(resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsFalse(result.AnyFeedSucceeded);
        }

        [TestMethod]
        public void GetEffectivePackageSourceMapping_SourceOverridesBypassMapping()
        {
            ISettings settings = CreateSourceMappingSettings();

            PackageSourceMapping? mapping = PackageAvailabilityChecker.GetEffectivePackageSourceMapping(
                settings,
                sourceOverridesSpecified: true,
                additionalSourcesSpecified: true);

            Assert.IsNull(mapping);
        }

        [TestMethod]
        public void GetEffectivePackageSourceMapping_AdditionalSourceWithoutOverrideIsRejected()
        {
            ISettings settings = CreateSourceMappingSettings();

            GracefulException exception = Assert.ThrowsExactly<GracefulException>(() =>
                PackageAvailabilityChecker.GetEffectivePackageSourceMapping(
                    settings,
                    sourceOverridesSpecified: false,
                    additionalSourcesSpecified: true));

            Assert.Contains("--add-source", exception.Message);
        }

        private static ISettings CreateSourceMappingSettings(string pattern = "Pack.*")
        {
            string directory = TestUtils.CreateTemporaryFolder("packageSourceMapping");
            File.WriteAllText(
                Path.Combine(directory, "NuGet.Config"),
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <add key="mapped-feed" value="https://example.test/mapped.json" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="mapped-feed">
                      <package pattern="{pattern}" />
                    </packageSource>
                  </packageSourceMapping>
                </configuration>
                """);
            return Settings.LoadSpecificSettings(directory, "NuGet.Config");
        }

        private static FindPackageByIdResource CreateResource(params string[] versions)
        {
            FindPackageByIdResource resource = A.Fake<FindPackageByIdResource>();
            A.CallTo(() => resource.GetAllVersionsAsync(
                    A<string>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Returns(Task.FromResult<IEnumerable<NuGetVersion>>(versions.Select(NuGetVersion.Parse)));
            return resource;
        }

        private static Func<PackageSource, CancellationToken, Task<FindPackageByIdResource?>> CreateResourceFactory(
            FindPackageByIdResource? resource) => (_, _) => Task.FromResult(resource);

        private sealed class BlockingVersionResource(int expectedFirstBatchSize)
        {
            private readonly TaskCompletionSource<bool> _firstBatchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _releaseRequests = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _activeRequests;
            private int _maximumConcurrentRequests;
            private int _startedRequestCount;

            internal Task FirstBatchStarted => _firstBatchStarted.Task;

            internal int MaximumConcurrentRequests => Volatile.Read(ref _maximumConcurrentRequests);

            internal int StartedRequestCount => Volatile.Read(ref _startedRequestCount);

            internal async Task<IEnumerable<NuGetVersion>> GetVersionsAsync()
            {
                int activeRequests = Interlocked.Increment(ref _activeRequests);
                int observedMaximum = Volatile.Read(ref _maximumConcurrentRequests);
                while (activeRequests > observedMaximum)
                {
                    int previousMaximum = Interlocked.CompareExchange(
                        ref _maximumConcurrentRequests,
                        activeRequests,
                        observedMaximum);
                    if (previousMaximum == observedMaximum)
                    {
                        break;
                    }
                    observedMaximum = previousMaximum;
                }

                if (Interlocked.Increment(ref _startedRequestCount) == expectedFirstBatchSize)
                {
                    _firstBatchStarted.SetResult(true);
                }

                try
                {
                    await _releaseRequests.Task;
                    return [];
                }
                finally
                {
                    Interlocked.Decrement(ref _activeRequests);
                }
            }

            internal void ReleaseRequests() => _releaseRequests.SetResult(true);
        }
    }
}
