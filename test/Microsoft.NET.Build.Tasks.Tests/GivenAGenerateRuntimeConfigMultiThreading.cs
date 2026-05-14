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

                var mockEngine = new MockBuildEngine();
                var task = new GenerateRuntimeConfigurationFiles
                {
                    BuildEngine = mockEngine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    TargetFramework = ".NETCoreApp,Version=v8.0",
                    TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                    RuntimeConfigPath = configRelativePath,
                    RuntimeFrameworks = new ITaskItem[] { runtimeFramework },
                    IsSelfContained = false,
                    GenerateRuntimeConfigDevFile = false,
                };

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
        public void ItBehavesSameWithEmptyAssetsFilePathInBothEnvironments()
        {
            // When AssetsFilePath = "", the task enters the else branch (not null)
            // and calls TaskEnvironment.GetAbsolutePath(""), which should fail the same way
            // regardless of whether CWD matches the project directory or not.
            var projectDir = Path.Combine(Path.GetTempPath(), "rtconfig-empty-assets-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "rtconfig-empty-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var configRelativePath = Path.Combine("bin", "test.runtimeconfig.json");
                Directory.CreateDirectory(Path.Combine(projectDir, "bin"));

                // --- Multi-process mode: CWD == projectDir; TaskEnvironment.Fallback reads live CWD ---
                Directory.SetCurrentDirectory(projectDir);
                var (mpResult, mpEngine, mpException) = RunTaskWithAssetsFilePath("", configRelativePath, projectDir, TaskEnvironment.Fallback);

                // --- Multi-threaded mode: CWD == otherDir; isolated TaskEnvironment carries projectDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (mtResult, mtEngine, mtException) = RunTaskWithAssetsFilePath(
                    "", configRelativePath, projectDir,
                    TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir));

                // Both should produce the same outcome
                mpResult.Should().Be(mtResult,
                    "task return value should be the same in both environments");
                mpEngine.Errors.Count.Should().Be(mtEngine.Errors.Count,
                    "error count should be the same in both environments");
                for (int i = 0; i < mpEngine.Errors.Count; i++)
                {
                    mpEngine.Errors[i].Message.Should().Be(mtEngine.Errors[i].Message,
                        $"error message [{i}] should be identical in both environments");
                }

                // Both should throw the same exception type (or both succeed without exception)
                if (mpException != null)
                {
                    mtException.Should().NotBeNull("both environments should throw the same exception");
                    mtException!.GetType().Should().Be(mpException.GetType(),
                        "exception type should match between environments");
                }
                else
                {
                    mtException.Should().BeNull("neither environment should throw if one doesn't");
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir))
                    Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir))
                    Directory.Delete(otherDir, true);
            }
        }

        private static (bool? result, MockBuildEngine engine, Exception? exception) RunTaskWithAssetsFilePath(
            string assetsFilePath, string configRelativePath, string projectDir, TaskEnvironment taskEnvironment)
        {
            return RunTaskCore(projectDir, configRelativePath, taskEnvironment, assetsFilePath: assetsFilePath);
        }

        private static (bool? result, MockBuildEngine engine, Exception? exception) RunTaskCore(
            string projectDir,
            string? runtimeConfigPath,
            TaskEnvironment taskEnvironment,
            string? assetsFilePath = null,
            string? runtimeConfigDevPath = null,
            string? userRuntimeConfig = null)
        {
            var runtimeFramework = new TaskItem("Microsoft.NETCore.App");
            runtimeFramework.SetMetadata("FrameworkName", "Microsoft.NETCore.App");
            runtimeFramework.SetMetadata("Version", "8.0.0");

            var task = new GenerateRuntimeConfigurationFiles
            {
                TargetFramework = ".NETCoreApp,Version=v8.0",
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                RuntimeConfigPath = runtimeConfigPath,
                RuntimeFrameworks = new ITaskItem[] { runtimeFramework },
                IsSelfContained = false,
                GenerateRuntimeConfigDevFile = false,
                AssetsFilePath = assetsFilePath,
                TaskEnvironment = taskEnvironment,
            };
            if (runtimeConfigDevPath != null)
            {
                task.RuntimeConfigDevPath = runtimeConfigDevPath;
            }
            if (userRuntimeConfig != null)
            {
                task.UserRuntimeConfig = userRuntimeConfig;
            }
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;

            bool? result = null;
            Exception? caught = null;
            try
            {
                result = task.Execute();
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            return (result, engine, caught);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ItBehavesSameWithEmptyOrNullRuntimeConfigPathInBothEnvironments(string? runtimeConfigPath)
        {
            // WriteRuntimeConfig calls TaskEnvironment.GetAbsolutePath(RuntimeConfigPath).
            // When RuntimeConfigPath is "" or null, this should fail the same way
            // regardless of whether CWD matches the project directory or not.
            var projectDir = Path.Combine(Path.GetTempPath(), "rtconfig-path-test-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "rtconfig-path-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // --- Multi-process mode: CWD == projectDir; TaskEnvironment.Fallback ---
                Directory.SetCurrentDirectory(projectDir);
                var (mpResult, mpEngine, mpException) = RunTaskCore(projectDir, runtimeConfigPath, TaskEnvironment.Fallback);

                // --- Multi-threaded mode: CWD == otherDir; isolated TaskEnvironment ---
                Directory.SetCurrentDirectory(otherDir);
                var (mtResult, mtEngine, mtException) = RunTaskCore(
                    projectDir, runtimeConfigPath,
                    TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir));

                // Both should produce the same outcome
                mpResult.Should().Be(mtResult,
                    "task return value should be the same in both environments");
                mpEngine.Errors.Count.Should().Be(mtEngine.Errors.Count,
                    "error count should be the same in both environments");
                for (int i = 0; i < mpEngine.Errors.Count; i++)
                {
                    mpEngine.Errors[i].Message.Should().Be(mtEngine.Errors[i].Message,
                        $"error message [{i}] should be identical in both environments");
                }

                // Both should throw the same exception type (or both not throw)
                if (mpException != null)
                {
                    mtException.Should().NotBeNull(
                        "both environments should throw the same exception for RuntimeConfigPath={0}", runtimeConfigPath ?? "(null)");
                    mtException!.GetType().Should().Be(mpException.GetType(),
                        "exception type should match between environments");
                }
                else
                {
                    mtException.Should().BeNull(
                        "neither environment should throw if one doesn't");
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir))
                    Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir))
                    Directory.Delete(otherDir, true);
            }
        }

        [Fact]
        public void UserRuntimeConfigProducesSameOutputInBothEnvironments()
        {
            // AddUserRuntimeOptions calls TaskEnvironment.GetAbsolutePath(UserRuntimeConfig).
            // Verify that the merged runtimeconfig.json content is identical whether
            // CWD == projectDir (multiprocess) or CWD == otherDir (multithreaded).
            var projectDir = Path.Combine(Path.GetTempPath(), "rtconfig-user-test-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "rtconfig-user-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // Use separate output files so both runs can write without conflict
                var config1RelativePath = Path.Combine("bin", "test1.runtimeconfig.json");
                var config2RelativePath = Path.Combine("bin", "test2.runtimeconfig.json");
                var config1Absolute = Path.Combine(projectDir, config1RelativePath);
                var config2Absolute = Path.Combine(projectDir, config2RelativePath);
                Directory.CreateDirectory(Path.Combine(projectDir, "bin"));

                // Create a user runtime config at a relative path under projectDir
                var userConfigRelativePath = "runtimeconfig.template.json";
                File.WriteAllText(
                    Path.Combine(projectDir, userConfigRelativePath),
                    "{\"configProperties\":{\"TestProperty\":true}}");

                // --- Multi-process mode: CWD == projectDir; TaskEnvironment.Fallback ---
                Directory.SetCurrentDirectory(projectDir);
                var (mpResult, mpEngine, mpException) = RunTaskCore(
                    projectDir, config1RelativePath, TaskEnvironment.Fallback, userRuntimeConfig: userConfigRelativePath);

                // --- Multi-threaded mode: CWD == otherDir; isolated TaskEnvironment ---
                Directory.SetCurrentDirectory(otherDir);
                var (mtResult, mtEngine, mtException) = RunTaskCore(
                    projectDir, config2RelativePath,
                    TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                    userRuntimeConfig: userConfigRelativePath);

                // Both should succeed
                mpResult.Should().Be(mtResult,
                    "task return value should be the same in both environments");
                mpException.Should().BeNull("multiprocess run should not throw");
                mtException.Should().BeNull("multithreaded run should not throw");
                mpEngine.Errors.Count.Should().Be(mtEngine.Errors.Count,
                    "error count should be the same in both environments");

                // Both output files should exist and have identical content
                File.Exists(config1Absolute).Should().BeTrue("multiprocess output should be created");
                File.Exists(config2Absolute).Should().BeTrue("multithreaded output should be created");
                var content1 = File.ReadAllText(config1Absolute);
                var content2 = File.ReadAllText(config2Absolute);
                content1.Should().Be(content2,
                    "runtimeconfig.json content should be identical in both environments");

                // Verify the user options were actually merged
                content1.Should().Contain("TestProperty",
                    "user runtime config properties should be merged into the output");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir))
                    Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir))
                    Directory.Delete(otherDir, true);
            }
        }

        [Fact]
        public void UserRuntimeConfigWithNonexistentFileProducesSameOutputInBothEnvironments()
        {
            // When UserRuntimeConfig points to a file that doesn't exist,
            // AddUserRuntimeOptions silently skips it. Verify both environments agree.
            var projectDir = Path.Combine(Path.GetTempPath(), "rtconfig-nouser-test-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "rtconfig-nouser-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                var config1RelativePath = Path.Combine("bin", "test1.runtimeconfig.json");
                var config2RelativePath = Path.Combine("bin", "test2.runtimeconfig.json");
                var config1Absolute = Path.Combine(projectDir, config1RelativePath);
                var config2Absolute = Path.Combine(projectDir, config2RelativePath);
                Directory.CreateDirectory(Path.Combine(projectDir, "bin"));

                // Point to a file that does NOT exist under projectDir
                var missingUserConfig = "does-not-exist.template.json";

                // --- Multi-process mode: CWD == projectDir; TaskEnvironment.Fallback ---
                Directory.SetCurrentDirectory(projectDir);
                var (mpResult, mpEngine, mpException) = RunTaskCore(
                    projectDir, config1RelativePath, TaskEnvironment.Fallback, userRuntimeConfig: missingUserConfig);

                // --- Multi-threaded mode: CWD == otherDir; isolated TaskEnvironment ---
                Directory.SetCurrentDirectory(otherDir);
                var (mtResult, mtEngine, mtException) = RunTaskCore(
                    projectDir, config2RelativePath,
                    TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                    userRuntimeConfig: missingUserConfig);

                mpResult.Should().Be(mtResult,
                    "task return value should be the same in both environments");
                mpEngine.Errors.Count.Should().Be(mtEngine.Errors.Count,
                    "error count should be the same in both environments");

                if (mpException != null)
                {
                    mtException.Should().NotBeNull("both environments should throw the same exception");
                    mtException!.GetType().Should().Be(mpException.GetType());
                }
                else
                {
                    mtException.Should().BeNull();
                }

                // Both output files should have identical content (without user properties)
                if (File.Exists(config1Absolute) && File.Exists(config2Absolute))
                {
                    File.ReadAllText(config1Absolute).Should().Be(
                        File.ReadAllText(config2Absolute),
                        "runtimeconfig.json content should be identical in both environments");
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir))
                    Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir))
                    Directory.Delete(otherDir, true);
            }
        }
    }
}
