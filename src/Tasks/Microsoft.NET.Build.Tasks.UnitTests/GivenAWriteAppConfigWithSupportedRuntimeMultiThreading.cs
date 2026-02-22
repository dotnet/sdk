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

        [Fact]
        public void ItHandlesEmptyOutputAppConfigFileItemSpec()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            Directory.CreateDirectory(projectDir);
            try
            {
                var task = new WriteAppConfigWithSupportedRuntime();
                task.BuildEngine = new MockBuildEngine();
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                task.AppConfigFile = null;
                task.TargetFrameworkIdentifier = ".NETFramework";
                task.TargetFrameworkVersion = "v4.7.2";
                task.OutputAppConfigFile = new TaskItem("");

                var result = false;
                Exception? caught = null;
                try { result = task.Execute(); } catch (Exception ex) { caught = ex; }

                caught.Should().NotBeOfType<NullReferenceException>(
                    "empty OutputAppConfigFile should not cause NullReferenceException");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItHandlesNonNullAppConfigFileWithEmptyItemSpec()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            Directory.CreateDirectory(projectDir);
            try
            {
                var task = new WriteAppConfigWithSupportedRuntime();
                task.BuildEngine = new MockBuildEngine();
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                task.AppConfigFile = new TaskItem("");
                task.TargetFrameworkIdentifier = ".NETFramework";
                task.TargetFrameworkVersion = "v4.7.2";
                task.OutputAppConfigFile = new TaskItem("valid.config");

                var result = false;
                Exception? caught = null;
                try { result = task.Execute(); } catch (Exception ex) { caught = ex; }

                caught.Should().NotBeOfType<NullReferenceException>(
                    "empty AppConfigFile ItemSpec should not cause NullReferenceException");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItHandlesWhitespaceOnlyOutputAppConfigFileItemSpec()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            Directory.CreateDirectory(projectDir);
            try
            {
                var task = new WriteAppConfigWithSupportedRuntime();
                task.BuildEngine = new MockBuildEngine();
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                task.AppConfigFile = null;
                task.TargetFrameworkIdentifier = ".NETFramework";
                task.TargetFrameworkVersion = "v4.7.2";
                task.OutputAppConfigFile = new TaskItem("   ");

                var result = false;
                Exception? caught = null;
                try { result = task.Execute(); } catch (Exception ex) { caught = ex; }

                // Whitespace-only path should not cause NullReferenceException
                caught.Should().NotBeOfType<NullReferenceException>(
                    "whitespace-only OutputAppConfigFile should not cause NullReferenceException");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItHandlesWhitespaceOnlyAppConfigFileItemSpec()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            Directory.CreateDirectory(projectDir);
            try
            {
                var task = new WriteAppConfigWithSupportedRuntime();
                task.BuildEngine = new MockBuildEngine();
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                task.AppConfigFile = new TaskItem("   ");
                task.TargetFrameworkIdentifier = ".NETFramework";
                task.TargetFrameworkVersion = "v4.7.2";
                task.OutputAppConfigFile = new TaskItem("valid.config");

                var result = false;
                Exception? caught = null;
                try { result = task.Execute(); } catch (Exception ex) { caught = ex; }

                // Whitespace-only path should not cause NullReferenceException
                caught.Should().NotBeOfType<NullReferenceException>(
                    "whitespace-only AppConfigFile should not cause NullReferenceException");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameOutputInSingleProcessAndMultiProcessMode()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "writeappconfig-sp-mp-" + Guid.NewGuid().ToString("N")));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "writeappconfig-other-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);

            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // Create an app.config under projectDir
                var appConfigContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration></configuration>";
                File.WriteAllText(Path.Combine(projectDir, "app.config"), appConfigContent);

                // --- Single-process run: CWD == projectDir ---
                string singleProcessOutput;
                Directory.SetCurrentDirectory(projectDir);
                try
                {
                    var task = new WriteAppConfigWithSupportedRuntime();
                    task.BuildEngine = new MockBuildEngine();
                    task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
                    task.AppConfigFile = new TaskItem("app.config");
                    task.TargetFrameworkIdentifier = ".NETFramework";
                    task.TargetFrameworkVersion = "v4.7.2";
                    task.OutputAppConfigFile = new TaskItem("output_sp.config");

                    task.Execute().Should().BeTrue("single-process run should succeed");
                    singleProcessOutput = File.ReadAllText(Path.Combine(projectDir, "output_sp.config"));
                }
                finally
                {
                    Directory.SetCurrentDirectory(savedCwd);
                }

                // --- Multi-process run: CWD != projectDir ---
                string multiProcessOutput;
                Directory.SetCurrentDirectory(otherDir);
                try
                {
                    var task = new WriteAppConfigWithSupportedRuntime();
                    task.BuildEngine = new MockBuildEngine();
                    task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
                    task.AppConfigFile = new TaskItem("app.config");
                    task.TargetFrameworkIdentifier = ".NETFramework";
                    task.TargetFrameworkVersion = "v4.7.2";
                    task.OutputAppConfigFile = new TaskItem("output_mp.config");

                    task.Execute().Should().BeTrue("multi-process run should succeed");
                    multiProcessOutput = File.ReadAllText(Path.Combine(projectDir, "output_mp.config"));
                }
                finally
                {
                    Directory.SetCurrentDirectory(savedCwd);
                }

                // Assert both runs produced identical output
                multiProcessOutput.Should().Be(singleProcessOutput,
                    "single-process and multi-process modes should produce identical output XML");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }
    }
}
