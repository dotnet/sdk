// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveRuntimePackAssetsTask : SdkTest
    {
        public GivenAResolveRuntimePackAssetsTask(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItFiltersSatelliteResources()
        {
            var testDirectory = TestAssetsManager.CreateTestDirectory().Path;

            var task = new ResolveRuntimePackAssets()
            {
                BuildEngine = new MockBuildEngine(),
                FrameworkReferences = new TaskItem[] { new TaskItem("TestFramework") },
                ResolvedRuntimePacks = new TaskItem[]
                {
                    new TaskItem("TestRuntimePack",
                    new Dictionary<string, string> {
                        { "FrameworkName", "TestFramework" },
                        { "RuntimeIdentifier", "test-rid" },
                        { "PackageDirectory", testDirectory },
                        { "PackageVersion", "0.1.0" },
                        { "IsTrimmable", "false" }
                    })
                },
                SatelliteResourceLanguages = new TaskItem[] { new TaskItem("de") }
            };

            Directory.CreateDirectory(Path.Combine(testDirectory, "data"));

            File.WriteAllText(
                Path.Combine(testDirectory, "data", "RuntimeList.xml"),
@"<FileList Name="".NET Core 3.1"" TargetFrameworkIdentifier="".NETCoreApp"" TargetFrameworkVersion=""3.1"" FrameworkName=""Microsoft.NETCore.App"">
  <File Type=""Resources"" Path=""runtimes/de/a.resources.dll"" Culture=""de"" FileVersion=""0.0.0.0"" />
  <File Type=""Resources"" Path=""runtimes/cs/a.resources.dll"" Culture=""cs"" FileVersion=""0.0.0.0"" />
</FileList>");

            task.Execute();
            task.RuntimePackAssets.Should().HaveCount(1);
            string expectedResource = Path.Combine("runtimes", "de", "a.resources.dll");
            task.RuntimePackAssets.FirstOrDefault().ItemSpec.Should().Contain(expectedResource);
        }

        [Fact]
        public void ItResolvesRelativePackageDirectoryAgainstProjectDirectory()
        {
            //  TaskTestEnvironment points the injected TaskEnvironment at ProjectDirectory while
            //  switching the process CWD to a different SpawnDirectory, so a task that resolves
            //  relative paths against the CWD (the pre-migration behavior) would produce the wrong path.
            using var testEnv = new TaskTestEnvironment();

            testEnv.CreateProjectFile(
                "runtimepack/data/RuntimeList.xml",
@"<FileList Name=""Test"" TargetFrameworkIdentifier="".NETCoreApp"" TargetFrameworkVersion=""3.1"" FrameworkName=""Microsoft.NETCore.App"">
  <File Type=""Managed"" Path=""runtimes/a.dll"" AssemblyVersion=""1.0.0.0"" FileVersion=""1.0.0.0"" />
</FileList>");

            var task = new ResolveRuntimePackAssets()
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = testEnv.TaskEnvironment,
                FrameworkReferences = new TaskItem[] { new TaskItem("TestFramework") },
                ResolvedRuntimePacks = new TaskItem[]
                {
                    new TaskItem("TestRuntimePack",
                    new Dictionary<string, string> {
                        { "FrameworkName", "TestFramework" },
                        { "RuntimeIdentifier", "test-rid" },
                        { "PackageDirectory", "runtimepack" },
                        { "PackageVersion", "0.1.0" },
                        { "IsTrimmable", "false" }
                    })
                }
            };

            task.Execute().Should().BeTrue();

            task.RuntimePackAssets.Should().HaveCount(1);
            string assetItemSpec = task.RuntimePackAssets[0].ItemSpec;

            //  The relative PackageDirectory must resolve against the project directory, not the process CWD.
            assetItemSpec.Should().Be(testEnv.GetProjectPath("runtimepack/runtimes/a.dll"));
            assetItemSpec.Should().StartWith(testEnv.ProjectDirectory);
            assetItemSpec.Should().NotContain(testEnv.SpawnDirectory,
                "the resolved asset path must not leak the process working directory");
        }

        [Fact]
        public void ItResolvesAbsolutePackageDirectoryIndependentlyOfCwd()
        {
            using var testEnv = new TaskTestEnvironment();

            string absolutePackageDirectory = testEnv.GetProjectPath("runtimepack");
            testEnv.CreateProjectFile(
                "runtimepack/data/RuntimeList.xml",
@"<FileList Name=""Test"" TargetFrameworkIdentifier="".NETCoreApp"" TargetFrameworkVersion=""3.1"" FrameworkName=""Microsoft.NETCore.App"">
  <File Type=""Managed"" Path=""runtimes/a.dll"" AssemblyVersion=""1.0.0.0"" FileVersion=""1.0.0.0"" />
</FileList>");

            var task = new ResolveRuntimePackAssets()
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = testEnv.TaskEnvironment,
                FrameworkReferences = new TaskItem[] { new TaskItem("TestFramework") },
                ResolvedRuntimePacks = new TaskItem[]
                {
                    new TaskItem("TestRuntimePack",
                    new Dictionary<string, string> {
                        { "FrameworkName", "TestFramework" },
                        { "RuntimeIdentifier", "test-rid" },
                        { "PackageDirectory", absolutePackageDirectory },
                        { "PackageVersion", "0.1.0" },
                        { "IsTrimmable", "false" }
                    })
                }
            };

            task.Execute().Should().BeTrue();

            task.RuntimePackAssets.Should().HaveCount(1);
            //  An absolute PackageDirectory is unaffected by the process CWD after the migration.
            task.RuntimePackAssets[0].ItemSpec.Should().Be(testEnv.GetProjectPath("runtimepack/runtimes/a.dll"));
        }
    }
}
