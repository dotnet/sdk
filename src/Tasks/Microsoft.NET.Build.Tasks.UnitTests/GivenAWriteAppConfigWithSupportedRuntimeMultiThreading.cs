// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAWriteAppConfigWithSupportedRuntimeMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new WriteAppConfigWithSupportedRuntime();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(WriteAppConfigWithSupportedRuntime).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItResolvesOutputPathViaTaskEnvironment()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            Directory.CreateDirectory(projectDir);
            try
            {
                var task = new WriteAppConfigWithSupportedRuntime();
                task.BuildEngine = new MockBuildEngine();

                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                // No input app.config — the task creates an empty one
                task.AppConfigFile = null;
                task.TargetFrameworkIdentifier = ".NETFramework";
                task.TargetFrameworkVersion = "v4.7.2";
                task.OutputAppConfigFile = new TaskItem("output.config");

                var result = task.Execute();

                result.Should().BeTrue("the task should succeed when writing to project directory");
                File.Exists(Path.Combine(projectDir, "output.config")).Should().BeTrue(
                    "output file should be created under the project directory, not CWD");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItResolvesInputAppConfigPathViaTaskEnvironment()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Create an app.config file under the project dir
                var appConfigContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration></configuration>";
                File.WriteAllText(Path.Combine(projectDir, "app.config"), appConfigContent);

                var task = new WriteAppConfigWithSupportedRuntime();
                task.BuildEngine = new MockBuildEngine();

                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                task.AppConfigFile = new TaskItem("app.config");
                task.TargetFrameworkIdentifier = ".NETFramework";
                task.TargetFrameworkVersion = "v4.7.2";
                task.OutputAppConfigFile = new TaskItem("output.config");

                var result = task.Execute();

                result.Should().BeTrue("the task should succeed when reading app.config from project directory");
                File.Exists(Path.Combine(projectDir, "output.config")).Should().BeTrue(
                    "output file should be created under the project directory");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
