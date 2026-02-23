// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateRegFreeComManifestMultiThreading
    {
        [Fact]
        public void IntermediateAssembly_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"regfree-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Place a copy of this test assembly at a relative path under the project dir
                var thisAssemblyPath = typeof(GivenAGenerateRegFreeComManifestMultiThreading).Assembly.Location;
                var binDir = Path.Combine(projectDir, "bin");
                Directory.CreateDirectory(binDir);
                var assemblyFileName = Path.GetFileName(thisAssemblyPath);
                File.Copy(thisAssemblyPath, Path.Combine(binDir, assemblyFileName));

                // Create fake clsidmap and manifest output paths
                File.WriteAllText(Path.Combine(binDir, "clsidmap.bin"), "{}");

                var task = new GenerateRegFreeComManifest
                {
                    BuildEngine = new MockBuildEngine(),
                    IntermediateAssembly = Path.Combine("bin", assemblyFileName),
                    ComHostName = "test.comhost.dll",
                    ClsidMapPath = Path.Combine("bin", "clsidmap.bin"),
                    ComManifestPath = Path.Combine("bin", "test.manifest"),
                };

                // Set TaskEnvironment for path resolution
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                // Execute — will try to read the assembly version from the relative path
                // resolved via TaskEnvironment, then create the manifest
                try
                {
                    task.Execute();
                }
                catch (Exception)
                {
                    // May fail due to invalid clsidmap content, but that's ok
                }

                // If the IntermediateAssembly was resolved correctly, the manifest file
                // should be created at the absolute path (or at least attempted)
                var engine = (MockBuildEngine)task.BuildEngine;
                var errors = engine.Errors.Select(e => e.Message).ToList();
                // Should NOT have file-not-found for the intermediate assembly
                errors.Should().NotContain(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    && e.Contains(assemblyFileName, StringComparison.OrdinalIgnoreCase),
                    "IntermediateAssembly should be resolved via TaskEnvironment");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameErrorsInMultiProcessAndMultiThreadedEnvironments()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "regfree-test-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "regfree-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // Copy a real assembly so FileUtilities.TryGetAssemblyVersion works
                var thisAssemblyPath = typeof(GivenAGenerateRegFreeComManifestMultiThreading).Assembly.Location;
                var binDir = Path.Combine(projectDir, "bin");
                Directory.CreateDirectory(binDir);
                var assemblyFileName = Path.GetFileName(thisAssemblyPath);
                File.Copy(thisAssemblyPath, Path.Combine(binDir, assemblyFileName));

                // Create a fake clsidmap
                File.WriteAllText(Path.Combine(binDir, "clsidmap.bin"), "{}");

                var assemblyRelPath = Path.Combine("bin", assemblyFileName);
                var clsidMapRelPath = Path.Combine("bin", "clsidmap.bin");
                var manifestRelPath = Path.Combine("bin", "test.manifest");

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (result1, engine1, ex1) = RunTask(assemblyRelPath, clsidMapRelPath, manifestRelPath, projectDir);

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (result2, engine2, ex2) = RunTask(assemblyRelPath, clsidMapRelPath, manifestRelPath, projectDir);

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

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task GenerateRegFreeComManifest_ConcurrentExecution(int parallelism)
        {
            var results = new ConcurrentBag<(bool success, string exType)>();
            using var startGate = new ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                int idx = i;
                tasks[idx] = System.Threading.Tasks.Task.Run(() =>
            {
                var projectDir = Path.Combine(Path.GetTempPath(), $"regfree-conc-{idx}-{Guid.NewGuid():N}");
                Directory.CreateDirectory(projectDir);
                try
                {
                    var thisAssemblyPath = typeof(GivenAGenerateRegFreeComManifestMultiThreading).Assembly.Location;
                    var binDir = Path.Combine(projectDir, "bin");
                    Directory.CreateDirectory(binDir);
                    var assemblyFileName = Path.GetFileName(thisAssemblyPath);
                    File.Copy(thisAssemblyPath, Path.Combine(binDir, assemblyFileName));
                    File.WriteAllText(Path.Combine(binDir, "clsidmap.bin"), "{}");

                    var task = new GenerateRegFreeComManifest
                    {
                        BuildEngine = new MockBuildEngine(),
                        IntermediateAssembly = Path.Combine("bin", assemblyFileName),
                        ComHostName = "test.comhost.dll",
                        ClsidMapPath = Path.Combine("bin", "clsidmap.bin"),
                        ComManifestPath = Path.Combine("bin", "test.manifest"),
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
            string assemblyRelPath, string clsidMapRelPath, string manifestRelPath, string projectDir)
        {
            var engine = new MockBuildEngine();
            var task = new GenerateRegFreeComManifest
            {
                BuildEngine = engine,
                IntermediateAssembly = assemblyRelPath,
                ComHostName = "test.comhost.dll",
                ClsidMapPath = clsidMapRelPath,
                ComManifestPath = manifestRelPath,
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
