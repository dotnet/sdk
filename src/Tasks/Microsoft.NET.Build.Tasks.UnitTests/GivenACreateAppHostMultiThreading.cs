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
    }
}
