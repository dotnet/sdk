// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [CollectionDefinition(nameof(GenerateDepsFileCwdSensitiveCollection), DisableParallelization = true)]
    public sealed class GenerateDepsFileCwdSensitiveCollection
    {
    }

    [Collection(nameof(GenerateDepsFileCwdSensitiveCollection))]
    public class GivenAGenerateDepsFilePathResolution : IDisposable
    {
        private readonly List<string> _tempDirs = new();
        private readonly string _originalCwd = Directory.GetCurrentDirectory();

        [Fact]
        public void ProjectAndDepsFilePathsAreResolvedThroughTaskEnvironment()
        {
            string projectDir = CreateTestProjectDirectory();
            string decoyCwd = CreateTestProjectDirectory("DecoyDir");
            string relativeDepsPath = Path.Combine("bin", "Debug", "net8.0", "TestApp.deps.json");

            Directory.SetCurrentDirectory(decoyCwd);

            var task = CreateMinimalTask("TestApp.csproj", relativeDepsPath, projectDir);
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

            bool result = task.Execute();

            result.Should().BeTrue("task should resolve relative paths from TaskEnvironment.ProjectDirectory");
            File.Exists(Path.Combine(projectDir, relativeDepsPath)).Should().BeTrue(
                "deps file should be written relative to TaskEnvironment.ProjectDirectory, not process CWD");
            File.Exists(Path.Combine(decoyCwd, relativeDepsPath)).Should().BeFalse(
                "deps file should not be written to the process CWD");
            task.FilesWritten.Should().ContainSingle().Which.ItemSpec.Should().Be(relativeDepsPath,
                "FilesWritten should preserve the caller's original relative output path");
        }

        [Fact]
        public void AssetsAndRuntimeGraphPathsAreResolvedThroughTaskEnvironment()
        {
            string projectDir = CreateTestProjectDirectory();
            // Decoy CWD is intentionally empty (no runtime.json) so that resolving
            // RuntimeGraphPath against the process CWD would fail; only resolution
            // through TaskEnvironment.ProjectDirectory can succeed.
            string decoyCwd = CreateEmptyDecoyDirectory();
            string relativeAssetsPath = Path.Combine("obj", "project.assets.json");
            CreateAssetsFile(projectDir, relativeAssetsPath);

            Directory.SetCurrentDirectory(decoyCwd);

            var task = CreateMinimalTask("TestApp.csproj", "selfcontained.deps.json", projectDir);
            task.AssetsFilePath = relativeAssetsPath;
            task.RuntimeGraphPath = "runtime.json";
            task.IsSelfContained = true;
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

            bool result = task.Execute();

            result.Should().BeTrue(
                "AssetsFilePath and RuntimeGraphPath should be resolved from TaskEnvironment.ProjectDirectory, not process CWD. Errors: {0}",
                GetLoggedErrors(task));
            File.Exists(Path.Combine(projectDir, "selfcontained.deps.json")).Should().BeTrue(
                "the relative runtime graph should be read from the task environment project directory");
            File.Exists(Path.Combine(decoyCwd, "selfcontained.deps.json")).Should().BeFalse(
                "deps file should not be written under the process CWD");
        }

        [Fact]
        public void SatelliteAssemblyTargetPathFlowsVerbatimToDepsJsonResources()
        {
            // Encodes the contract that AssemblySatelliteAssemblies metadata is passed through
            // to deps.json verbatim. The task reads only Culture and TargetPath; TargetPath is
            // relative by design (deps.json format requirement) and must NOT be absolutized
            // against TaskEnvironment.ProjectDirectory or the process CWD.
            string projectDir = CreateTestProjectDirectory();
            string decoyCwd = CreateEmptyDecoyDirectory();
            string relativeDepsPath = Path.Combine("bin", "Debug", "net8.0", "TestApp.deps.json");
            string satelliteRelativeTargetPath = Path.Combine("de", "TestApp.resources.dll");

            Directory.SetCurrentDirectory(decoyCwd);

            var task = CreateMinimalTask("TestApp.csproj", relativeDepsPath, projectDir);
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
            task.AssemblySatelliteAssemblies = new ITaskItem[]
            {
                // ItemSpec is intentionally not a real on-disk path: the task only reads
                // metadata from satellite items, never opens the underlying file.
                new MockTaskItem(
                    itemSpec: @"de\TestApp.resources.dll",
                    metadata: new Dictionary<string, string>
                    {
                        { "Culture", "de" },
                        { "TargetPath", satelliteRelativeTargetPath },
                    }),
            };

            bool result = task.Execute();

            result.Should().BeTrue("task should succeed: {0}", GetLoggedErrors(task));
            string depsPath = Path.Combine(projectDir, relativeDepsPath);
            File.Exists(depsPath).Should().BeTrue("deps file should be produced");

            JObject depsJson = JObject.Parse(File.ReadAllText(depsPath));
            JObject targets = (JObject)depsJson["targets"];
            targets.Should().NotBeNull("deps.json must have a targets section");
            JObject targetFramework = (JObject)targets.Properties().First().Value;
            JObject mainLibrary = (JObject)targetFramework["TestApp/1.0.0.0"];
            mainLibrary.Should().NotBeNull("the main project library should be present in deps.json");

            JObject resources = (JObject)mainLibrary["resources"];
            resources.Should().NotBeNull("the satellite assembly should produce a resources section");

            // The deps.json writer normalizes path separators to '/'; the path itself is
            // otherwise the TargetPath metadata byte-for-byte. Critically it is NOT:
            //   - prefixed with TaskEnvironment.ProjectDirectory,
            //   - prefixed with the process CWD,
            //   - resolved or absolutized in any way.
            string expectedRelativePath = satelliteRelativeTargetPath.Replace('\\', '/');
            resources.Properties().Should().ContainSingle(
                "exactly one satellite was supplied")
                .Which.Name.Should().Be(expectedRelativePath,
                    "TargetPath metadata flows verbatim into deps.json (slashes normalized only)");
            resources[expectedRelativePath]["locale"].Value<string>().Should().Be("de",
                "Culture metadata is preserved as the locale");
        }

        private string CreateEmptyDecoyDirectory([CallerMemberName] string testName = null)
        {
            string decoyDir = Path.Combine(Path.GetTempPath(), $"GenerateDepsFileTest_{testName}_decoy_{Guid.NewGuid():N}");
            Directory.CreateDirectory(decoyDir);
            _tempDirs.Add(decoyDir);
            return decoyDir;
        }

        private string CreateTestProjectDirectory([CallerMemberName] string testName = null)
        {
            string testDir = Path.Combine(Path.GetTempPath(), $"GenerateDepsFileTest_{testName}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);
            Directory.CreateDirectory(Path.Combine(testDir, "bin", "Debug", "net8.0"));
            _tempDirs.Add(testDir);

            // Create a minimal runtime.json
            string runtimeJsonPath = Path.Combine(testDir, "runtime.json");
            File.WriteAllText(runtimeJsonPath, @"{
  ""runtimes"": {
    ""win"": {},
    ""win-x64"": { ""#import"": [ ""win"" ] }
  }
}");

            return testDir;
        }

        private GenerateDepsFile CreateMinimalTask(string projectPath, string depsFilePath, string projectDirectory = null)
        {
            string projectDir = projectDirectory ?? Path.GetDirectoryName(projectPath);
            projectDir.Should().NotBeNull("relative project paths require the test project directory to be provided");

            return new GenerateDepsFile
            {
                BuildEngine = new MockBuildEngine(),
                ProjectPath = projectPath,
                DepsFilePath = depsFilePath,
                TargetFramework = "net8.0",
                AssemblyName = "TestApp",
                AssemblyExtension = ".dll",
                AssemblyVersion = "1.0.0.0",
                IncludeMainProject = true,
                CompileReferences = Array.Empty<ITaskItem>(),
                ResolvedNuGetFiles = Array.Empty<ITaskItem>(),
                ResolvedRuntimeTargetsFiles = Array.Empty<ITaskItem>(),
                RuntimeGraphPath = Path.Combine(projectDir, "runtime.json"),
            };
        }

        private static string CreateAssetsFile(string projectDir, string relativeAssetsPath)
        {
            string assetsPath = Path.Combine(projectDir, relativeAssetsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(assetsPath));
            string projectPath = Path.Combine(projectDir, "TestApp.csproj");

            File.WriteAllText(assetsPath, $$"""
                {
                  "version": 3,
                  "targets": { "net8.0": {} },
                  "libraries": {},
                  "projectFileDependencyGroups": { "net8.0": [] },
                  "packageFolders": {},
                  "project": {
                    "version": "1.0.0",
                    "restore": {
                      "projectUniqueName": {{JsonConvert.ToString(projectPath)}},
                      "projectName": "TestApp",
                      "projectPath": {{JsonConvert.ToString(projectPath)}},
                      "packagesPath": {{JsonConvert.ToString(Path.Combine(projectDir, ".nuget", "packages"))}},
                      "outputPath": {{JsonConvert.ToString(Path.Combine(projectDir, "obj"))}},
                      "projectStyle": "PackageReference",
                      "fallbackFolders": [],
                      "configFilePaths": [],
                      "originalTargetFrameworks": [ "net8.0" ],
                      "sources": {},
                      "frameworks": { "net8.0": { "targetAlias": "net8.0" } },
                      "warningProperties": { "warnAsError": [ "NU1605" ] }
                    },
                    "frameworks": {
                      "net8.0": {
                        "targetAlias": "net8.0",
                        "runtimeIdentifierGraphPath": {{JsonConvert.ToString(Path.Combine(projectDir, "runtime.json"))}}
                      }
                    }
                  }
                }
                """);
            return assetsPath;
        }

        private static string GetLoggedErrors(GenerateDepsFile task)
        {
            return string.Join(Environment.NewLine, ((MockBuildEngine)task.BuildEngine).Errors.Select(e => e.Message));
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalCwd);
            foreach (var dir in _tempDirs)
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
