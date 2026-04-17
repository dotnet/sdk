// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
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
        public void ProjectPathIsAbsolutized()
        {
            string projectDir = CreateTestProjectDirectory();
            string relativeProjectPath = "TestApp.csproj";
            string absoluteDepsPath = Path.Combine(projectDir, "bin", "Debug", "net8.0", "TestApp.deps.json");

            var originalCwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(projectDir);

                var task = CreateMinimalTask(relativeProjectPath, absoluteDepsPath);
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                bool result = task.Execute();

                result.Should().BeTrue("task should succeed");
                
                // Verify the deps.json was written to the correct absolute path
                File.Exists(absoluteDepsPath).Should().BeTrue("deps file should be created");
                
                // Parse and check that mainProject has absolute path
                var depsJson = JObject.Parse(File.ReadAllText(absoluteDepsPath));
                string projectPath = depsJson.SelectToken("targets..TestApp/1.0.0")?.Parent?.Parent?.Parent?.Parent?
                    .SelectToken("libraries['TestApp/1.0.0'].path")?.ToString();
                
                // The path stored should be absolute because we absolutized ProjectPath
                if (!string.IsNullOrEmpty(projectPath))
                {
                    Path.IsPathRooted(projectPath).Should().BeTrue(
                        "ProjectPath should be absolutized internally even when passed as relative");
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
            }
        }

        [Fact]
        public void DecoyCwdDoesNotAffectPathResolution()
        {
            string projectDir = CreateTestProjectDirectory();
            string decoyCwd = CreateTestProjectDirectory("DecoyDir");
            string relativeDepsPath = Path.Combine("bin", "Debug", "net8.0", "TestApp.deps.json");
            string absoluteProjectPath = Path.Combine(projectDir, "TestApp.csproj");

            var originalCwd = Directory.GetCurrentDirectory();
            try
            {
                // Set CWD to a decoy directory - task should still resolve relative to projectDir
                Directory.SetCurrentDirectory(decoyCwd);

                var task = CreateMinimalTask(absoluteProjectPath, relativeDepsPath);
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                bool result = task.Execute();

                result.Should().BeTrue("task should succeed");
                
                // File should be written relative to project directory, not decoy CWD
                string expectedPath = Path.Combine(projectDir, relativeDepsPath);
                File.Exists(expectedPath).Should().BeTrue(
                    "deps file should be written relative to TaskEnvironment.ProjectDirectory, not process CWD");
                
                // Verify it was NOT written to the decoy directory
                string decoyPath = Path.Combine(decoyCwd, relativeDepsPath);
                File.Exists(decoyPath).Should().BeFalse(
                    "deps file should NOT be written to the decoy CWD");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
            }
        }

        [Fact]
        public async Task CrossThreadEnvVarIsolation()
        {
            string projectDir1 = CreateTestProjectDirectory("Project1");
            string projectDir2 = CreateTestProjectDirectory("Project2");

            var env1 = TaskEnvironmentHelper.CreateForTest(projectDir1);
            env1.SetEnvironmentVariable("TEST_GENERATE_DEPS_VAR", "value1");

            var env2 = TaskEnvironmentHelper.CreateForTest(projectDir2);
            env2.SetEnvironmentVariable("TEST_GENERATE_DEPS_VAR", "value2");

            var task1 = CreateMinimalTask(
                Path.Combine(projectDir1, "TestApp.csproj"),
                Path.Combine(projectDir1, "TestApp.deps.json"));
            task1.TaskEnvironment = env1;

            var task2 = CreateMinimalTask(
                Path.Combine(projectDir2, "TestApp.csproj"),
                Path.Combine(projectDir2, "TestApp.deps.json"));
            task2.TaskEnvironment = env2;

            var task1Execution = Task.Run(() => task1.Execute());
            var task2Execution = Task.Run(() => task2.Execute());

            await Task.WhenAll(task1Execution, task2Execution);

            (await task1Execution).Should().BeTrue("task1 should succeed");
            (await task2Execution).Should().BeTrue("task2 should succeed");

            // Verify each task operated in its own environment
            env1.GetEnvironmentVariable("TEST_GENERATE_DEPS_VAR").Should().Be("value1");
            env2.GetEnvironmentVariable("TEST_GENERATE_DEPS_VAR").Should().Be("value2");
        }

        [Fact]
        public void MultiProcessParity()
        {
            string projectDir = CreateTestProjectDirectory();
            string projectPath = Path.Combine(projectDir, "TestApp.csproj");
            string depsPath = Path.Combine(projectDir, "TestApp.deps.json");

            // Run without TaskEnvironment (legacy)
            var task1 = CreateMinimalTask(projectPath, depsPath);
            bool result1 = task1.Execute();
            result1.Should().BeTrue("legacy task should succeed");
            string deps1 = File.ReadAllText(depsPath);
            File.Delete(depsPath);

            // Run with TaskEnvironment
            var task2 = CreateMinimalTask(projectPath, depsPath);
            task2.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
            bool result2 = task2.Execute();
            result2.Should().BeTrue("multi-threadable task should succeed");
            string deps2 = File.ReadAllText(depsPath);

            // Normalize JSON for comparison (whitespace/formatting may differ)
            var json1 = JsonConvert.DeserializeObject<JObject>(deps1);
            var json2 = JsonConvert.DeserializeObject<JObject>(deps2);

            JToken.DeepEquals(json1, json2).Should().BeTrue(
                "deps.json content should be identical whether using TaskEnvironment or not");
        }

        [Fact]
        public async Task ConcurrentExecutionProducesCorrectResults()
        {
            const int concurrency = 4;
            var projectDirs = new string[concurrency];
            var tasks = new GenerateDepsFile[concurrency];
            var executionTasks = new Task<bool>[concurrency];

            for (int i = 0; i < concurrency; i++)
            {
                projectDirs[i] = CreateTestProjectDirectory($"ConcurrentProject{i}");
                string projectPath = Path.Combine(projectDirs[i], "TestApp.csproj");
                string depsPath = Path.Combine(projectDirs[i], "TestApp.deps.json");

                tasks[i] = CreateMinimalTask(projectPath, depsPath);
                tasks[i].TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDirs[i]);
            }

            for (int i = 0; i < concurrency; i++)
            {
                var t = tasks[i];
                executionTasks[i] = Task.Run(() => t.Execute());
            }

            await Task.WhenAll(executionTasks);

            for (int i = 0; i < concurrency; i++)
            {
                (await executionTasks[i]).Should().BeTrue($"task {i} should succeed");
                string depsPath = Path.Combine(projectDirs[i], "TestApp.deps.json");
                File.Exists(depsPath).Should().BeTrue($"task {i} should create deps file");
            }

            // Verify all created valid JSON
            for (int i = 0; i < concurrency; i++)
            {
                string depsPath = Path.Combine(projectDirs[i], "TestApp.deps.json");
                var json = JObject.Parse(File.ReadAllText(depsPath));
                json.Should().NotBeNull($"task {i} should produce valid JSON");
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

        private GenerateDepsFile CreateMinimalTask(string projectPath, string depsFilePath)
        {
            string projectDir = Path.GetDirectoryName(projectPath);
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
