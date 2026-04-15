// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Tasks.ConflictResolution;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveOverlappingItemGroupConflicts : IDisposable
    {
        private readonly string _tempDir;

        public GivenAResolveOverlappingItemGroupConflicts()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "resolve_overlap_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        /// <summary>
        /// The task must produce identical results regardless of whether TaskEnvironment
        /// is set (multithreaded mode) or left null (legacy single-threaded mode).
        /// </summary>
        [Fact]
        public void ParityBetweenLegacyAndMultithreadedModes()
        {
            var sharedPath1 = CreateTempFile("Shared_A.dll");
            var unique1Path = CreateTempFile("Unique1.dll");
            var sharedPath2 = CreateTempFile("Shared_B.dll");
            var unique2Path = CreateTempFile("Unique2.dll");

            var group1 = new ITaskItem[]
            {
                CreateItem(sharedPath1, targetPath: "Shared.dll", assemblyVersion: "2.0.0.0", packageId: "PackageA"),
                CreateItem(unique1Path, targetPath: "Unique1.dll", assemblyVersion: "1.0.0.0", packageId: "PackageA"),
            };

            var group2 = new ITaskItem[]
            {
                CreateItem(sharedPath2, targetPath: "Shared.dll", assemblyVersion: "1.0.0.0", packageId: "PackageB"),
                CreateItem(unique2Path, targetPath: "Unique2.dll", assemblyVersion: "1.0.0.0", packageId: "PackageB"),
            };

            // Legacy mode (no TaskEnvironment)
            var legacyTask = CreateTask(group1, group2);
            legacyTask.Execute().Should().BeTrue();

            // Multithreaded mode (with TaskEnvironment)
            var mtTask = CreateTask(group1, group2);
            ((IMultiThreadableTask)mtTask).TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir);
            mtTask.Execute().Should().BeTrue();

            // Both should produce the same removed items
            var legacyRemoved1 = legacyTask.RemovedItemGroup1?.Select(i => i?.ItemSpec) ?? Enumerable.Empty<string?>();
            var mtRemoved1 = mtTask.RemovedItemGroup1?.Select(i => i?.ItemSpec) ?? Enumerable.Empty<string?>();
            mtRemoved1.Should().BeEquivalentTo(legacyRemoved1);

            var legacyRemoved2 = legacyTask.RemovedItemGroup2?.Select(i => i?.ItemSpec) ?? Enumerable.Empty<string?>();
            var mtRemoved2 = mtTask.RemovedItemGroup2?.Select(i => i?.ItemSpec) ?? Enumerable.Empty<string?>();
            mtRemoved2.Should().BeEquivalentTo(legacyRemoved2);
        }

        /// <summary>
        /// When the task runs from a decoy CWD that differs from the project directory,
        /// it should still produce correct results because it doesn't rely on process CWD.
        /// </summary>
        [Fact]
        public void PathResolutionUsesProjectDirectoryNotProcessCwd()
        {
            var decoyCwd = Path.Combine(Path.GetTempPath(), "decoy_" + Guid.NewGuid().ToString("N"));
            var projectDir = Path.Combine(Path.GetTempPath(), "project_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(decoyCwd);
            Directory.CreateDirectory(projectDir);

            try
            {
                var sharedPath1 = CreateTempFile("SharedA.dll");
                var sharedPath2 = CreateTempFile("SharedB.dll");

                var group1 = new ITaskItem[]
                {
                    CreateItem(sharedPath1, targetPath: "Shared.dll", assemblyVersion: "2.0.0.0", packageId: "PackageA"),
                };

                var group2 = new ITaskItem[]
                {
                    CreateItem(sharedPath2, targetPath: "Shared.dll", assemblyVersion: "1.0.0.0", packageId: "PackageB"),
                };

                var task = CreateTask(group1, group2);
                ((IMultiThreadableTask)task).TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                var oldCwd = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(decoyCwd);
                try
                {
                    task.Execute().Should().BeTrue();

                    // The lower version (1.0.0.0 from group2) should be removed
                    task.RemovedItemGroup2.Should().NotBeNull();
                    task.RemovedItemGroup2!.Length.Should().Be(1);
                    task.RemovedItemGroup1.Should().BeNullOrEmpty();
                }
                finally
                {
                    Directory.SetCurrentDirectory(oldCwd);
                }
            }
            finally
            {
                Directory.Delete(decoyCwd, true);
                Directory.Delete(projectDir, true);
            }
        }

        /// <summary>
        /// Output items must be a subset of the corresponding input items (by reference).
        /// </summary>
        [Fact]
        public void OutputsAreSubsetsOfInputs()
        {
            var sharedPath1 = CreateTempFile("SharedA.dll");
            var only1Path = CreateTempFile("OnlyInGroup1.dll");
            var sharedPath2 = CreateTempFile("SharedB.dll");
            var only2Path = CreateTempFile("OnlyInGroup2.dll");

            var group1 = new ITaskItem[]
            {
                CreateItem(sharedPath1, targetPath: "Shared.dll", assemblyVersion: "1.0.0.0", packageId: "PackageA"),
                CreateItem(only1Path, targetPath: "OnlyInGroup1.dll", assemblyVersion: "1.0.0.0", packageId: "PackageA"),
            };

            var group2 = new ITaskItem[]
            {
                CreateItem(sharedPath2, targetPath: "Shared.dll", assemblyVersion: "2.0.0.0", packageId: "PackageB"),
                CreateItem(only2Path, targetPath: "OnlyInGroup2.dll", assemblyVersion: "1.0.0.0", packageId: "PackageB"),
            };

            var task = CreateTask(group1, group2);
            ((IMultiThreadableTask)task).TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir);
            task.Execute().Should().BeTrue();

            // Group1's Shared.dll (v1.0) should lose to Group2's Shared.dll (v2.0)
            task.RemovedItemGroup1.Should().NotBeNull();
            task.RemovedItemGroup1!.Length.Should().Be(1);

            // Each removed item should be reference-equal to one of the original input items
            foreach (var removed in task.RemovedItemGroup1!)
            {
                group1.Should().Contain(removed!);
            }

            if (task.RemovedItemGroup2 != null)
            {
                foreach (var removed in task.RemovedItemGroup2)
                {
                    group2.Should().Contain(removed!);
                }
            }
        }

        /// <summary>
        /// Multiple task instances should execute correctly in parallel without interference.
        /// </summary>
        [Fact]
        public void ConcurrentExecutionProducesCorrectResults()
        {
            const int concurrency = 8;
            var tasks = new ResolveOverlappingItemGroupConflicts[concurrency];
            var results = new bool[concurrency];

            for (int i = 0; i < concurrency; i++)
            {
                var sharedPath1 = CreateTempFile($"SharedA_{i}.dll");
                var unique1Path = CreateTempFile($"Unique1_{i}.dll");
                var sharedPath2 = CreateTempFile($"SharedB_{i}.dll");
                var unique2Path = CreateTempFile($"Unique2_{i}.dll");

                var group1 = new ITaskItem[]
                {
                    CreateItem(sharedPath1, targetPath: $"Shared{i}.dll", assemblyVersion: "2.0.0.0", packageId: "PackageA"),
                    CreateItem(unique1Path, targetPath: $"Unique1_{i}.dll", assemblyVersion: "1.0.0.0", packageId: "PackageA"),
                };

                var group2 = new ITaskItem[]
                {
                    CreateItem(sharedPath2, targetPath: $"Shared{i}.dll", assemblyVersion: "1.0.0.0", packageId: "PackageB"),
                    CreateItem(unique2Path, targetPath: $"Unique2_{i}.dll", assemblyVersion: "1.0.0.0", packageId: "PackageB"),
                };

                tasks[i] = CreateTask(group1, group2);
                ((IMultiThreadableTask)tasks[i]).TaskEnvironment = TaskEnvironmentHelper.CreateForTest(
                    Path.Combine(Path.GetTempPath(), $"proj_{i}").TrimEnd(Path.DirectorySeparatorChar));
            }

            Parallel.For(0, concurrency, i =>
            {
                results[i] = tasks[i].Execute();
            });

            for (int i = 0; i < concurrency; i++)
            {
                results[i].Should().BeTrue($"task {i} should succeed");

                // The lower version from group2 should be removed in each instance
                tasks[i].RemovedItemGroup2.Should().NotBeNull($"task {i} should have removed items from group2");
                tasks[i].RemovedItemGroup2!.Length.Should().Be(1, $"task {i} should remove exactly one item from group2");
                tasks[i].RemovedItemGroup1.Should().BeNullOrEmpty($"task {i} should not remove items from group1");
            }
        }

        private static ResolveOverlappingItemGroupConflicts CreateTask(ITaskItem[] group1, ITaskItem[] group2)
        {
            var task = new ResolveOverlappingItemGroupConflicts
            {
                BuildEngine = new MockBuildEngine(),
                ItemGroup1 = group1,
                ItemGroup2 = group2,
            };
            return task;
        }

        private string CreateTempFile(string name)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllBytes(path, Array.Empty<byte>());
            return path;
        }

        private static ITaskItem CreateItem(string itemSpec, string? targetPath = null, string? assemblyVersion = null, string? packageId = null)
        {
            var item = new TaskItem(itemSpec);
            if (targetPath != null)
            {
                item.SetMetadata("TargetPath", targetPath);
            }
            if (assemblyVersion != null)
            {
                item.SetMetadata("AssemblyVersion", assemblyVersion);
            }
            if (packageId != null)
            {
                item.SetMetadata("NuGetPackageId", packageId);
            }
            return item;
        }
    }
}
