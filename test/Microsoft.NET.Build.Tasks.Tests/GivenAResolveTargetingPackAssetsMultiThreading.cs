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

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
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
