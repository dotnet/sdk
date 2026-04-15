// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveTargetingPackAssetsMultiThreading : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        [Fact]
        public void CachingBehaviorIsControlledByTaskEnvironment()
        {
            string mockDir = CreateMockTargetingPackDirectory();

            // Task with caching DISABLED via TaskEnvironment
            var envNoCaching = TaskEnvironmentHelper.CreateForTest(mockDir);
            envNoCaching.SetEnvironmentVariable("DOTNETSDK_ALLOW_TARGETING_PACK_CACHING", "0");

            var engineNoCaching = new MockBuildEngine();
            var taskNoCaching = CreateTask(mockDir, engineNoCaching);
            taskNoCaching.TaskEnvironment = envNoCaching;
            taskNoCaching.Execute().Should().BeTrue();

            // Task with caching ENABLED via TaskEnvironment
            var envWithCaching = TaskEnvironmentHelper.CreateForTest(mockDir);
            envWithCaching.SetEnvironmentVariable("DOTNETSDK_ALLOW_TARGETING_PACK_CACHING", "1");

            var engineWithCaching = new MockBuildEngine();
            var taskWithCaching = CreateTask(mockDir, engineWithCaching);
            taskWithCaching.TaskEnvironment = envWithCaching;
            taskWithCaching.Execute().Should().BeTrue();

            engineNoCaching.RegisteredTaskObjects.Should().BeEmpty(
                "caching should be disabled when DOTNETSDK_ALLOW_TARGETING_PACK_CACHING=0 in TaskEnvironment");

            engineWithCaching.RegisteredTaskObjects.Should().NotBeEmpty(
                "caching should be enabled when DOTNETSDK_ALLOW_TARGETING_PACK_CACHING!=0 in TaskEnvironment");
        }

        [Fact]
        public void TaskProducesSameOutputsWithAndWithoutExplicitTaskEnvironment()
        {
            string mockDir = CreateMockTargetingPackDirectory();

            // Run without explicitly setting TaskEnvironment (uses default)
            var engine1 = new MockNeverCacheBuildEngine4();
            var task1 = CreateTask(mockDir, engine1);
            task1.Execute().Should().BeTrue();

            // Run with explicitly set TaskEnvironment seeded from process env
            var engine2 = new MockNeverCacheBuildEngine4();
            var task2 = CreateTask(mockDir, engine2);
            task2.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(mockDir);
            task2.Execute().Should().BeTrue();

            task1.ReferencesToAdd.Select(r => r.ItemSpec).Should()
                .BeEquivalentTo(task2.ReferencesToAdd.Select(r => r.ItemSpec));

            task1.AnalyzersToAdd.Select(r => r.ItemSpec).Should()
                .BeEquivalentTo(task2.AnalyzersToAdd.Select(r => r.ItemSpec));

            task1.PlatformManifests.Select(r => r.ItemSpec).Should()
                .BeEquivalentTo(task2.PlatformManifests.Select(r => r.ItemSpec));
        }

        [Fact]
        public void OutputPathsAreAbsolute()
        {
            string mockDir = CreateMockTargetingPackDirectory();

            var engine = new MockNeverCacheBuildEngine4();
            var task = CreateTask(mockDir, engine);
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(mockDir);
            task.Execute().Should().BeTrue();

            foreach (var reference in task.ReferencesToAdd)
            {
                Path.IsPathRooted(reference.ItemSpec).Should().BeTrue(
                    $"reference path '{reference.ItemSpec}' should be absolute");
            }

            foreach (var analyzer in task.AnalyzersToAdd)
            {
                Path.IsPathRooted(analyzer.ItemSpec).Should().BeTrue(
                    $"analyzer path '{analyzer.ItemSpec}' should be absolute");
            }

            foreach (var manifest in task.PlatformManifests)
            {
                Path.IsPathRooted(manifest.ItemSpec).Should().BeTrue(
                    $"platform manifest path '{manifest.ItemSpec}' should be absolute");
            }
        }

        [Fact]
        public async Task ConcurrentExecutionProducesCorrectResults()
        {
            string mockDir = CreateMockTargetingPackDirectory();

            const int concurrency = 8;
            var taskInstances = new ResolveTargetingPackAssets[concurrency];
            var executeTasks = new Task<bool>[concurrency];

            for (int i = 0; i < concurrency; i++)
            {
                var engine = new MockNeverCacheBuildEngine4();
                var task = CreateTask(mockDir, engine);
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(mockDir);
                taskInstances[i] = task;
            }

            for (int i = 0; i < concurrency; i++)
            {
                var t = taskInstances[i];
                executeTasks[i] = Task.Run(() => t.Execute());
            }

            await Task.WhenAll(executeTasks);

            for (int i = 0; i < concurrency; i++)
            {
                (await executeTasks[i]).Should().BeTrue($"task {i} should succeed");
                taskInstances[i].ReferencesToAdd.Should().NotBeEmpty($"task {i} should produce references");
            }

            var firstRefs = taskInstances[0].ReferencesToAdd.Select(r => r.ItemSpec).OrderBy(x => x).ToArray();
            for (int i = 1; i < concurrency; i++)
            {
                taskInstances[i].ReferencesToAdd.Select(r => r.ItemSpec).OrderBy(x => x)
                    .Should().BeEquivalentTo(firstRefs, $"task {i} output should match task 0");
            }
        }

        private string CreateMockTargetingPackDirectory([CallerMemberName] string testName = null)
        {
            string mockDir = Path.Combine(Path.GetTempPath(), $"ResolveTargetingPackTest_{testName}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(mockDir);
            _tempDirs.Add(mockDir);

            string dataDir = Path.Combine(mockDir, "data");
            Directory.CreateDirectory(dataDir);

            File.WriteAllText(Path.Combine(dataDir, "FrameworkList.xml"), FrameworkListXml);
            File.WriteAllText(Path.Combine(dataDir, "PlatformManifest.txt"), "");

            return mockDir;
        }

        private static ResolveTargetingPackAssets CreateTask(string mockPackageDirectory, IBuildEngine buildEngine)
        {
            return new ResolveTargetingPackAssets
            {
                BuildEngine = buildEngine,
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>())
                },
                ResolvedTargetingPacks = new[]
                {
                    new MockTaskItem("Microsoft.Windows.SDK.NET.Ref",
                        new Dictionary<string, string>()
                        {
                            { MetadataKeys.NuGetPackageId, "Microsoft.Windows.SDK.NET.Ref" },
                            { MetadataKeys.NuGetPackageVersion, "5.0.0-preview1" },
                            { MetadataKeys.PackageConflictPreferredPackages, "Microsoft.Windows.SDK.NET.Ref;" },
                            { MetadataKeys.PackageDirectory, mockPackageDirectory },
                            { MetadataKeys.Path, mockPackageDirectory },
                            { "TargetFramework", "net5.0" },
                        })
                },
                ProjectLanguage = "C#",
            };
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        private const string FrameworkListXml =
@"<FileList Name=""cswinrt .NET Core 5.0"">
  <File Type=""Managed"" Path=""lib/Microsoft.Windows.SDK.NET.dll"" PublicKeyToken=""null"" AssemblyName=""Microsoft.Windows.SDK.NET"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Path=""analyzers/dotnet/anyAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""anyAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Language=""cs"" Path=""analyzers/dotnet/cs/csAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""csAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Language=""vb"" Path=""analyzers/dotnet/vb/vbAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""vbAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
</FileList>";
    }
}
