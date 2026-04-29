// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections;
using System.Collections.Concurrent;
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
        public void TaskEnvironmentMustBeInjected()
        {
            string mockDir = CreateMockTargetingPackDirectory();

            var engine = new MockNeverCacheBuildEngine4();
            var task = CreateTask(mockDir, engine);

            Action execute = () => task.Execute();

            execute.Should().Throw<InvalidOperationException>()
                .WithMessage("*TaskEnvironment*");
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
        public void RelativeTargetingPackPathsResolveAgainstTaskEnvironmentProjectDirectoryAndArePreservedInOutputs()
        {
            string projectDir = CreateTestDirectory();
            string relativePackPath = Path.Combine("packs", $"Microsoft.Windows.SDK.NET.Ref_{Guid.NewGuid():N}");
            string mockDir = Path.Combine(projectDir, relativePackPath);
            CreateMockTargetingPackContents(mockDir);

            Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), relativePackPath)).Should().BeFalse(
                "this test must fail if task paths are resolved from process current directory");

            var engine = new MockNeverCacheBuildEngine4();
            var task = CreateTask(relativePackPath, engine);
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

            task.Execute().Should().BeTrue();

            task.ReferencesToAdd.Single(r => r.GetMetadata("AssemblyName") == "Microsoft.Windows.SDK.NET").ItemSpec
                .Should().Be(Path.Combine(relativePackPath, "lib/Microsoft.Windows.SDK.NET.dll"));
            task.AnalyzersToAdd.Select(a => a.ItemSpec).Should().BeEquivalentTo(new[]
            {
                Path.Combine(relativePackPath, "analyzers/dotnet/anyAnalyzer.dll"),
                Path.Combine(relativePackPath, "analyzers/dotnet/cs/csAnalyzer.dll"),
            });
            task.PlatformManifests.Single().ItemSpec
                .Should().Be(Path.Combine(relativePackPath, $"data{Path.DirectorySeparatorChar}PlatformManifest.txt"));

            task.ReferencesToAdd.Select(r => r.ItemSpec)
                .Concat(task.AnalyzersToAdd.Select(a => a.ItemSpec))
                .Concat(task.PlatformManifests.Select(m => m.ItemSpec))
                .Should().OnlyContain(path => !Path.IsPathRooted(path));
        }

        [Fact]
        public void SharedCacheKeepsIdenticalRelativeTargetingPackPathsIsolatedByProjectDirectory()
        {
            string firstProjectDir = CreateTestDirectory();
            string secondProjectDir = CreateTestDirectory();
            string relativePackPath = Path.Combine("packs", "Microsoft.Windows.SDK.NET.Ref");

            CreateMockTargetingPackContents(
                Path.Combine(firstProjectDir, relativePackPath),
                FrameworkListXmlWithManagedAssembly("First.Project"));
            CreateMockTargetingPackContents(
                Path.Combine(secondProjectDir, relativePackPath),
                FrameworkListXmlWithManagedAssembly("Second.Project"));

            var engine = new MockBuildEngine();

            var firstTask = CreateTask(relativePackPath, engine);
            firstTask.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(firstProjectDir);
            firstTask.Execute().Should().BeTrue();

            var secondTask = CreateTask(relativePackPath, engine);
            secondTask.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(secondProjectDir);
            secondTask.Execute().Should().BeTrue();

            firstTask.ReferencesToAdd.Single(r => r.GetMetadata("AssemblyName") == "First.Project").ItemSpec
                .Should().Be(Path.Combine(relativePackPath, "lib/First.Project.dll"));
            secondTask.ReferencesToAdd.Single(r => r.GetMetadata("AssemblyName") == "Second.Project").ItemSpec
                .Should().Be(Path.Combine(relativePackPath, "lib/Second.Project.dll"));
            secondTask.ReferencesToAdd.Select(r => r.GetMetadata("AssemblyName"))
                .Should().NotContain("First.Project");

            engine.RegisteredTaskObjects.Count.Should().Be(4,
                "each project directory should have distinct top-level and framework-list cache entries");
        }

        [Fact]
        public void RuntimeFrameworkMetadataIsNotReusedFromCachedResults()
        {
            string mockDir = CreateMockTargetingPackDirectory();
            var engine = new MockBuildEngine();

            var firstTask = CreateTask(mockDir, engine);
            firstTask.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(mockDir);
            firstTask.RuntimeFrameworks = new[] { RuntimeFrameworkWithProfile("firstProfile") };
            firstTask.Execute().Should().BeTrue();
            firstTask.UsedRuntimeFrameworks.Single().GetMetadata("Profile").Should().Be("firstProfile");

            var secondTask = CreateTask(mockDir, engine);
            secondTask.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(mockDir);
            secondTask.RuntimeFrameworks = new[] { RuntimeFrameworkWithProfile("secondProfile") };
            secondTask.Execute().Should().BeTrue();

            secondTask.UsedRuntimeFrameworks.Single().GetMetadata("Profile").Should().Be("secondProfile");
            engine.RegisteredTaskObjects.Count.Should().Be(3,
                "runtime framework metadata should distinguish top-level cache entries while sharing the framework-list cache entry");
        }

        [Fact]
        public async Task ConcurrentExecutionProducesCorrectResults()
        {
            string mockDir = CreateMockTargetingPackDirectory();

            const int concurrency = 64;
            var engine = new MockBuildEngine();
            var taskInstances = new ResolveTargetingPackAssets[concurrency];
            var executeTasks = new Task<bool>[concurrency];
            using var start = new System.Threading.ManualResetEventSlim(false);

            for (int i = 0; i < concurrency; i++)
            {
                var task = CreateTask(mockDir, engine);
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(mockDir);
                taskInstances[i] = task;
            }

            for (int i = 0; i < concurrency; i++)
            {
                var t = taskInstances[i];
                executeTasks[i] = Task.Run(() =>
                {
                    start.Wait();
                    return t.Execute();
                });
            }

            start.Set();

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

            engine.RegisteredTaskObjects.Count.Should().Be(2,
                "shared top-level and framework-list cache entries should be registered once under concurrent execution");
        }

        [Fact]
        public async Task ConcurrentExecutionWithDifferentTopLevelInputsSharesFrameworkListCache()
        {
            string mockDir = CreateMockTargetingPackDirectory();

            const int concurrency = 64;
            using var engine = new ConcurrentMockBuildEngine(topLevelLookupBarrierParticipants: 8, delayFrameworkListCacheMisses: true);
            var taskInstances = new ResolveTargetingPackAssets[concurrency];
            var executeTasks = new Task<bool>[concurrency];
            using var start = new System.Threading.ManualResetEventSlim(false);

            for (int i = 0; i < concurrency; i++)
            {
                var task = CreateTask(mockDir, engine);
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(mockDir);
                task.NetCoreTargetingPackRoot = Path.Combine(mockDir, $"root{i}");
                taskInstances[i] = task;
            }

            for (int i = 0; i < concurrency; i++)
            {
                var t = taskInstances[i];
                executeTasks[i] = Task.Run(() =>
                {
                    start.Wait();
                    return t.Execute();
                });
            }

            start.Set();

            await Task.WhenAll(executeTasks);

            for (int i = 0; i < concurrency; i++)
            {
                (await executeTasks[i]).Should().BeTrue($"task {i} should succeed");
                taskInstances[i].ReferencesToAdd.Should().NotBeEmpty($"task {i} should produce references");
            }

            engine.RegisteredTaskObjects.Count.Should().Be(concurrency + 1,
                "each invocation has a unique top-level key but all invocations share one framework-list key");
            engine.RegisterTaskObjectCalls.Where(kvp => IsTopLevelCacheKey(kvp.Key))
                .Should().HaveCount(concurrency);
            engine.RegisterTaskObjectCalls.Where(kvp => IsFrameworkListCacheKey(kvp.Key))
                .Should().ContainSingle().Which.Value.Should().Be(1);
        }

        private string CreateTestDirectory([CallerMemberName] string testName = null)
        {
            string testDir = Path.Combine(Path.GetTempPath(), $"ResolveTargetingPackTest_{testName}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);
            _tempDirs.Add(testDir);
            return testDir;
        }

        private string CreateMockTargetingPackDirectory([CallerMemberName] string testName = null)
        {
            string mockDir = Path.Combine(CreateTestDirectory(testName), "targetingPack");
            CreateMockTargetingPackContents(mockDir);
            return mockDir;
        }

        private void CreateMockTargetingPackContents(string mockDir, string frameworkListXml = FrameworkListXml)
        {
            Directory.CreateDirectory(mockDir);

            string dataDir = Path.Combine(mockDir, "data");
            Directory.CreateDirectory(dataDir);

            File.WriteAllText(Path.Combine(dataDir, "FrameworkList.xml"), frameworkListXml);
            File.WriteAllText(Path.Combine(dataDir, "PlatformManifest.txt"), "");
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

        private static MockTaskItem RuntimeFrameworkWithProfile(string profile) =>
            new("RuntimeFramework", new Dictionary<string, string>
            {
                [MetadataKeys.FrameworkName] = "Microsoft.Windows.SDK.NET.Ref",
                [MetadataKeys.Version] = "5.0.0",
                ["Profile"] = profile,
            });

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        private static bool IsTopLevelCacheKey(object key) =>
            key is string keyString &&
            keyString.StartsWith(
                $"{nameof(ResolveTargetingPackAssets)}{nameof(ResolveTargetingPackAssets.StronglyTypedInputs)}",
                StringComparison.Ordinal);

        private static bool IsFrameworkListCacheKey(object key) =>
            key is string keyString &&
            keyString.StartsWith(nameof(ResolveTargetingPackAssets.FrameworkListDefinition), StringComparison.Ordinal);

        private sealed class ConcurrentMockBuildEngine : IBuildEngine4, IDisposable
        {
            private readonly Barrier _topLevelLookupBarrier;
            private readonly bool _delayFrameworkListCacheMisses;
            private readonly object _logLock = new();
            private int _topLevelLookupBarrierParticipantsRemaining;
            private int _registeredTaskObjectsQueries;

            public ConcurrentMockBuildEngine(int topLevelLookupBarrierParticipants, bool delayFrameworkListCacheMisses)
            {
                _topLevelLookupBarrier = new Barrier(topLevelLookupBarrierParticipants);
                _topLevelLookupBarrierParticipantsRemaining = topLevelLookupBarrierParticipants;
                _delayFrameworkListCacheMisses = delayFrameworkListCacheMisses;
            }

            public int ColumnNumberOfTaskNode { get; set; }
            public bool ContinueOnError { get; set; }
            public int LineNumberOfTaskNode { get; set; }
            public string ProjectFileOfTaskNode { get; set; }
            public bool IsRunningMultipleNodes => false;
            public ConcurrentDictionary<object, object> RegisteredTaskObjects { get; } = new();
            public ConcurrentDictionary<object, int> RegisterTaskObjectCalls { get; } = new();
            public int RegisteredTaskObjectsQueries => _registeredTaskObjectsQueries;
            public IList<CustomBuildEventArgs> CustomEvents { get; } = new List<CustomBuildEventArgs>();
            public IList<BuildErrorEventArgs> Errors { get; } = new List<BuildErrorEventArgs>();
            public IList<BuildMessageEventArgs> Messages { get; } = new List<BuildMessageEventArgs>();
            public IList<BuildWarningEventArgs> Warnings { get; } = new List<BuildWarningEventArgs>();

            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
            {
                throw new NotImplementedException();
            }

            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) => false;

            public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs) => new();

            public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion) => false;

            public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
            {
                Interlocked.Increment(ref _registeredTaskObjectsQueries);

                if (IsTopLevelCacheKey(key) &&
                    Interlocked.Decrement(ref _topLevelLookupBarrierParticipantsRemaining) >= 0 &&
                    !_topLevelLookupBarrier.SignalAndWait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("Timed out waiting for concurrent top-level cache lookups.");
                }

                RegisteredTaskObjects.TryGetValue(key, out object ret);

                if (ret is null && _delayFrameworkListCacheMisses && IsFrameworkListCacheKey(key))
                {
                    Thread.Sleep(50);
                }

                return ret;
            }

            public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
            {
                RegisterTaskObjectCalls.AddOrUpdate(key, 1, (_, count) => count + 1);

                if (!RegisteredTaskObjects.TryAdd(key, obj))
                {
                    throw new InvalidOperationException($"Task object was already registered for key '{key}'.");
                }
            }

            public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
            {
                RegisteredTaskObjects.TryRemove(key, out object removed);
                return removed;
            }

            public void LogCustomEvent(CustomBuildEventArgs e)
            {
                lock (_logLock)
                {
                    CustomEvents.Add(e);
                }
            }

            public void LogErrorEvent(BuildErrorEventArgs e)
            {
                lock (_logLock)
                {
                    Errors.Add(e);
                }
            }

            public void LogMessageEvent(BuildMessageEventArgs e)
            {
                lock (_logLock)
                {
                    Messages.Add(e);
                }
            }

            public void LogWarningEvent(BuildWarningEventArgs e)
            {
                lock (_logLock)
                {
                    Warnings.Add(e);
                }
            }

            public void Yield()
            {
            }

            public void Reacquire()
            {
            }

            public void Dispose()
            {
                _topLevelLookupBarrier.Dispose();
            }
        }

        private static string FrameworkListXmlWithManagedAssembly(string assemblyName) =>
$@"<FileList Name=""{assemblyName}"">
  <File Type=""Managed"" Path=""lib/{assemblyName}.dll"" PublicKeyToken=""null"" AssemblyName=""{assemblyName}"" AssemblyVersion=""1.0.0.0"" FileVersion=""1.0.0.0"" />
</FileList>";

        private const string FrameworkListXml =
@"<FileList Name=""cswinrt .NET Core 5.0"">
  <File Type=""Managed"" Path=""lib/Microsoft.Windows.SDK.NET.dll"" PublicKeyToken=""null"" AssemblyName=""Microsoft.Windows.SDK.NET"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Path=""analyzers/dotnet/anyAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""anyAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Language=""cs"" Path=""analyzers/dotnet/cs/csAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""csAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
  <File Type=""Analyzer"" Language=""vb"" Path=""analyzers/dotnet/vb/vbAnalyzer.dll"" PublicKeyToken=""null"" AssemblyName=""vbAnalyzer"" AssemblyVersion=""10.0.18362.3"" FileVersion=""10.0.18362.3"" />
</FileList>";
    }
}
