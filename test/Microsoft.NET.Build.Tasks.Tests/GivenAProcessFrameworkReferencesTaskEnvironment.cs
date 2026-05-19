// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAProcessFrameworkReferencesTaskEnvironment
    {
        private const string MinimalRuntimeGraph = """
            {
                "runtimes": {
                    "any": {
                        "#import": ["base"]
                    },
                    "base": {
                        "#import": []
                    }
                }
            }
            """;

        [Fact]
        public void It_resolves_relative_TargetingPackRoot_without_absolutizing_output_metadata()
        {
            var testRoot = CreateTestRoot();
            var runtimeGraphPath = CreateRuntimeGraphFile();
            const string targetingPackRoot = "packs";
            const string targetingPackName = "Microsoft.NETCore.App.Ref";
            const string packVersion = "9.0.0";
            var expectedPackageDirectory = Path.Combine(targetingPackRoot, targetingPackName, packVersion);

            try
            {
                Directory.CreateDirectory(Path.Combine(testRoot, expectedPackageDirectory));

                var task = CreateTask(testRoot, runtimeGraphPath, packVersion);
                task.TargetingPackRoot = targetingPackRoot;

                task.Execute().Should().BeTrue();

                task.TargetingPacks.Should().ContainSingle();
                task.TargetingPacks![0].GetMetadata(MetadataKeys.PackageDirectory).Should().Be(expectedPackageDirectory);
                task.TargetingPacks[0].GetMetadata(MetadataKeys.Path).Should().Be(expectedPackageDirectory);
                task.PackagesToDownload.Should().BeNull();
            }
            finally
            {
                TryDeleteDirectory(testRoot);
                TryDeleteFile(runtimeGraphPath);
            }
        }

        [Fact]
        public void It_resolves_relative_workload_pack_root_without_absolutizing_output_metadata()
        {
            var testRoot = CreateTestRoot();
            var runtimeGraphPath = CreateRuntimeGraphFile();
            const string workloadPackRoot = "workload-root";
            const string targetingPackName = "Microsoft.NETCore.App.Ref";
            const string packVersion = "9.0.0";
            var expectedPackageDirectory = Path.Combine(workloadPackRoot, "packs", targetingPackName, packVersion);

            try
            {
                Directory.CreateDirectory(Path.Combine(testRoot, expectedPackageDirectory));

                var task = CreateTask(testRoot, runtimeGraphPath, packVersion);
                task.TaskEnvironment.SetEnvironmentVariable("DOTNETSDK_WORKLOAD_PACK_ROOTS", workloadPackRoot);

                task.Execute().Should().BeTrue();

                task.TargetingPacks.Should().ContainSingle();
                task.TargetingPacks![0].GetMetadata(MetadataKeys.PackageDirectory).Should().Be(expectedPackageDirectory);
                task.TargetingPacks[0].GetMetadata(MetadataKeys.Path).Should().Be(expectedPackageDirectory);
                task.PackagesToDownload.Should().BeNull();
            }
            finally
            {
                TryDeleteDirectory(testRoot);
                TryDeleteFile(runtimeGraphPath);
            }
        }

        private static ProcessFrameworkReferences CreateTask(string projectDirectory, string runtimeGraphPath, string packVersion)
        {
            return new ProcessFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDirectory),
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = ToolsetInfo.CurrentTargetFrameworkVersion,
                NETCoreSdkRuntimeIdentifier = "win-x64",
                NetCoreRoot = projectDirectory,
                NETCoreSdkVersion = "9.0.100",
                RuntimeGraphPath = runtimeGraphPath,
                EnableTargetingPackDownload = true,
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        { "TargetFramework", ToolsetInfo.CurrentTargetFramework },
                        { "RuntimeFrameworkName", "Microsoft.NETCore.App" },
                        { "DefaultRuntimeFrameworkVersion", packVersion },
                        { "LatestRuntimeFrameworkVersion", packVersion },
                        { "TargetingPackName", "Microsoft.NETCore.App.Ref" },
                        { "TargetingPackVersion", packVersion },
                    })
                },
            };
        }

        private static string CreateTestRoot()
        {
            var testRoot = Path.Combine(Path.GetTempPath(), $"pfr_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testRoot);
            return testRoot;
        }

        private static string CreateRuntimeGraphFile()
        {
            var path = Path.Combine(Path.GetTempPath(), $"rg_{Guid.NewGuid():N}.json");
            File.WriteAllText(path, MinimalRuntimeGraph);
            return path;
        }

        private static void TryDeleteDirectory(string path)
        {
            try { Directory.Delete(path, recursive: true); } catch { }
        }

        private static void TryDeleteFile(string path)
        {
            try { File.Delete(path); } catch { }
        }
    }
}
