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
    public class GivenARunReadyToRunCompilerMultiThreading
    {
        [Fact]
        public void NullInputs_DoNotCrashWithNullReferenceException()
        {
            // RunReadyToRunCompiler extends ToolTask — full Execute() launches crossgen2.
            // We only verify that null/empty properties don't cause NullReferenceException.
            var engine = new MockBuildEngine();

            var compilationEntry = new TaskItem("test.dll");
            // Don't set any metadata — tests graceful handling of missing metadata

            var task = new RunReadyToRunCompiler
            {
                BuildEngine = engine,
                CompilationEntry = compilationEntry,
                ImplementationAssemblyReferences = Array.Empty<ITaskItem>(),
                CrossgenTool = null,
                Crossgen2Tool = null,
                UseCrossgen2 = false,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
            };

            // Execute should fail validation (missing crossgen tool) but not throw NRE
            bool threw = false;
            bool result = false;
            try
            {
                result = task.Execute();
            }
            catch (NullReferenceException)
            {
                threw = true;
            }

            threw.Should().BeFalse("null CrossgenTool should be handled gracefully, not throw NRE");
            result.Should().BeFalse("validation should fail without crossgen tool");
            engine.Errors.Should().NotBeEmpty("should log error about missing crossgen tool");
        }

        [Fact]
        public void TaskEnvironmentProperty_IsWirable()
        {
            var task = new RunReadyToRunCompiler();

            var teProp = task.GetType().GetProperty("TaskEnvironment");
            teProp.Should().NotBeNull("task must have a TaskEnvironment property after migration");

            var env = TaskEnvironmentHelper.CreateForTest();
            teProp!.SetValue(task, env);
            task.TaskEnvironment.Should().NotBeNull();
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public void RunReadyToRunCompiler_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(parallelism);
            Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
            {
                try
                {
                    var task = new RunReadyToRunCompiler
                    {
                        BuildEngine = new MockBuildEngine(),
                        TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                        CompilationEntry = new TaskItem("test.dll"),
                        ImplementationAssemblyReferences = Array.Empty<ITaskItem>(),
                        CrossgenTool = null,
                        Crossgen2Tool = null,
                        UseCrossgen2 = false,
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
