// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACreateAppHostMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new CreateAppHost();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(CreateAppHost).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItResolvesPathsViaTaskEnvironment()
        {
            // Create a temp directory to act as a fake project dir (different from CWD).
            // If the task resolves via TaskEnvironment (correct), it finds the files.
            // If it resolves via CWD (incorrect), it gets FileNotFoundException.
            var projectDir = Path.Combine(Path.GetTempPath(), "apphost-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Set up relative paths for the task inputs
                var sourceRelativePath = Path.Combine("input", "apphost.exe");
                var destRelativePath = Path.Combine("output", "myapp.exe");
                var assemblyRelativePath = Path.Combine("input", "myapp.dll");

                // Create the expected files at the project-dir-relative locations
                var sourceAbsolutePath = Path.Combine(projectDir, sourceRelativePath);
                var destAbsoluteDir = Path.Combine(projectDir, "output");
                var assemblyAbsolutePath = Path.Combine(projectDir, assemblyRelativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(sourceAbsolutePath)!);
                Directory.CreateDirectory(destAbsoluteDir);

                // Write a minimal file to serve as the apphost source — not a real PE,
                // so HostWriter will throw. That's fine — we're testing path resolution.
                File.WriteAllText(sourceAbsolutePath, "not a real apphost");
                File.WriteAllText(assemblyAbsolutePath, "not a real assembly");

                var task = new CreateAppHost
                {
                    AppHostSourcePath = sourceRelativePath,
                    AppHostDestinationPath = destRelativePath,
                    AppBinaryName = "myapp.dll",
                    IntermediateAssembly = assemblyRelativePath,
                    Retries = 0
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;

                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property (from IMultiThreadableTask)");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                // Execute — HostWriter.CreateAppHost will fail because the source isn't a real
                // apphost binary. But if path resolution fails first (FileNotFoundException),
                // that means the task didn't use TaskEnvironment to resolve paths.
                // We expect a BuildErrorException (caught by TaskBase and logged as error),
                // NOT a raw FileNotFoundException.
                task.Execute();

                // The task should have attempted to open the files (proving path resolution
                // worked via TaskEnvironment). The error should be about invalid apphost content,
                // NOT about files not being found.
                bool hasFileNotFound = mockEngine.Errors.Any(e =>
                    e.Message?.Contains("FileNotFoundException") == true ||
                    e.Message?.Contains("Could not find file") == true ||
                    e.Message?.Contains("could not be found") == true);

                hasFileNotFound.Should().BeFalse(
                    "the task should resolve paths via TaskEnvironment, not CWD — " +
                    "file-not-found errors indicate CWD-relative resolution");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void PathResolutionMatchesBetweenSingleAndMultiProcessMode()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "apphost-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Both TaskEnvironments point to the same directory — they should resolve identically.
                var singleEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
                var multiEnv = TaskEnvironmentHelper.CreateForTest(projectDir);

                // Test with relative paths
                var relativePaths = new[]
                {
                    Path.Combine("input", "apphost.exe"),
                    Path.Combine("output", "myapp.exe"),
                    Path.Combine("bin", "Debug", "net8.0", "myapp.dll"),
                    "simple.dll"
                };

                foreach (var relativePath in relativePaths)
                {
                    var singleResolved = singleEnv.GetAbsolutePath(relativePath);
                    var multiResolved = multiEnv.GetAbsolutePath(relativePath);

                    ((string)singleResolved).Should().Be((string)multiResolved,
                        $"relative path '{relativePath}' should resolve identically in both modes");
                }

                // Test with absolute paths — should pass through unchanged
                var absolutePaths = new[]
                {
                    Path.Combine(projectDir, "input", "apphost.exe"),
                    Path.GetTempFileName()
                };

                foreach (var absolutePath in absolutePaths)
                {
                    var singleResolved = singleEnv.GetAbsolutePath(absolutePath);
                    var multiResolved = multiEnv.GetAbsolutePath(absolutePath);

                    ((string)singleResolved).Should().Be((string)multiResolved,
                        $"absolute path '{absolutePath}' should resolve identically in both modes");
                    ((string)singleResolved).Should().Be(absolutePath,
                        "absolute paths should pass through unchanged");
                }

                // Create dummy files so both task instances hit the same error path
                Directory.CreateDirectory(Path.Combine(projectDir, "input"));
                Directory.CreateDirectory(Path.Combine(projectDir, "output"));
                Directory.CreateDirectory(Path.Combine(projectDir, "bin"));
                File.WriteAllText(Path.Combine(projectDir, "input", "apphost.exe"), "not a real apphost");
                File.WriteAllText(Path.Combine(projectDir, "bin", "myapp.dll"), "not a real assembly");

                // Verify that the task itself uses TaskEnvironment for all path inputs
                var singleTask = new CreateAppHost
                {
                    AppHostSourcePath = Path.Combine("input", "apphost.exe"),
                    AppHostDestinationPath = Path.Combine("output", "myapp.exe"),
                    AppBinaryName = "myapp.dll",
                    IntermediateAssembly = Path.Combine("bin", "myapp.dll"),
                    Retries = 0,
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = singleEnv
                };

                var multiTask = new CreateAppHost
                {
                    AppHostSourcePath = Path.Combine("input", "apphost.exe"),
                    AppHostDestinationPath = Path.Combine("output", "myapp.exe"),
                    AppBinaryName = "myapp.dll",
                    IntermediateAssembly = Path.Combine("bin", "myapp.dll"),
                    Retries = 0,
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = multiEnv
                };

                // Both tasks will fail (no real PE files) but we verify they get the same errors
                singleTask.Execute();
                multiTask.Execute();

                var singleErrors = ((MockBuildEngine)singleTask.BuildEngine).Errors
                    .Select(e => e.Message).ToList();
                var multiErrors = ((MockBuildEngine)multiTask.BuildEngine).Errors
                    .Select(e => e.Message).ToList();

                multiErrors.Should().BeEquivalentTo(singleErrors,
                    "both modes should produce identical error messages when given the same inputs");
            }
            finally
            {
                try { Directory.Delete(projectDir, true); } catch { }
            }
        }
    }
}
