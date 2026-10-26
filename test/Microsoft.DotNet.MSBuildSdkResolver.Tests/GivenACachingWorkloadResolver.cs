// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    /// <summary>
    ///  The workload opt-out is sampled separately from constructing a resolver, so that a caller
    ///  which caches resolvers can key on the same value it built the resolver from. These tests
    ///  cover that split: sampling must follow the environment, and construction must follow the
    ///  value it was given rather than sampling again.
    /// </summary>
    [TestClass]
    public class GivenACachingWorkloadResolver : SdkTest
    {
        private const string OptOutVariable = "MSBuildEnableWorkloadResolver";

        public GivenACachingWorkloadResolver() : base()
        {
        }

        //  Runs an action with the opt-out variable set, then restores it.
        private static void WithOptOut(string? value, Action action)
        {
            string? original = Environment.GetEnvironmentVariable(OptOutVariable);
            Environment.SetEnvironmentVariable(OptOutVariable, value);
            try
            {
                action();
            }
            finally
            {
                Environment.SetEnvironmentVariable(OptOutVariable, original);
            }
        }

        //  A minimal SDK layout: enough for a resolver to answer without finding any workload.
        private (string DotnetRoot, string SdkVersion) CreateEmptySdk(string identifier)
        {
            string dotnetRoot = TestAssetsManager.CreateTestDirectory(identifier: identifier).Path;
            string sdkVersion = "10.0.100";
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk", sdkVersion));
            Directory.CreateDirectory(Path.Combine(dotnetRoot, "sdk-manifests", sdkVersion));
            return (dotnetRoot, sdkVersion);
        }

        private CachingWorkloadResolver.ResolutionResult ResolveAutoImports(CachingWorkloadResolver resolver, string dotnetRoot, string sdkVersion)
            => resolver.Resolve(
                "Microsoft.NET.SDK.WorkloadAutoImportPropsLocator",
                dotnetRoot,
                sdkVersion,
                userProfileDir: null,
                globalJsonPath: null);

        [TestMethod]
        public void ItSamplesTheOptOutAsDisabled()
        {
            WithOptOut("false", () => CachingWorkloadResolver.IsEnabled().Should().BeFalse());
        }

        [TestMethod]
        public void ItSamplesTheOptOutCaseInsensitively()
        {
            WithOptOut("FALSE", () => CachingWorkloadResolver.IsEnabled().Should().BeFalse());
        }

        [TestMethod]
        public void ItSamplesAnyOtherValueAsEnabled()
        {
            //  Only the exact word "false" opts out; anything else leaves resolution on.
            WithOptOut("true", () => CachingWorkloadResolver.IsEnabled().Should().BeTrue());
            WithOptOut("0", () => CachingWorkloadResolver.IsEnabled().Should().BeTrue());
        }

        [TestMethod]
        public void ItResolvesNothingWhenConstructedDisabled()
        {
            //  A disabled resolver answers without touching the filesystem, so a path that does
            //  not exist is enough here, and proves it never looked.
            string missingRoot = Path.Combine(Path.GetTempPath(), "no-such-dotnet-root");

            WithOptOut(null, () =>
            {
                var result = ResolveAutoImports(new CachingWorkloadResolver(enabled: false), missingRoot, "10.0.100");
                result.Should().BeOfType<CachingWorkloadResolver.NullResolutionResult>();
            });
        }

        [TestMethod]
        public void ItDoesNotResampleTheOptOutWhenConstructedEnabled()
        {
            var (dotnetRoot, sdkVersion) = CreateEmptySdk("enabledResolver");

            //  Constructed enabled while the environment says "false". If the constructor sampled
            //  the environment again it would disagree with the value a caller keyed on.
            WithOptOut("false", () =>
            {
                var result = ResolveAutoImports(new CachingWorkloadResolver(enabled: true), dotnetRoot, sdkVersion);
                result.Should().BeOfType<CachingWorkloadResolver.MultiplePathResolutionResult>();
            });
        }

        //  One test rather than several, because the shared slot is process-wide state and
        //  separate test methods could run in parallel against it.
        [TestMethod]
        public void ItSharesOneResolverUntilTheKeyChanges()
        {
            var first = CachingWorkloadResolver.GetShared(enabled: true, userProfileDir: "profile-one", globalJsonPath: null);

            //  Same settings: the whole point of the change.
            CachingWorkloadResolver.GetShared(enabled: true, userProfileDir: "profile-one", globalJsonPath: null)
                .Should().BeSameAs(first, "nothing changed, so the resolver must be reused");

            //  A different user profile selects different user-local manifests and packs.
            var otherProfile = CachingWorkloadResolver.GetShared(enabled: true, userProfileDir: "profile-two", globalJsonPath: null);
            otherProfile.Should().NotBeSameAs(first);

            //  The opt-out is re-applied by MSBuild on every build, so it must not be frozen.
            CachingWorkloadResolver.GetShared(enabled: false, userProfileDir: "profile-two", globalJsonPath: null)
                .Should().NotBeSameAs(otherProfile);
        }

        [TestMethod]
        public void ItReplacesTheSharedResolverWhenGlobalJsonIsEditedInPlace()
        {
            string directory = TestAssetsManager.CreateTestDirectory(identifier: "sharedGlobalJson").Path;
            string globalJsonPath = Path.Combine(directory, "global.json");
            File.WriteAllText(globalJsonPath, """{"sdk":{"version":"10.0.100"}}""");

            var first = CachingWorkloadResolver.GetShared(enabled: true, userProfileDir: null, globalJsonPath);

            //  An unchanged global.json must still reuse, or the optimization is lost for every
            //  build in a repository that has one.
            CachingWorkloadResolver.GetShared(enabled: true, userProfileDir: null, globalJsonPath)
                .Should().BeSameAs(first, "the file has not changed");

            //  Same path, same length, and the timestamp is put back: only the contents differ.
            DateTime originalWriteTime = File.GetLastWriteTimeUtc(globalJsonPath);
            File.WriteAllText(globalJsonPath, """{"sdk":{"version":"10.0.200"}}""");
            File.SetLastWriteTimeUtc(globalJsonPath, originalWriteTime);

            CachingWorkloadResolver.GetShared(enabled: true, userProfileDir: null, globalJsonPath)
                .Should().NotBeSameAs(first, "the workload version global.json selects may have changed");
        }
    }
}
