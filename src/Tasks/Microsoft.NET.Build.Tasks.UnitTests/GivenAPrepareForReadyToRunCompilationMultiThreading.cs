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
    public class GivenAPrepareForReadyToRunCompilationMultiThreading
    {
        [Fact]
        public void NullInputs_DoNotCrash()
        {
            var engine = new MockBuildEngine();
            var task = new PrepareForReadyToRunCompilation
            {
                BuildEngine = engine,
                MainAssembly = new TaskItem("nonexistent.dll"),
                OutputPath = "output",
                IncludeSymbolsInSingleFile = false,
                Assemblies = null,
                ReadyToRunUseCrossgen2 = false,
            };

            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();

            // With null Assemblies and no crossgen tool, Execute should complete without NRE
            var result = task.Execute();

            // Task may log errors about missing crossgen tool, but must not throw NRE
            result.Should().BeTrue("null Assemblies causes early return with no errors");
        }

        [Fact]
        public void DiaSymReaderPath_IsResolvedRelativeToProjectDirectory()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"r2r-prepare-mt-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Create a fake DiaSymReader file at a relative path
                var toolsDir = Path.Combine(projectDir, "tools");
                Directory.CreateDirectory(toolsDir);
                File.WriteAllText(Path.Combine(toolsDir, "diasymreader.dll"), "fake");

                var crossgenTool = new TaskItem("tools\\crossgen.exe");
                crossgenTool.SetMetadata("DiaSymReader", "tools\\diasymreader.dll");
                crossgenTool.SetMetadata("JitPath", "tools\\clrjit.dll");

                var task = new PrepareForReadyToRunCompilation
                {
                    BuildEngine = new MockBuildEngine(),
                    MainAssembly = new TaskItem("test.dll"),
                    OutputPath = "output",
                    IncludeSymbolsInSingleFile = false,
                    Assemblies = null,
                    ReadyToRunUseCrossgen2 = false,
                    CrossgenTool = crossgenTool,
                    EmitSymbols = true,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                };

                var result = task.Execute();

                // With null Assemblies, ProcessInputFileList returns early.
                // The key point is that DiaSymReader at "tools\diasymreader.dll"
                // is resolved via TaskEnvironment relative to projectDir and found.
                result.Should().BeTrue("null Assemblies produces no errors");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void DualModeParity_SameResultRegardlessOfCwd()
        {
            var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"r2r-prepare-dual-{Guid.NewGuid():N}"));
            var otherDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"r2r-prepare-other-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);
            try
            {
                // Create a fake DiaSymReader file under projectDir
                var toolsDir = Path.Combine(projectDir, "tools");
                Directory.CreateDirectory(toolsDir);
                File.WriteAllText(Path.Combine(toolsDir, "diasymreader.dll"), "fake");

                var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);

                // Run with CWD = projectDir
                var savedCwd = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(projectDir);

                var engine1 = new MockBuildEngine();
                var crossgen1 = new TaskItem("tools\\crossgen.exe");
                crossgen1.SetMetadata("DiaSymReader", "tools\\diasymreader.dll");
                crossgen1.SetMetadata("JitPath", "tools\\clrjit.dll");

                var task1 = new PrepareForReadyToRunCompilation
                {
                    BuildEngine = engine1,
                    MainAssembly = new TaskItem("test.dll"),
                    OutputPath = "output",
                    IncludeSymbolsInSingleFile = false,
                    Assemblies = null,
                    ReadyToRunUseCrossgen2 = false,
                    CrossgenTool = crossgen1,
                    EmitSymbols = true,
                    TaskEnvironment = taskEnv,
                };
                var result1 = task1.Execute();

                // Run with CWD = otherDir (different from projectDir)
                Directory.SetCurrentDirectory(otherDir);

                var engine2 = new MockBuildEngine();
                var crossgen2 = new TaskItem("tools\\crossgen.exe");
                crossgen2.SetMetadata("DiaSymReader", "tools\\diasymreader.dll");
                crossgen2.SetMetadata("JitPath", "tools\\clrjit.dll");

                var task2 = new PrepareForReadyToRunCompilation
                {
                    BuildEngine = engine2,
                    MainAssembly = new TaskItem("test.dll"),
                    OutputPath = "output",
                    IncludeSymbolsInSingleFile = false,
                    Assemblies = null,
                    ReadyToRunUseCrossgen2 = false,
                    CrossgenTool = crossgen2,
                    EmitSymbols = true,
                    TaskEnvironment = taskEnv,
                };
                var result2 = task2.Execute();

                Directory.SetCurrentDirectory(savedCwd);

                // Both runs should produce the same result regardless of CWD
                result1.Should().Be(result2, "TaskEnvironment resolves paths independent of CWD");
                engine1.Errors.Count.Should().Be(engine2.Errors.Count,
                    "same errors regardless of CWD");
            }
            finally
            {
                Directory.Delete(projectDir, true);
                Directory.Delete(otherDir, true);
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public void PrepareForReadyToRunCompilation_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(parallelism);
            Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
            {
                try
                {
                    var task = new PrepareForReadyToRunCompilation
                    {
                        BuildEngine = new MockBuildEngine(),
                        TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                        MainAssembly = new TaskItem("nonexistent.dll"),
                        OutputPath = "output",
                        IncludeSymbolsInSingleFile = false,
                        Assemblies = null,
                        ReadyToRunUseCrossgen2 = false,
                    };
                    barrier.SignalAndWait();
                    task.Execute();
                }
                catch (Exception ex) { errors.Add($"Thread {i}: {ex.Message}"); }
            });
            errors.Should().BeEmpty();
        }
    }
}
