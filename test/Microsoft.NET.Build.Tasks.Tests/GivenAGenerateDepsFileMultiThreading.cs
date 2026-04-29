// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [CollectionDefinition("CwdSensitive", DisableParallelization = true)]
    public sealed class CwdSensitiveCollection
    {
    }

    [Collection("CwdSensitive")]
    public class GivenAGenerateDepsFileMultiThreading : IDisposable
    {
        private readonly List<string> _tempDirs = new();
        private readonly string _originalCwd = Directory.GetCurrentDirectory();

        [Fact]
        public void OutputRelativityIsPreserved()
        {
            string projectDir = CreateTestProjectDirectory();
            string relativeDepsPath = Path.Combine("bin", "Debug", "net8.0", "TestApp.deps.json");
            string absoluteProjectPath = Path.Combine(projectDir, "TestApp.csproj");

            var task = CreateMinimalTask(absoluteProjectPath, relativeDepsPath);
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

            bool result = task.Execute();

            result.Should().BeTrue("task should succeed");
            task.FilesWritten.Should().HaveCount(1, "should write one file");
            task.FilesWritten[0].ItemSpec.Should().Be(relativeDepsPath,
                "FilesWritten should preserve the original relative path, not absolutize it");
        }

        [Fact]
        public void TaskEnvironmentIsRequired()
        {
            string projectDir = CreateTestProjectDirectory();
            string relativeDepsPath = Path.Combine("bin", "Debug", "net8.0", "TestApp.deps.json");

            var task = CreateMinimalTask(Path.Combine(projectDir, "TestApp.csproj"), relativeDepsPath, projectDir);

            Action act = () => task.Execute();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"*{nameof(TaskEnvironment)}*");
        }

        [Fact]
        public void AssetsFilePathIsResolvedThroughTaskEnvironment()
        {
            string projectDir = CreateTestProjectDirectory();
            string decoyCwd = CreateTestProjectDirectory("AssetsDecoy");
            string relativeAssetsPath = Path.Combine("obj", "project.assets.json");
            CreateAssetsFile(projectDir, relativeAssetsPath);

            var originalCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(decoyCwd);

                var task = CreateMinimalTask("TestApp.csproj", "assets.deps.json", projectDir);
                task.AssetsFilePath = relativeAssetsPath;
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                bool result = task.Execute();

                result.Should().BeTrue(
                    "AssetsFilePath should be resolved from TaskEnvironment.ProjectDirectory, not process CWD. Errors: {0}",
                    GetLoggedErrors(task));
                File.Exists(Path.Combine(projectDir, "assets.deps.json")).Should().BeTrue(
                    "deps file should be written under the task environment project directory");
                File.Exists(Path.Combine(decoyCwd, "assets.deps.json")).Should().BeFalse(
                    "deps file should not be written under the process CWD");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
            }
        }

        [Fact]
        public void RuntimeGraphPathIsResolvedThroughTaskEnvironment()
        {
            string projectDir = CreateTestProjectDirectory();
            string decoyCwd = CreateTestProjectDirectory("RuntimeGraphDecoy");
            string decoyRuntimeGraphPath = Path.Combine(decoyCwd, "runtime.json");
            File.Delete(decoyRuntimeGraphPath);
            string relativeAssetsPath = Path.Combine("obj", "project.assets.json");
            CreateAssetsFile(projectDir, relativeAssetsPath);

            var originalCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(decoyCwd);
                File.Exists(decoyRuntimeGraphPath).Should().BeFalse(
                    "the process CWD must not contain a runtime graph that could satisfy RuntimeGraphPath");

                var task = CreateMinimalTask("TestApp.csproj", "selfcontained.deps.json", projectDir);
                task.AssetsFilePath = relativeAssetsPath;
                task.RuntimeGraphPath = "runtime.json";
                task.IsSelfContained = true;
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                bool result = task.Execute();

                result.Should().BeTrue(
                    "RuntimeGraphPath should be resolved from TaskEnvironment.ProjectDirectory, not process CWD. Errors: {0}",
                    GetLoggedErrors(task));
                File.Exists(Path.Combine(projectDir, "selfcontained.deps.json")).Should().BeTrue(
                    "the relative runtime graph should be read from the task environment project directory");
                File.Exists(Path.Combine(decoyCwd, "selfcontained.deps.json")).Should().BeFalse(
                    "deps file should not be written under the process CWD");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
            }
        }

        [Fact]
        public void GenerateDepsFileHasMSBuildMultiThreadableMarkers()
        {
            typeof(GenerateDepsFile).Should().BeAssignableTo<IMultiThreadableTask>();
            typeof(GenerateDepsFile)
                .GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), inherit: false)
                .Should()
                .ContainSingle("MSBuild should recognize GenerateDepsFile as multi-threadable through the marker attribute");
        }

        [Fact]
        public void ProjectDirectoryControlsRelativePathResolution()
        {
            string projectDir = CreateTestProjectDirectory();
            string decoyCwd = CreateTestProjectDirectory("DecoyDir");
            string relativeProjectPath = "TestApp.csproj";
            string relativeDepsPath = Path.Combine("bin", "Debug", "net8.0", "TestApp.deps.json");

            var originalCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(decoyCwd);

                var task = CreateMinimalTask(relativeProjectPath, relativeDepsPath, projectDir);
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                bool result = task.Execute();

                result.Should().BeTrue("task should resolve relative paths from TaskEnvironment.ProjectDirectory");

                string expectedPath = Path.Combine(projectDir, relativeDepsPath);
                File.Exists(expectedPath).Should().BeTrue(
                    "deps file should be written relative to TaskEnvironment.ProjectDirectory, not process CWD");

                string decoyPath = Path.Combine(decoyCwd, relativeDepsPath);
                File.Exists(decoyPath).Should().BeFalse(
                    "deps file should not be written to the process CWD");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
            }
        }

        [Fact]
        public void InjectedTaskEnvironmentProducesSameDepsJsonForAbsoluteAndRelativeProjectPathInputs()
        {
            string projectDir = CreateTestProjectDirectory();
            string decoyCwd = CreateTestProjectDirectory("ParityDecoy");
            string absoluteDepsPath = Path.Combine(projectDir, "absolute.deps.json");
            string relativeDepsPath = "relative.deps.json";
            string assetsPath = CreateAssetsFile(projectDir, Path.Combine("obj", "project.assets.json"), includeProjectReference: true);
            ITaskItem projectReference = CreateProjectReference(projectDir);

            var originalCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(decoyCwd);

                var absoluteTask = CreateMinimalTask(
                    Path.Combine(projectDir, "TestApp.csproj"),
                    absoluteDepsPath,
                    projectDir);
                absoluteTask.AssetsFilePath = assetsPath;
                absoluteTask.ReferencePaths = [projectReference];
                absoluteTask.UserRuntimeAssemblies = [projectReference];
                absoluteTask.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
                absoluteTask.Execute().Should().BeTrue("absolute input task should succeed");

                var relativeTask = CreateMinimalTask("TestApp.csproj", relativeDepsPath, projectDir);
                relativeTask.AssetsFilePath = assetsPath;
                relativeTask.ReferencePaths = [projectReference];
                relativeTask.UserRuntimeAssemblies = [projectReference];
                relativeTask.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
                relativeTask.Execute().Should().BeTrue("relative input task should succeed");

                var absoluteJson = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(absoluteDepsPath));
                var relativeJson = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(Path.Combine(projectDir, relativeDepsPath)));

                FindTargetLibrary(relativeJson, "ReferenceProject/1.0.0").Should().NotBeNull(
                    "relative ProjectPath should be absolutized before project-reference lookup");
                JToken.DeepEquals(absoluteJson, relativeJson).Should().BeTrue(
                    "relative paths should be absolutized through TaskEnvironment before deps generation");
                relativeTask.FilesWritten[0].ItemSpec.Should().Be(relativeDepsPath,
                    "FilesWritten should preserve the caller's original relative output path");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
            }
        }

        [Fact]
        public async Task ConcurrentExecutionWithRelativePathsKeepsCwdStable()
        {
            const int concurrency = 64;
            string decoyCwd = CreateTestProjectDirectory("StressDecoy");
            var projectDirs = new string[concurrency];

            for (int i = 0; i < concurrency; i++)
            {
                projectDirs[i] = CreateTestProjectDirectory($"StressProject{i}");
            }

            using var startBarrier = new Barrier(concurrency);
            var failures = new ConcurrentBag<Exception>();
            var executionTasks = new Task[concurrency];
            var originalCwd = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(decoyCwd);

                for (int i = 0; i < concurrency; i++)
                {
                    int taskIndex = i;
                    executionTasks[i] = Task.Factory.StartNew(() =>
                    {
                        string relativeDepsPath = Path.Combine("bin", "Debug", "net8.0", $"TestApp{taskIndex}.deps.json");
                        try
                        {
                            startBarrier.SignalAndWait(TimeSpan.FromSeconds(30));

                            var task = CreateMinimalTask("TestApp.csproj", relativeDepsPath, projectDirs[taskIndex]);
                            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDirs[taskIndex]);

                            task.Execute().Should().BeTrue($"task {taskIndex} should succeed");
                            File.Exists(Path.Combine(projectDirs[taskIndex], relativeDepsPath)).Should().BeTrue(
                                $"task {taskIndex} should write under its project directory");
                            File.Exists(Path.Combine(decoyCwd, relativeDepsPath)).Should().BeFalse(
                                $"task {taskIndex} should not write under the process CWD");
                            task.FilesWritten[0].ItemSpec.Should().Be(relativeDepsPath);
                        }
                        catch (Exception ex)
                        {
                            failures.Add(ex);
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }

                await Task.WhenAll(executionTasks);

                failures.Should().BeEmpty();
                Directory.GetCurrentDirectory().Should().Be(decoyCwd,
                    "GenerateDepsFile must not mutate process CWD during concurrent execution");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
            }
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

        private static ITaskItem CreateProjectReference(string projectDir)
        {
            string referenceProjectDir = Path.Combine(projectDir, "ReferenceProject");
            string referenceProjectPath = Path.Combine(referenceProjectDir, "ReferenceProject.csproj");
            string referenceAssemblyPath = Path.Combine(referenceProjectDir, "bin", "Debug", "net8.0", "ReferenceProject.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(referenceAssemblyPath));
            File.WriteAllText(referenceProjectPath, "<Project />");
            File.WriteAllText(referenceAssemblyPath, string.Empty);

            var reference = new TaskItem(referenceAssemblyPath);
            reference.SetMetadata(MetadataKeys.ReferenceSourceTarget, "ProjectReference");
            reference.SetMetadata(MetadataKeys.MSBuildSourceProjectFile, referenceProjectPath);
            reference.SetMetadata("Version", "1.0.0");
            return reference;
        }

        private static string CreateAssetsFile(
            string projectDir,
            string relativeAssetsPath,
            string targetName = "net8.0",
            bool includeProjectReference = false)
        {
            string assetsPath = Path.Combine(projectDir, relativeAssetsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(assetsPath));

            var targetLibraries = new JObject();
            var libraries = new JObject();
            var projectFileDependencies = new JArray();
            var frameworkDependencies = new JObject();
            var restoreProjectReferences = new JObject();

            if (includeProjectReference)
            {
                string relativeProjectReferencePath = Path.Combine("ReferenceProject", "ReferenceProject.csproj");
                string projectReferenceName = "ReferenceProject/1.0.0";

                targetLibraries[projectReferenceName] = new JObject
                {
                    ["type"] = "project",
                    ["compile"] = new JObject
                    {
                        ["bin/ReferenceProject.dll"] = new JObject()
                    },
                    ["runtime"] = new JObject
                    {
                        ["bin/ReferenceProject.dll"] = new JObject()
                    }
                };
                libraries[projectReferenceName] = new JObject
                {
                    ["type"] = "project",
                    ["path"] = relativeProjectReferencePath,
                    ["msbuildProject"] = relativeProjectReferencePath
                };
                projectFileDependencies.Add("ReferenceProject >= 1.0.0");
                frameworkDependencies["ReferenceProject"] = new JObject
                {
                    ["target"] = "Project",
                    ["version"] = "[1.0.0, )"
                };
                restoreProjectReferences[relativeProjectReferencePath] = new JObject
                {
                    ["projectPath"] = Path.Combine(projectDir, relativeProjectReferencePath)
                };
            }

            var assets = new JObject
            {
                ["version"] = 3,
                ["targets"] = new JObject
                {
                    [targetName] = targetLibraries
                },
                ["libraries"] = libraries,
                ["projectFileDependencyGroups"] = new JObject
                {
                    ["net8.0"] = projectFileDependencies
                },
                ["packageFolders"] = new JObject(),
                ["project"] = new JObject
                {
                    ["version"] = "1.0.0",
                    ["restore"] = new JObject
                    {
                        ["projectUniqueName"] = Path.Combine(projectDir, "TestApp.csproj"),
                        ["projectName"] = "TestApp",
                        ["projectPath"] = Path.Combine(projectDir, "TestApp.csproj"),
                        ["packagesPath"] = Path.Combine(projectDir, ".nuget", "packages"),
                        ["outputPath"] = Path.Combine(projectDir, "obj"),
                        ["projectStyle"] = "PackageReference",
                        ["fallbackFolders"] = new JArray(),
                        ["configFilePaths"] = new JArray(),
                        ["originalTargetFrameworks"] = new JArray("net8.0"),
                        ["sources"] = new JObject(),
                        ["frameworks"] = new JObject
                        {
                            ["net8.0"] = new JObject
                            {
                                ["targetAlias"] = "net8.0",
                                ["projectReferences"] = restoreProjectReferences
                            }
                        },
                        ["warningProperties"] = new JObject
                        {
                            ["warnAsError"] = new JArray("NU1605")
                        }
                    },
                    ["frameworks"] = new JObject
                    {
                        ["net8.0"] = new JObject
                        {
                            ["targetAlias"] = "net8.0",
                            ["dependencies"] = frameworkDependencies,
                            ["runtimeIdentifierGraphPath"] = Path.Combine(projectDir, "runtime.json")
                        }
                    }
                }
            };

            File.WriteAllText(assetsPath, assets.ToString(Formatting.Indented));
            return assetsPath;
        }

        private static JToken FindTargetLibrary(JObject depsJson, string libraryName)
        {
            return ((JObject)depsJson["targets"])
                .Properties()
                .Select(target => target.Value[libraryName])
                .FirstOrDefault(library => library != null);
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
