// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetPackagesToPruneMultiThreading
    {
        private const string Tfm = "10.0";
        private const string FrameworkRef = "Microsoft.NETCore.App";

        /// <summary>
        /// Creates the PrunePackageData directory structure with a PackageOverrides.txt file.
        /// Layout: {pruneRoot}/{tfm}/{frameworkRef}/PackageOverrides.txt
        /// </summary>
        private static void WritePruneData(string pruneRoot)
        {
            var dir = Path.Combine(pruneRoot, Tfm, FrameworkRef);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "PackageOverrides.txt"),
                "System.Text.Json|10.0.0\nSystem.Memory|10.0.0\n");
        }

        private static (bool result, MockBuildEngine engine) RunTask(
            string pruneDataRelPath, string projectDir)
        {
            var engine = new MockBuildEngine();
            var task = new GetPackagesToPrune
            {
                BuildEngine = engine,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = Tfm,
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem(FrameworkRef, new Dictionary<string, string>())
                },
                TargetingPacks = new ITaskItem[]
                {
                    new MockTaskItem(FrameworkRef, new Dictionary<string, string>
                    {
                        ["RuntimeFrameworkName"] = FrameworkRef
                    })
                },
                TargetingPackRoots = Array.Empty<string>(),
                PrunePackageDataRoot = pruneDataRelPath,
                AllowMissingPrunePackageData = false,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };

            var result = task.Execute();
            return (result, engine);
        }

        [Fact]
        public void PrunePackageDataRoot_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"prune-mt-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"prune-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var pruneRelPath = "PruneData";
                WritePruneData(Path.Combine(projectDir, pruneRelPath));

                // CWD != projectDir - path must resolve via TaskEnvironment
                Directory.SetCurrentDirectory(otherDir);

                var (result, engine) = RunTask(pruneRelPath, projectDir);

                result.Should().BeTrue("task should succeed when prune data is found via TaskEnvironment");
                engine.Errors.Should().BeEmpty();
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }

        [Fact]
        public void MissingPruneData_WithAllowMissing_ProducesNoError()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"prune-null-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var engine = new MockBuildEngine();
                var task = new GetPackagesToPrune
                {
                    BuildEngine = engine,
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = Tfm,
                    FrameworkReferences = new ITaskItem[]
                    {
                        new MockTaskItem(FrameworkRef, new Dictionary<string, string>())
                    },
                    TargetingPacks = new ITaskItem[]
                    {
                        new MockTaskItem(FrameworkRef, new Dictionary<string, string>
                        {
                            ["RuntimeFrameworkName"] = FrameworkRef
                        })
                    },
                    TargetingPackRoots = Array.Empty<string>(),
                    PrunePackageDataRoot = "nonexistent",
                    AllowMissingPrunePackageData = true,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                };

                var result = task.Execute();
                result.Should().BeTrue("task should succeed when AllowMissingPrunePackageData is true");
                engine.Errors.Should().BeEmpty();
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameResultsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"prune-parity-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"prune-parity-decoy-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var pruneRelPath = "PruneData";
                WritePruneData(Path.Combine(projectDir, pruneRelPath));

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (mpResult, mpEngine) = RunTask(pruneRelPath, projectDir);

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (mtResult, mtEngine) = RunTask(pruneRelPath, projectDir);

                mpResult.Should().Be(mtResult,
                    "task should return the same success/failure in both environments");
                mpEngine.Errors.Count.Should().Be(mtEngine.Errors.Count,
                    "error count should be the same in both environments");
                for (int i = 0; i < mpEngine.Errors.Count; i++)
                {
                    mpEngine.Errors[i].Message.Should().Be(
                        mtEngine.Errors[i].Message,
                        $"error message [{i}] should be identical in both environments");
                }
                mpEngine.Warnings.Count.Should().Be(mtEngine.Warnings.Count,
                    "warning count should be the same in both environments");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }
    }
}
