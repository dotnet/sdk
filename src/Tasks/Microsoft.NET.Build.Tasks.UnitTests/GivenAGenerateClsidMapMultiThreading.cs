// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateClsidMapMultiThreading
    {
        [Fact]
        public void ItProducesSameErrorsInMultiProcessAndMultiThreadedEnvironments()
        {
            // In multiprocess mode, CWD == projectDir (as MSBuild traditionally works).
            // In multithreaded mode, CWD == otherDir but TaskEnvironment still points to projectDir.
            // Both should produce identical errors for an invalid (non-PE) assembly.
            var projectDir = Path.Combine(Path.GetTempPath(), "clsidmap-test-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "clsidmap-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // Place a non-PE file at a relative path under projectDir
                var assemblyRelativePath = Path.Combine("output", "test.dll");
                var assemblyAbsolutePath = Path.Combine(projectDir, assemblyRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(assemblyAbsolutePath)!);
                File.WriteAllText(assemblyAbsolutePath, "not a real assembly");

                var clsidMapRelativePath = Path.Combine("output", "clsid.map");

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (multiProcessResult, multiProcessEngine) = RunTask(assemblyRelativePath, clsidMapRelativePath, projectDir);

                // --- Multithreaded mode: CWD == otherDir (different from projectDir) ---
                Directory.SetCurrentDirectory(otherDir);
                var (multiThreadedResult, multiThreadedEngine) = RunTask(assemblyRelativePath, clsidMapRelativePath, projectDir);

                // Both should produce the same result
                multiProcessResult.Should().Be(multiThreadedResult,
                    "task should return the same success/failure in both environments");
                multiProcessEngine.Errors.Count.Should().Be(multiThreadedEngine.Errors.Count,
                    "error count should be the same in both environments");
                for (int i = 0; i < multiProcessEngine.Errors.Count; i++)
                {
                    multiProcessEngine.Errors[i].Message.Should().Be(
                        multiThreadedEngine.Errors[i].Message,
                        $"error message [{i}] should be identical in both environments");
                }
                multiProcessEngine.Warnings.Count.Should().Be(multiThreadedEngine.Warnings.Count,
                    "warning count should be the same in both environments");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir))
                    Directory.Delete(otherDir, true);
            }
        }

        [Fact]
        public void ItProducesSameClsidMapInMultiProcessAndMultiThreadedEnvironments()
        {
            // Same pattern but with a valid .NET assembly to test the success path.
            // Both modes should produce identical CLSID map file contents.
            var projectDir = Path.Combine(Path.GetTempPath(), "clsidmap-test-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "clsidmap-decoy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // Copy a real .NET assembly into projectDir
                var sourceAssembly = typeof(GenerateClsidMap).Assembly.Location;
                var assemblyRelativePath = Path.Combine("output", "test.dll");
                var assemblyAbsolutePath = Path.Combine(projectDir, assemblyRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(assemblyAbsolutePath)!);
                File.Copy(sourceAssembly, assemblyAbsolutePath);

                // Each mode writes to a separate clsid map so we can compare contents
                var clsidMap1Relative = Path.Combine("output", "clsid1.map");
                var clsidMap2Relative = Path.Combine("output", "clsid2.map");
                var clsidMap1Absolute = Path.Combine(projectDir, clsidMap1Relative);
                var clsidMap2Absolute = Path.Combine(projectDir, clsidMap2Relative);

                // --- Multiprocess mode: CWD == projectDir ---
                Directory.SetCurrentDirectory(projectDir);
                var (result1, engine1) = RunTask(assemblyRelativePath, clsidMap1Relative, projectDir);

                // --- Multithreaded mode: CWD == otherDir ---
                Directory.SetCurrentDirectory(otherDir);
                var (result2, engine2) = RunTask(assemblyRelativePath, clsidMap2Relative, projectDir);

                // Same result, same errors/warnings
                result1.Should().Be(result2, "task result should match between environments");
                engine1.Errors.Count.Should().Be(engine2.Errors.Count, "error count should match");
                engine1.Warnings.Count.Should().Be(engine2.Warnings.Count, "warning count should match");

                // Both map files should be created (or both not)
                File.Exists(clsidMap1Absolute).Should().Be(File.Exists(clsidMap2Absolute),
                    "both environments should agree on whether the clsid map file is created");

                if (File.Exists(clsidMap1Absolute))
                {
                    File.ReadAllText(clsidMap1Absolute).Should().Be(
                        File.ReadAllText(clsidMap2Absolute),
                        "clsid map file contents should be identical in both environments");
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir))
                    Directory.Delete(otherDir, true);
            }
        }

        private static (bool result, MockBuildEngine engine) RunTask(
            string assemblyRelativePath, string clsidMapRelativePath, string projectDir)
        {
            var task = new GenerateClsidMap
            {
                IntermediateAssembly = assemblyRelativePath,
                ClsidMapDestinationPath = clsidMapRelativePath,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;

            var result = task.Execute();
            return (result, engine);
        }
    }
}
