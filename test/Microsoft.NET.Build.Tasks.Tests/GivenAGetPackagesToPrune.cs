// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Functional tests for <see cref="GetPackagesToPrune"/> focused on how the task
    /// resolves <c>PrunePackageDataRoot</c> and <c>TargetingPackRoots</c> through
    /// <see cref="TaskEnvironment"/>. These tests pass RELATIVE paths and verify that
    /// the task reads the on-disk data from the right place even when the process
    /// current working directory differs from the project directory.
    /// </summary>
    public class GivenAGetPackagesToPrune
    {
        private const string NetCoreApp = "Microsoft.NETCore.App";
        private const string TargetFrameworkVersion = "10.0";

        private static GetPackagesToPrune CreateTask(TaskTestEnvironment env, string targetingPackRoots, string prunePackageDataRoot, bool allowMissing = false)
        {
            return CreateTask(env, new[] { targetingPackRoots }, prunePackageDataRoot, allowMissing);
        }

        private static GetPackagesToPrune CreateTask(TaskTestEnvironment env, string[] targetingPackRoots, string prunePackageDataRoot, bool allowMissing = false)
        {
            var frameworkReference = new TaskItem(NetCoreApp);
            var targetingPack = new TaskItem(NetCoreApp);
            targetingPack.SetMetadata("RuntimeFrameworkName", NetCoreApp);

            return new GetPackagesToPrune
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TaskEnvironment = env.TaskEnvironment,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = TargetFrameworkVersion,
                FrameworkReferences = new ITaskItem[] { frameworkReference },
                TargetingPacks = new ITaskItem[] { targetingPack },
                TargetingPackRoots = targetingPackRoots,
                PrunePackageDataRoot = prunePackageDataRoot,
                AllowMissingPrunePackageData = allowMissing,
                LoadPrunePackageDataFromNearestFramework = false,
            };
        }

        [Fact]
        public void ItResolvesPrunePackageDataRootRelativeToTaskEnvironmentProjectDirectory()
        {
            using var env = new TaskTestEnvironment();

            // Lay out a PackageOverrides.txt at <ProjectDir>/PrunePackageData/<tfmVersion>/<frameworkRef>/
            const string prunePackageDataRelative = "PrunePackageData";
            env.CreateProjectFile(
                Path.Combine(prunePackageDataRelative, TargetFrameworkVersion, NetCoreApp, "PackageOverrides.txt"),
                "Newtonsoft.Json|13.0.1");

            var task = CreateTask(env, targetingPackRoots: env.GetProjectPath("targetingpacks"), prunePackageDataRelative);

            task.Execute().Should().BeTrue(
                "PrunePackageDataRoot is relative and must be resolved against TaskEnvironment.ProjectDirectory, not the process CWD");

            task.PackagesToPrune.Should().ContainSingle()
                .Which.ItemSpec.Should().Be("Newtonsoft.Json");
        }

        [Fact]
        public void ItResolvesTargetingPackRootsRelativeToTaskEnvironmentProjectDirectory()
        {
            using var env = new TaskTestEnvironment();

            // Lay out a targeting-pack PackageOverrides.txt at
            // <ProjectDir>/packs/Microsoft.NETCore.App.Ref/<major>.<minor>.0/data/PackageOverrides.txt
            const string targetingPackRootRelative = "packs";
            env.CreateProjectFile(
                Path.Combine(
                    targetingPackRootRelative,
                    NetCoreApp + ".Ref",
                    TargetFrameworkVersion + ".0",
                    "data",
                    "PackageOverrides.txt"),
                "Newtonsoft.Json|13.0.1");

            // Point PrunePackageDataRoot at an existing-but-empty directory so the task falls back
            // to the targeting pack lookup (LoadPackagesToPruneFromPrunePackageData returns null).
            const string emptyPruneDataRelative = "EmptyPruneData";
            env.CreateProjectDirectory(emptyPruneDataRelative);

            var task = CreateTask(env, targetingPackRoots: targetingPackRootRelative, prunePackageDataRoot: emptyPruneDataRelative);

            task.Execute().Should().BeTrue(
                "TargetingPackRoots is relative and must be resolved against TaskEnvironment.ProjectDirectory, not the process CWD");

            task.PackagesToPrune.Should().ContainSingle()
                .Which.ItemSpec.Should().Be("Newtonsoft.Json");
        }

        [Fact]
        public void ItIgnoresEmptyTargetingPackRootEntries()
        {
            using var env = new TaskTestEnvironment();

            // Provide valid prune data so the task can succeed without any targeting pack roots.
            const string prunePackageDataRelative = "PrunePackageData";
            env.CreateProjectFile(
                Path.Combine(prunePackageDataRelative, TargetFrameworkVersion, NetCoreApp, "PackageOverrides.txt"),
                "Newtonsoft.Json|13.0.1");

            var task = CreateTask(env, targetingPackRoots: string.Empty, prunePackageDataRelative);

            // An empty string in TargetingPackRoots must be skipped without throwing from
            // TaskEnvironment.GetAbsolutePath("").
            task.Execute().Should().BeTrue(
                "empty entries in TargetingPackRoots should be silently skipped, preserving pre-migration behavior");

            task.PackagesToPrune.Should().ContainSingle()
                .Which.ItemSpec.Should().Be("Newtonsoft.Json");
        }

        [Fact]
        public void ItDoesNotReuseCachedDataForDifferentResolvedTargetingPackRoots()
        {
            using var env = new TaskTestEnvironment();

            // Two roots with the SAME framework values but DIFFERENT data. The build-wide cache key
            // must include the resolved roots; otherwise the second task would incorrectly reuse the
            // first one's packages.
            const string rootA = "packs-a";
            const string rootB = "packs-b";
            env.CreateProjectFile(
                Path.Combine(rootA, NetCoreApp + ".Ref", TargetFrameworkVersion + ".0", "data", "PackageOverrides.txt"),
                "Newtonsoft.Json|13.0.1");
            env.CreateProjectFile(
                Path.Combine(rootB, NetCoreApp + ".Ref", TargetFrameworkVersion + ".0", "data", "PackageOverrides.txt"),
                "System.Text.Json|8.0.0");

            // Empty-but-existing prune data so both fall back to the targeting pack lookup.
            const string emptyPruneDataRelative = "EmptyPruneData";
            env.CreateProjectDirectory(emptyPruneDataRelative);

            // A SHARED caching engine across both task runs (MockBuildEngine caches by CacheKey).
            var sharedEngine = new MockBuildEngine();

            var taskA = CreateTask(env, targetingPackRoots: rootA, prunePackageDataRoot: emptyPruneDataRelative);
            taskA.BuildEngine = sharedEngine;
            taskA.Execute().Should().BeTrue();
            taskA.PackagesToPrune.Should().ContainSingle().Which.ItemSpec.Should().Be("Newtonsoft.Json");

            var taskB = CreateTask(env, targetingPackRoots: rootB, prunePackageDataRoot: emptyPruneDataRelative);
            taskB.BuildEngine = sharedEngine;
            taskB.Execute().Should().BeTrue();
            taskB.PackagesToPrune.Should().ContainSingle().Which.ItemSpec.Should().Be(
                "System.Text.Json",
                "different resolved targeting pack roots must not collide in the build-wide cache");
        }

        [Fact]
        public void ItReusesCachedDataForIdenticalInputs()
        {
            using var env = new TaskTestEnvironment();

            const string prunePackageDataRelative = "PrunePackageData";
            env.CreateProjectFile(
                Path.Combine(prunePackageDataRelative, TargetFrameworkVersion, NetCoreApp, "PackageOverrides.txt"),
                "Newtonsoft.Json|13.0.1");

            var sharedEngine = new MockBuildEngine();

            var first = CreateTask(env, targetingPackRoots: "packs", prunePackageDataRelative);
            first.BuildEngine = sharedEngine;
            first.Execute().Should().BeTrue();

            var second = CreateTask(env, targetingPackRoots: "packs", prunePackageDataRelative);
            second.BuildEngine = sharedEngine;
            second.Execute().Should().BeTrue();

            // The second run with identical inputs should hit the cache and return the very same array.
            second.PackagesToPrune.Should().BeSameAs(first.PackagesToPrune,
                "identical framework values and resolved roots should produce a cache hit");
        }
    }
}
