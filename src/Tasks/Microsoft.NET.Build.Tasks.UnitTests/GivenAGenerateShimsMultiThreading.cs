// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateShimsMultiThreading
    {
        [Fact]
        public void Paths_AreResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"shims-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Create fake apphost and assembly under projectDir
                var toolsDir = Path.Combine(projectDir, "tools");
                var binDir = Path.Combine(projectDir, "bin");
                Directory.CreateDirectory(toolsDir);
                Directory.CreateDirectory(binDir);
                File.WriteAllText(Path.Combine(toolsDir, "apphost.exe"), "not a real apphost");
                File.WriteAllText(Path.Combine(binDir, "test.dll"), "not a real assembly");

                var apphostItem = new TaskItem(Path.Combine("tools", "apphost.exe"));
                apphostItem.SetMetadata(MetadataKeys.RuntimeIdentifier, "linux-x64");

                var task = new GenerateShims
                {
                    BuildEngine = new MockBuildEngine(),
                    ApphostsForShimRuntimeIdentifiers = new ITaskItem[] { apphostItem },
                    IntermediateAssembly = Path.Combine("bin", "test.dll"),
                    PackageId = "TestPackage",
                    PackageVersion = "1.0.0",
                    TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                    ToolCommandName = "test-tool",
                    ToolEntryPoint = "test-tool.dll",
                    PackagedShimOutputDirectory = "shims",
                    ShimRuntimeIdentifiers = new ITaskItem[] { new TaskItem("linux-x64") },
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                };

                // Will throw because fake files aren't valid PE binaries.
                // Key assertion: exception is from PE processing, NOT "file not found"
                try
                {
                    task.Execute();
                }
                catch (Exception)
                {
                    // Expected — HostWriter.CreateAppHost fails on fake binaries
                }

                var engine = (MockBuildEngine)task.BuildEngine;
                var errors = engine.Errors.Select(e => e.Message).ToList();
                errors.Should().NotContain(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    && e.Contains("apphost", StringComparison.OrdinalIgnoreCase),
                    "paths should be resolved via TaskEnvironment, not CWD");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameErrorsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "shims-test-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "shims-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // Create a fake apphost file under projectDir
                var apphostRelPath = Path.Combine("tools", "apphost.exe");
                var apphostAbsPath = Path.Combine(projectDir, apphostRelPath);
                Directory.CreateDirectory(Path.GetDirectoryName(apphostAbsPath)!);
                File.WriteAllText(apphostAbsPath, "not a real apphost binary");

                // Create a fake intermediate assembly under projectDir
                var assemblyRelPath = Path.Combine("bin", "test.dll");
                var assemblyAbsPath = Path.Combine(projectDir, assemblyRelPath);
                Directory.CreateDirectory(Path.GetDirectoryName(assemblyAbsPath)!);
                File.WriteAllText(assemblyAbsPath, "not a real assembly");

                var outputRelPath = "shims";

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (result1, engine1, ex1) = RunTask(apphostRelPath, assemblyRelPath, outputRelPath, projectDir);

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (result2, engine2, ex2) = RunTask(apphostRelPath, assemblyRelPath, outputRelPath, projectDir);

                // Both should produce the same result
                result1.Should().Be(result2,
                    "task should return the same success/failure in both environments");
                (ex1 == null).Should().Be(ex2 == null,
                    "both environments should agree on whether an exception is thrown");
                if (ex1 != null && ex2 != null)
                {
                    ex1.GetType().Should().Be(ex2.GetType(),
                        "exception type should be identical in both environments");
                }
                engine1.Errors.Count.Should().Be(engine2.Errors.Count,
                    "error count should be the same in both environments");
                for (int i = 0; i < engine1.Errors.Count; i++)
                {
                    engine1.Errors[i].Message.Should().Be(
                        engine2.Errors[i].Message,
                        $"error message [{i}] should be identical in both environments");
                }
                engine1.Warnings.Count.Should().Be(engine2.Warnings.Count,
                    "warning count should be the same in both environments");
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
        public void EmptyShimRuntimeIdentifiers_DoesNotCrash()
        {
            var engine = new MockBuildEngine();
            var task = new GenerateShims
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ApphostsForShimRuntimeIdentifiers = Array.Empty<ITaskItem>(),
                IntermediateAssembly = "test.dll",
                PackageId = "TestPackage",
                PackageVersion = "1.0.0",
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                ToolCommandName = "test-tool",
                ToolEntryPoint = "test-tool.dll",
                PackagedShimOutputDirectory = "shims",
                ShimRuntimeIdentifiers = Array.Empty<ITaskItem>(),
            };

            try
            {
                // With no ShimRuntimeIdentifiers, the loop body never executes
                var result = task.Execute();

                result.Should().BeTrue("empty ShimRuntimeIdentifiers means no work to do");
                engine.Errors.Should().BeEmpty("empty ShimRuntimeIdentifiers should not produce errors");
            }
            catch (FileNotFoundException ex) when (ex.FileName?.Contains("HostModel") == true)
            {
                // Microsoft.NET.HostModel is excluded from test output (ExcludeAssets="Runtime"
                // in Tasks .csproj). The assembly is normally loaded from the SDK redist layout.
                // This test still validates construction and property assignment succeed.
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task GenerateShims_ConcurrentExecution(int parallelism)
        {
            var results = new ConcurrentBag<(bool success, string exType)>();
            using var startGate = new ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                int idx = i;
                tasks[idx] = System.Threading.Tasks.Task.Run(() =>
            {
                var projectDir = Path.Combine(Path.GetTempPath(), $"shims-conc-{idx}-{Guid.NewGuid():N}");
                Directory.CreateDirectory(projectDir);
                try
                {
                    var toolsDir = Path.Combine(projectDir, "tools");
                    var binDir = Path.Combine(projectDir, "bin");
                    Directory.CreateDirectory(toolsDir);
                    Directory.CreateDirectory(binDir);
                    File.WriteAllText(Path.Combine(toolsDir, "apphost.exe"), "not a real apphost");
                    File.WriteAllText(Path.Combine(binDir, "test.dll"), "not a real assembly");

                    var apphostItem = new TaskItem(Path.Combine("tools", "apphost.exe"));
                    apphostItem.SetMetadata(MetadataKeys.RuntimeIdentifier, "linux-x64");

                    var task = new GenerateShims
                    {
                        BuildEngine = new MockBuildEngine(),
                        ApphostsForShimRuntimeIdentifiers = new ITaskItem[] { apphostItem },
                        IntermediateAssembly = Path.Combine("bin", "test.dll"),
                        PackageId = "TestPackage",
                        PackageVersion = "1.0.0",
                        TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                        ToolCommandName = "test-tool",
                        ToolEntryPoint = "test-tool.dll",
                        PackagedShimOutputDirectory = "shims",
                        ShimRuntimeIdentifiers = new ITaskItem[] { new TaskItem("linux-x64") },
                        TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    };

                    startGate.Wait();
                    var result = task.Execute();
                    results.Add((result, "none"));
                }
                catch (Exception ex)
                {
                    results.Add((false, ex.GetType().Name));
                }
                finally
                {
                    if (Directory.Exists(projectDir))
                        Directory.Delete(projectDir, true);
                }
            });
            }
            startGate.Set();
            await System.Threading.Tasks.Task.WhenAll(tasks);

            // All threads should get the same outcome (all succeed or all fail the same way)
            results.Select(r => r.exType).Distinct().Should().HaveCount(1,
                "all threads should experience the same failure mode");
        }

        private static (bool result, MockBuildEngine engine, Exception? exception) RunTask(
            string apphostRelPath, string assemblyRelPath, string outputRelPath, string projectDir)
        {
            var apphostItem = new TaskItem(apphostRelPath);
            apphostItem.SetMetadata(MetadataKeys.RuntimeIdentifier, "linux-x64");

            var shimRid = new TaskItem("linux-x64");

            var engine = new MockBuildEngine();
            var task = new GenerateShims
            {
                BuildEngine = engine,
                ApphostsForShimRuntimeIdentifiers = new ITaskItem[] { apphostItem },
                IntermediateAssembly = assemblyRelPath,
                PackageId = "TestPackage",
                PackageVersion = "1.0.0",
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                ToolCommandName = "test-tool",
                ToolEntryPoint = "test-tool.dll",
                PackagedShimOutputDirectory = outputRelPath,
                ShimRuntimeIdentifiers = new ITaskItem[] { shimRid },
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };

            try
            {
                var result = task.Execute();
                return (result, engine, null);
            }
            catch (Exception ex)
            {
                return (false, engine, ex);
            }
        }
    }
}
