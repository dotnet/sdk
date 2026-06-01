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
        public void ItFallsBackToALaterTargetingPackRootWhenAnEarlierOneHasNoData()
        {
            using var env = new TaskTestEnvironment();

            // Only the second targeting pack root has the data; the first points at an
            // existing-but-empty directory. This verifies LoadFrameworkPackagesFromPack
            // iterates the AbsolutePath[] and absolutizes each entry (not just the first).
            const string emptyTargetingPackRoot = "packs-empty";
            const string realTargetingPackRoot = "packs-real";
            env.CreateProjectDirectory(emptyTargetingPackRoot);
            env.CreateProjectFile(
                Path.Combine(
                    realTargetingPackRoot,
                    NetCoreApp + ".Ref",
                    TargetFrameworkVersion + ".0",
                    "data",
                    "PackageOverrides.txt"),
                "Newtonsoft.Json|13.0.1");

            // Empty-but-existing prune data so the task falls back to the targeting pack lookup.
            const string emptyPruneDataRelative = "EmptyPruneData";
            env.CreateProjectDirectory(emptyPruneDataRelative);

            var task = CreateTask(
                env,
                targetingPackRoots: new[] { emptyTargetingPackRoot, realTargetingPackRoot },
                prunePackageDataRoot: emptyPruneDataRelative);

            task.Execute().Should().BeTrue(
                "the lookup should continue to later targeting pack roots when an earlier one has no data");

            task.PackagesToPrune.Should().ContainSingle()
                .Which.ItemSpec.Should().Be("Newtonsoft.Json");
        }

        [Fact]
        public void ItSucceedsWithNoPackagesWhenTargetingPackFolderDoesNotExist()
        {
            using var env = new TaskTestEnvironment();

            // Point both roots at non-existent folders so Directory.Exists(packsFolder) is false
            // (the missing-folder branch in LoadFrameworkPackagesFromPack). With AllowMissing set,
            // the task should succeed without throwing and produce no packages.
            const string missingTargetingPackRoot = "does-not-exist";
            const string emptyPruneDataRelative = "EmptyPruneData";
            env.CreateProjectDirectory(emptyPruneDataRelative);

            var task = CreateTask(
                env,
                targetingPackRoots: missingTargetingPackRoot,
                prunePackageDataRoot: emptyPruneDataRelative,
                allowMissing: true);

            task.Execute().Should().BeTrue(
                "a non-existent targeting pack folder should be handled gracefully when AllowMissingPrunePackageData is true");

            task.PackagesToPrune.Should().BeEmpty();
        }
    }
}
