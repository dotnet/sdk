// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.NET.Build.Tasks.ConflictResolution;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [Collection(nameof(WorkingDirectoryCollection))]
    public class GivenAResolveOverlappingItemGroupConflicts : IDisposable
    {
        private static readonly MetadataReference[] s_compilerReferences = GetCompilerReferences();
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

        [Fact]
        public void ResolvePublishOutputConflictsIsRecognizedAsMSBuildMultiThreadable()
        {
            typeof(ResolveOverlappingItemGroupConflicts)
                .GetCustomAttributes(inherit: false)
                .Select(attribute => attribute.GetType().FullName)
                .Should()
                .Contain("Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute");
        }

        [Fact]
        public void RelativeSourcePathsResolveAgainstProjectDirectory()
        {
            var projectDir = CreateProjectDirectory(nameof(RelativeSourcePathsResolveAgainstProjectDirectory));
            var relativePath1 = CreateProjectFile(projectDir, Path.Combine("refs", "SharedA.dll"));
            var relativePath2 = CreateProjectFile(projectDir, Path.Combine("refs", "SharedB.dll"));

            var group1 = new ITaskItem[]
            {
                CreateItem(relativePath1, targetPath: "Shared.dll", assemblyVersion: "2.0.0.0", packageId: "PackageA"),
            };

            var group2 = new ITaskItem[]
            {
                CreateItem(relativePath2, targetPath: "Shared.dll", assemblyVersion: "1.0.0.0", packageId: "PackageB"),
            };

            var task = CreateTask(group1, group2, projectDir);

            task.Execute().Should().BeTrue();

            task.RemovedItemGroup2.Should().ContainSingle()
                .Which.Should().BeSameAs(group2[0]);
            task.RemovedItemGroup2![0]!.ItemSpec.Should().Be(relativePath2);
            task.RemovedItemGroup1.Should().BeNullOrEmpty();
        }

        [Fact]
        public void RelativeSourcePathAssemblyVersionReadsResolveAgainstProjectDirectoryWhenCurrentDirectoryDiffers()
        {
            var projectDir = CreateProjectDirectory(nameof(RelativeSourcePathAssemblyVersionReadsResolveAgainstProjectDirectoryWhenCurrentDirectoryDiffers));
            var hostileCurrentDirectory = CreateProjectDirectory("hostile_cwd");
            var relativePath1 = Path.Combine("refs", "SharedA.dll");
            var relativePath2 = Path.Combine("refs", "SharedB.dll");

            CreateVersionedAssembly(projectDir, relativePath1, assemblyVersion: "2.0.0.0", fileVersion: "1.0.0.0");
            CreateVersionedAssembly(projectDir, relativePath2, assemblyVersion: "1.0.0.0", fileVersion: "1.0.0.0");
            CreateVersionedAssembly(hostileCurrentDirectory, relativePath1, assemblyVersion: "1.0.0.0", fileVersion: "1.0.0.0");
            CreateVersionedAssembly(hostileCurrentDirectory, relativePath2, assemblyVersion: "3.0.0.0", fileVersion: "1.0.0.0");

            var group1 = new ITaskItem[]
            {
                CreateItem(relativePath1, targetPath: "Shared.dll", packageId: "PackageA"),
            };

            var group2 = new ITaskItem[]
            {
                CreateItem(relativePath2, targetPath: "Shared.dll", packageId: "PackageB"),
            };

            var task = CreateTask(group1, group2, projectDir);

            ExecuteWithCurrentDirectory(hostileCurrentDirectory, () => task.Execute().Should().BeTrue());

            task.RemovedItemGroup2.Should().ContainSingle()
                .Which.Should().BeSameAs(group2[0]);
            task.RemovedItemGroup2![0]!.ItemSpec.Should().Be(relativePath2);
            task.RemovedItemGroup1.Should().BeNullOrEmpty();
        }

        [Fact]
        public void RelativeSourcePathFileVersionReadsResolveAgainstProjectDirectoryWhenCurrentDirectoryDiffers()
        {
            var projectDir = CreateProjectDirectory(nameof(RelativeSourcePathFileVersionReadsResolveAgainstProjectDirectoryWhenCurrentDirectoryDiffers));
            var hostileCurrentDirectory = CreateProjectDirectory("hostile_cwd");
            var relativePath1 = Path.Combine("refs", "SharedA.dll");
            var relativePath2 = Path.Combine("refs", "SharedB.dll");

            CreateVersionedAssembly(projectDir, relativePath1, assemblyVersion: "1.0.0.0", fileVersion: "3.0.0.0");
            CreateVersionedAssembly(projectDir, relativePath2, assemblyVersion: "1.0.0.0", fileVersion: "1.0.0.0");
            CreateVersionedAssembly(hostileCurrentDirectory, relativePath1, assemblyVersion: "1.0.0.0", fileVersion: "1.0.0.0");
            CreateVersionedAssembly(hostileCurrentDirectory, relativePath2, assemblyVersion: "1.0.0.0", fileVersion: "3.0.0.0");

            var group1 = new ITaskItem[]
            {
                CreateItem(relativePath1, targetPath: "Shared.dll", packageId: "PackageA"),
            };

            var group2 = new ITaskItem[]
            {
                CreateItem(relativePath2, targetPath: "Shared.dll", packageId: "PackageB"),
            };

            var task = CreateTask(group1, group2, projectDir);

            ExecuteWithCurrentDirectory(hostileCurrentDirectory, () => task.Execute().Should().BeTrue());

            task.RemovedItemGroup2.Should().ContainSingle()
                .Which.Should().BeSameAs(group2[0]);
            task.RemovedItemGroup2![0]!.ItemSpec.Should().Be(relativePath2);
            task.RemovedItemGroup1.Should().BeNullOrEmpty();
        }

        [Fact]
        public void AbsoluteSourcePathsAreNotRebasedToProjectDirectory()
        {
            var projectDir = CreateProjectDirectory(nameof(AbsoluteSourcePathsAreNotRebasedToProjectDirectory));
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

            var task = CreateTask(group1, group2, projectDir);

            task.Execute().Should().BeTrue();

            task.RemovedItemGroup2.Should().ContainSingle()
                .Which.Should().BeSameAs(group2[0]);
            task.RemovedItemGroup2![0]!.ItemSpec.Should().Be(sharedPath2);
            task.RemovedItemGroup1.Should().BeNullOrEmpty();
        }

        [Fact]
        public void OutputsAreSubsetsOfInputs()
        {
            var projectDir = CreateProjectDirectory(nameof(OutputsAreSubsetsOfInputs));
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

            var task = CreateTask(group1, group2, projectDir);
            task.Execute().Should().BeTrue();

            task.RemovedItemGroup1.Should().ContainSingle()
                .Which.Should().BeSameAs(group1[0]);

            if (task.RemovedItemGroup2 != null)
            {
                foreach (var removed in task.RemovedItemGroup2)
                {
                    group2.Should().Contain(removed!);
                }
            }
        }

        [Fact]
        public void ConcurrentExecutionProducesCorrectResults()
        {
            const int concurrency = 64;
            var tasks = new ResolveOverlappingItemGroupConflicts[concurrency];
            var itemGroups2 = new ITaskItem[concurrency][];
            var results = new bool[concurrency];
            var exceptions = new Exception?[concurrency];
            var currentDirectory = Directory.GetCurrentDirectory();
            var cancellationToken = TestContext.Current.CancellationToken;

            for (int i = 0; i < concurrency; i++)
            {
                var projectDir = CreateProjectDirectory($"parallel_{i}");
                var sharedPath1 = CreateProjectFile(projectDir, Path.Combine("refs", "SharedA.dll"));
                var unique1Path = CreateProjectFile(projectDir, Path.Combine("refs", "Unique1.dll"));
                var sharedPath2 = CreateProjectFile(projectDir, Path.Combine("refs", "SharedB.dll"));
                var unique2Path = CreateProjectFile(projectDir, Path.Combine("refs", "Unique2.dll"));

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

                itemGroups2[i] = group2;
                tasks[i] = CreateTask(group1, group2, projectDir);
            }

            using var barrier = new Barrier(concurrency + 1);
            var workers = new Thread[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                var index = i;
                workers[i] = new Thread(() =>
                {
                    try
                    {
                        if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30), cancellationToken))
                        {
                            throw new TimeoutException("Timed out waiting for concurrent task start.");
                        }
                        results[index] = tasks[index].Execute();
                    }
                    catch (Exception ex)
                    {
                        exceptions[index] = ex;
                    }
                });
                workers[i].IsBackground = true;
                workers[i].Start();
            }

            barrier.SignalAndWait(TimeSpan.FromSeconds(30), cancellationToken).Should().BeTrue();

            foreach (var worker in workers)
            {
                worker.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();
            }

            exceptions.Where(e => e != null).Should().BeEmpty();

            Directory.GetCurrentDirectory().Should().Be(currentDirectory);

            for (int i = 0; i < concurrency; i++)
            {
                results[i].Should().BeTrue($"task {i} should succeed");
                tasks[i].RemovedItemGroup2.Should().ContainSingle($"task {i} should remove exactly one item from group2")
                    .Which.Should().BeSameAs(itemGroups2[i][0]);
                tasks[i].RemovedItemGroup1.Should().BeNullOrEmpty($"task {i} should not remove items from group1");
            }
        }

        private static ResolveOverlappingItemGroupConflicts CreateTask(ITaskItem[] group1, ITaskItem[] group2, string projectDirectory)
        {
            var task = new ResolveOverlappingItemGroupConflicts
            {
                BuildEngine = new MockBuildEngine(),
                ItemGroup1 = group1,
                ItemGroup2 = group2,
            };
            ((IMultiThreadableTask)task).TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDirectory);
            return task;
        }

        private string CreateTempFile(string name)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllBytes(path, Array.Empty<byte>());
            return path;
        }

        private string CreateProjectDirectory(string name)
        {
            var path = Path.Combine(_tempDir, name + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string CreateProjectFile(string projectDirectory, string relativePath)
        {
            var path = Path.Combine(projectDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Array.Empty<byte>());
            return relativePath;
        }

        private static void CreateVersionedAssembly(string projectDirectory, string relativePath, string assemblyVersion, string fileVersion)
        {
            var path = Path.Combine(projectDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var source = $$"""
                using System.Reflection;

                [assembly: AssemblyVersion("{{assemblyVersion}}")]
                [assembly: AssemblyFileVersion("{{fileVersion}}")]

                public class VersionedAssembly
                {
                }
                """;

            var emitResult = CSharpCompilation.Create(
                    Path.GetFileNameWithoutExtension(path),
                    new[] { CSharpSyntaxTree.ParseText(source) },
                    s_compilerReferences,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .Emit(path);

            emitResult.Success.Should().BeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics));
        }

        private static void ExecuteWithCurrentDirectory(string currentDirectory, Action action)
        {
            var originalCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(currentDirectory);
                action();
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
            }
        }

        private static MetadataReference[] GetCompilerReferences()
        {
            var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (!string.IsNullOrEmpty(trustedPlatformAssemblies))
            {
                return trustedPlatformAssemblies
                    .Split(Path.PathSeparator)
                    .Select(path => MetadataReference.CreateFromFile(path))
                    .ToArray();
            }

            return new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(AssemblyVersionAttribute).Assembly.Location),
            };
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

    [CollectionDefinition(nameof(WorkingDirectoryCollection), DisableParallelization = true)]
    public sealed class WorkingDirectoryCollection
    {
    }
}
