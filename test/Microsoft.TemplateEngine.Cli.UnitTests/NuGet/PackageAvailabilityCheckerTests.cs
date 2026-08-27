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
                repositoryFactory: _ => throw new InvalidOperationException("Should not query any feed when there are no candidates."));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                Array.Empty<PackageAvailabilityCandidate>(),
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
            Assert.IsEmpty(failures);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_SingleFeed_PackageFound_IsAvailable()
        {
            FindPackageByIdResource resource = CreateResource("1.0.0");
            PackageSource source = new("https://example.test/index.json", "feed1");
            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                repositoryFactory: _ => CreateRepository(source, resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.Contains(s_candidate, result.AvailablePackages);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_SingleFeed_PackageNotFound_FeedStillSucceeds()
        {
            FindPackageByIdResource resource = CreateResource();
            PackageSource source = new("https://example.test/index.json", "feed1");
            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                repositoryFactory: _ => CreateRepository(source, resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_UnparseableVersion_IsSkippedWithoutFailingTheFeed()
        {
            PackageAvailabilityCandidate candidate = new("Pack.One", "not-a-version");
            FindPackageByIdResource resource = CreateResource("1.0.0");
            PackageSource source = new("https://example.test/index.json", "feed1");
            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                repositoryFactory: _ => CreateRepository(source, resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_FeedResourceUnavailable_IsReportedAndOtherFeedsStillTried()
        {
            List<string> failures = new();
            PackageSource unavailableSource = new("https://example.test/unavailable.json", "unavailable-feed");
            PackageSource workingSource = new("https://example.test/index.json", "working-feed");
            FindPackageByIdResource workingResource = CreateResource("1.0.0");

            Dictionary<string, SourceRepository> repositories = new()
            {
                [unavailableSource.Source] = CreateRepository(unavailableSource, resource: null),
                [workingSource.Source] = CreateRepository(workingSource, workingResource),
            };

            PackageAvailabilityChecker checker = new(
                sources: new[] { unavailableSource, workingSource },
                reportFeedFailure: failures.Add,
                repositoryFactory: source => repositories[source.Source]);

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.Contains(s_candidate, result.AvailablePackages);
            Assert.ContainsSingle(failure => failure.Contains(unavailableSource.Name, StringComparison.Ordinal), failures);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_FeedQueryThrows_IsReportedAndOtherFeedsStillTried()
        {
            List<string> failures = new();
            FindPackageByIdResource throwingResource = A.Fake<FindPackageByIdResource>();
            A.CallTo(() => throwingResource.GetAllVersionsAsync(
                    A<string>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Throws(new InvalidOperationException("simulated feed failure"));

            PackageSource throwingSource = new("https://example.test/throws.json", "throwing-feed");
            PackageSource workingSource = new("https://example.test/index.json", "working-feed");
            FindPackageByIdResource workingResource = CreateResource("1.0.0");

            Dictionary<string, SourceRepository> repositories = new()
            {
                [throwingSource.Source] = CreateRepository(throwingSource, throwingResource),
                [workingSource.Source] = CreateRepository(workingSource, workingResource),
            };

            PackageAvailabilityChecker checker = new(
                sources: new[] { throwingSource, workingSource },
                reportFeedFailure: failures.Add,
                repositoryFactory: source => repositories[source.Source]);

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.Contains(s_candidate, result.AvailablePackages);
            Assert.ContainsSingle(failure => failure.Contains("simulated feed failure", StringComparison.Ordinal), failures);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_AllFeedsFail_ReturnsFailure()
        {
            List<string> failures = new();
            PackageSource sourceOne = new("https://example.test/one.json", "feed-one");
            PackageSource sourceTwo = new("https://example.test/two.json", "feed-two");

            Dictionary<string, SourceRepository> repositories = new()
            {
                [sourceOne.Source] = CreateRepository(sourceOne, resource: null),
                [sourceTwo.Source] = CreateRepository(sourceTwo, resource: null),
            };

            PackageAvailabilityChecker checker = new(
                sources: new[] { sourceOne, sourceTwo },
                reportFeedFailure: failures.Add,
                repositoryFactory: source => repositories[source.Source]);

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsFalse(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
            Assert.HasCount(2, failures);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_AllPackageQueriesFail_ReturnsFailure()
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
                repositoryFactory: _ => CreateRepository(source, resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsFalse(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
            Assert.ContainsSingle(failure => failure.Contains("simulated package endpoint failure", StringComparison.Ordinal), failures);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_ReportsEachFailingFeedOnce()
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
                repositoryFactory: _ => CreateRepository(source, resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[]
                {
                    s_candidate,
                    new PackageAvailabilityCandidate("Pack.Two", "1.0.0"),
                    new PackageAvailabilityCandidate("Pack.Three", "1.0.0"),
                },
                CancellationToken.None);

            Assert.IsFalse(result.AnyFeedSucceeded);
            Assert.HasCount(1, failures);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_ManyVersionsQueryEachFeedAndPackageIdOnce()
        {
            PackageSource[] sources =
            [
                new("https://example.test/one.json", "feed-one"),
                new("https://example.test/two.json", "feed-two"),
                new("https://example.test/three.json", "feed-three"),
            ];
            FindPackageByIdResource[] resources =
            [
                CreateResource("1.0.0"),
                CreateResource("1.0.0"),
                CreateResource("1.0.0"),
            ];
            Dictionary<string, SourceRepository> repositories = sources
                .Select((source, index) => (source, repository: CreateRepository(source, resources[index])))
                .ToDictionary(item => item.source.Source, item => item.repository);
            PackageAvailabilityCandidate[] candidates = Enumerable.Range(1, 100)
                .SelectMany(version => new[]
                {
                    new PackageAvailabilityCandidate("Pack.One", $"1.0.{version}"),
                    new PackageAvailabilityCandidate("Pack.Two", $"1.0.{version}"),
                })
                .ToArray();
            PackageAvailabilityChecker checker = new(
                sources,
                repositoryFactory: source => repositories[source.Source]);

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                candidates,
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            foreach (FindPackageByIdResource resource in resources)
            {
                A.CallTo(() => resource.GetAllVersionsAsync(
                        A<string>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                    .MustHaveHappened(2, Times.Exactly);
            }
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
                repositoryFactory: _ => CreateRepository(source, resource));
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
                repositoryFactory: _ => CreateRepository(source, resource));

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
        public async Task GetAvailablePackagesAsync_PackageSourceMapping_ExcludesCandidateWithNoMatchingPattern()
        {
            string directory = TestUtils.CreateTemporaryFolder("packageSourceMapping");
            File.WriteAllText(
                Path.Combine(directory, "NuGet.Config"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <add key="mapped-feed" value="https://example.test/mapped.json" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="mapped-feed">
                      <package pattern="Some.Other.*" />
                    </packageSource>
                  </packageSourceMapping>
                </configuration>
                """);
            ISettings settings = Settings.LoadSpecificSettings(directory, "NuGet.Config");

            FindPackageByIdResource resource = A.Fake<FindPackageByIdResource>();
            A.CallTo(() => resource.GetAllVersionsAsync(
                    A<string>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Throws(new InvalidOperationException("The mapped feed does not own this package id and should not be queried."));

            PackageSource mappedSource = new("https://example.test/mapped.json", "mapped-feed");
            PackageAvailabilityChecker checker = new(
                sources: new[] { mappedSource },
                sourceMapping: PackageSourceMapping.GetPackageSourceMapping(settings),
                repositoryFactory: _ => CreateRepository(mappedSource, resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_PackageSourceMapping_IncludesCandidateWithMatchingPattern()
        {
            string directory = TestUtils.CreateTemporaryFolder("packageSourceMapping");
            File.WriteAllText(
                Path.Combine(directory, "NuGet.Config"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <add key="mapped-feed" value="https://example.test/mapped.json" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="mapped-feed">
                      <package pattern="Pack.*" />
                    </packageSource>
                  </packageSourceMapping>
                </configuration>
                """);
            ISettings settings = Settings.LoadSpecificSettings(directory, "NuGet.Config");

            FindPackageByIdResource resource = CreateResource("1.0.0");
            PackageSource mappedSource = new("https://example.test/mapped.json", "mapped-feed");
            PackageAvailabilityChecker checker = new(
                sources: new[] { mappedSource },
                sourceMapping: PackageSourceMapping.GetPackageSourceMapping(settings),
                repositoryFactory: _ => CreateRepository(mappedSource, resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.Contains(s_candidate, result.AvailablePackages);
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

        private static ISettings CreateSourceMappingSettings()
        {
            string directory = TestUtils.CreateTemporaryFolder("packageSourceMapping");
            File.WriteAllText(
                Path.Combine(directory, "NuGet.Config"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <add key="mapped-feed" value="https://example.test/mapped.json" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="mapped-feed">
                      <package pattern="Pack.*" />
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

        private static SourceRepository CreateRepository(PackageSource source, FindPackageByIdResource? resource)
        {
            return new SourceRepository(source, new[] { new Lazy<INuGetResourceProvider>(() => new StubResourceProvider(resource)) });
        }

        private sealed class StubResourceProvider : INuGetResourceProvider
        {
            private readonly FindPackageByIdResource? _resource;

            internal StubResourceProvider(FindPackageByIdResource? resource)
            {
                _resource = resource;
            }

            public Type ResourceType => typeof(FindPackageByIdResource);

            public string Name => nameof(StubResourceProvider);

            public IEnumerable<string> Before => Array.Empty<string>();

            public IEnumerable<string> After => Array.Empty<string>();

            public Task<Tuple<bool, INuGetResource?>> TryCreate(SourceRepository source, CancellationToken token)
            {
                return Task.FromResult(new Tuple<bool, INuGetResource?>(_resource != null, _resource));
            }
        }

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
