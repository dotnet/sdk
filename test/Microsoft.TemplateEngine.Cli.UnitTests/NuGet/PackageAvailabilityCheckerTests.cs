// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
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
                // The candidates set is empty, so no feed should ever need to be queried.
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
            FindPackageByIdResource resource = CreateResource(exists: true);
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
            FindPackageByIdResource resource = CreateResource(exists: false);
            PackageSource source = new("https://example.test/index.json", "feed1");
            PackageAvailabilityChecker checker = new(
                sources: new[] { source },
                repositoryFactory: _ => CreateRepository(source, resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            // A feed that successfully reports "no such package" is still a successful feed query.
            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.IsEmpty(result.AvailablePackages);
        }

        [TestMethod]
        public async Task GetAvailablePackagesAsync_UnparseableVersion_IsSkippedWithoutFailingTheFeed()
        {
            PackageAvailabilityCandidate candidate = new("Pack.One", "not-a-version");
            FindPackageByIdResource resource = CreateResource(exists: true);
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
            FindPackageByIdResource workingResource = CreateResource(exists: true);

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
            A.CallTo(() => throwingResource.DoesPackageExistAsync(
                    A<string>._, A<NuGetVersion>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Throws(new InvalidOperationException("simulated feed failure"));

            PackageSource throwingSource = new("https://example.test/throws.json", "throwing-feed");
            PackageSource workingSource = new("https://example.test/index.json", "working-feed");
            FindPackageByIdResource workingResource = CreateResource(exists: true);

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
        public async Task GetAvailablePackagesAsync_FirstAvailableFeedShortCircuitsLaterFeeds()
        {
            FindPackageByIdResource firstResource = CreateResource(exists: true);
            FindPackageByIdResource secondResource = A.Fake<FindPackageByIdResource>();
            A.CallTo(() => secondResource.DoesPackageExistAsync(
                    A<string>._, A<NuGetVersion>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Throws(new InvalidOperationException("The second feed should not have been queried."));

            PackageSource firstSource = new("https://example.test/first.json", "first-feed");
            PackageSource secondSource = new("https://example.test/second.json", "second-feed");

            Dictionary<string, SourceRepository> repositories = new()
            {
                [firstSource.Source] = CreateRepository(firstSource, firstResource),
                [secondSource.Source] = CreateRepository(secondSource, secondResource),
            };

            PackageAvailabilityChecker checker = new(
                sources: new[] { firstSource, secondSource },
                repositoryFactory: source => repositories[source.Source]);

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.Contains(s_candidate, result.AvailablePackages);
            A.CallTo(() => secondResource.DoesPackageExistAsync(
                    A<string>._, A<NuGetVersion>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .MustNotHaveHappened();
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
            A.CallTo(() => resource.DoesPackageExistAsync(
                    A<string>._, A<NuGetVersion>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Throws(new InvalidOperationException("The mapped feed does not own this package id and should not be queried."));

            PackageSource mappedSource = new("https://example.test/mapped.json", "mapped-feed");
            PackageAvailabilityChecker checker = new(
                sources: new[] { mappedSource },
                settings: settings,
                repositoryFactory: _ => CreateRepository(mappedSource, resource));

            // s_candidate's package id ("Pack.One") does not match the "Some.Other.*" mapping pattern for "mapped-feed".
            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            // Nothing was eligible to check on the only configured feed, but the feed itself was never queried
            // (and thus never failed), so this remains a successful (empty) result rather than an overall failure.
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

            FindPackageByIdResource resource = CreateResource(exists: true);
            PackageSource mappedSource = new("https://example.test/mapped.json", "mapped-feed");
            PackageAvailabilityChecker checker = new(
                sources: new[] { mappedSource },
                settings: settings,
                repositoryFactory: _ => CreateRepository(mappedSource, resource));

            PackageAvailabilityResult result = await checker.GetAvailablePackagesAsync(
                new[] { s_candidate },
                CancellationToken.None);

            Assert.IsTrue(result.AnyFeedSucceeded);
            Assert.Contains(s_candidate, result.AvailablePackages);
        }

        private static FindPackageByIdResource CreateResource(bool exists)
        {
            FindPackageByIdResource resource = A.Fake<FindPackageByIdResource>();
            A.CallTo(() => resource.DoesPackageExistAsync(
                    A<string>._, A<NuGetVersion>._, A<SourceCacheContext>._, A<ILogger>._, A<CancellationToken>._))
                .Returns(Task.FromResult(exists));
            return resource;
        }

        /// <summary>
        /// Wraps a (possibly null, to simulate an unsupported feed) <see cref="FindPackageByIdResource"/> in a real
        /// <see cref="SourceRepository"/>, so <see cref="PackageAvailabilityChecker"/> can be exercised through its
        /// actual resource-resolution path (<see cref="SourceRepository.GetResourceAsync{T}(CancellationToken)"/>)
        /// without any network access.
        /// </summary>
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
    }
}
