// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateShimsMultiThreading
    {
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
                ApphostsForShimRuntimeIdentifiers = Array.Empty<ITaskItem>(),
                IntermediateAssembly = "test.dll",
                PackageId = "TestPackage",
                PackageVersion = "1.0.0",
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                ToolCommandName = "test-tool",
                ToolEntryPoint = "test-tool.dll",
                PackagedShimOutputDirectory = "",
                ShimRuntimeIdentifiers = Array.Empty<ITaskItem>(),
            };

            // With no ShimRuntimeIdentifiers, the loop body never executes
            var result = task.Execute();

            result.Should().BeTrue("empty ShimRuntimeIdentifiers means no work to do");
            engine.Errors.Should().BeEmpty("empty ShimRuntimeIdentifiers should not produce errors");
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
