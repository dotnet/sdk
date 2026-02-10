// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateRuntimeConfigMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new GenerateRuntimeConfigurationFiles();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(GenerateRuntimeConfigurationFiles).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItWritesRuntimeConfigViaTaskEnvironment()
        {
            // Create a temp directory to act as a fake project dir (different from CWD).
            // If the task resolves RuntimeConfigPath via TaskEnvironment (correct), it creates the file.
            // If it resolves via CWD (incorrect), it writes to the wrong location or fails.
            var projectDir = Path.Combine(Path.GetTempPath(), "rtconfig-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var configRelativePath = Path.Combine("bin", "test.runtimeconfig.json");
                var configAbsolutePath = Path.Combine(projectDir, configRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(configAbsolutePath)!);

                var runtimeFramework = new TaskItem("Microsoft.NETCore.App");
                runtimeFramework.SetMetadata("FrameworkName", "Microsoft.NETCore.App");
                runtimeFramework.SetMetadata("Version", "8.0.0");

                var task = new GenerateRuntimeConfigurationFiles
                {
                    TargetFramework = ".NETCoreApp,Version=v8.0",
                    TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                    RuntimeConfigPath = configRelativePath,
                    RuntimeFrameworks = new ITaskItem[] { runtimeFramework },
                    IsSelfContained = false,
                    GenerateRuntimeConfigDevFile = false,
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;

                // Set TaskEnvironment pointing to projectDir (different from CWD).
                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property (from IMultiThreadableTask)");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                // Execute — should write runtimeconfig.json under projectDir via TaskEnvironment
                task.Execute().Should().BeTrue(
                    string.Join("; ", mockEngine.Errors.Select(e => e.Message)));

                // The runtimeconfig.json file should exist at the absolute path under projectDir
                File.Exists(configAbsolutePath).Should().BeTrue(
                    "the runtimeconfig file should be created at the path resolved via TaskEnvironment, not CWD");

                // Verify it's valid JSON with framework info
                var content = File.ReadAllText(configAbsolutePath);
                content.Should().Contain("Microsoft.NETCore.App");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItReadsUserRuntimeConfigViaTaskEnvironment()
        {
            // Tests that UserRuntimeConfig path is resolved via TaskEnvironment
            var projectDir = Path.Combine(Path.GetTempPath(), "rtconfig-user-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var configRelativePath = Path.Combine("bin", "test.runtimeconfig.json");
                var configAbsolutePath = Path.Combine(projectDir, configRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(configAbsolutePath)!);

                // Create a user runtime config at a relative path under projectDir
                var userConfigRelativePath = "runtimeconfig.template.json";
                var userConfigAbsolutePath = Path.Combine(projectDir, userConfigRelativePath);
                File.WriteAllText(userConfigAbsolutePath, "{\"configProperties\":{\"TestProperty\":true}}");

                var runtimeFramework = new TaskItem("Microsoft.NETCore.App");
                runtimeFramework.SetMetadata("FrameworkName", "Microsoft.NETCore.App");
                runtimeFramework.SetMetadata("Version", "8.0.0");

                var task = new GenerateRuntimeConfigurationFiles
                {
                    TargetFramework = ".NETCoreApp,Version=v8.0",
                    TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                    RuntimeConfigPath = configRelativePath,
                    RuntimeFrameworks = new ITaskItem[] { runtimeFramework },
                    IsSelfContained = false,
                    GenerateRuntimeConfigDevFile = false,
                    UserRuntimeConfig = userConfigRelativePath,
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;

                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property (from IMultiThreadableTask)");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                task.Execute().Should().BeTrue(
                    string.Join("; ", mockEngine.Errors.Select(e => e.Message)));

                // Verify the user runtime config was merged into the output
                var content = File.ReadAllText(configAbsolutePath);
                content.Should().Contain("TestProperty",
                    "user runtime config should be read via TaskEnvironment path resolution");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
