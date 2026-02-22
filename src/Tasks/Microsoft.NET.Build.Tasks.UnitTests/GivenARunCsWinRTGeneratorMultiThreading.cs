// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenARunCsWinRTGeneratorMultiThreading
    {
        [Fact]
        public void NullInputs_DoNotCrashWithNullReferenceException()
        {
            // RunCsWinRTGenerator extends ToolTask — full Execute() tries to launch
            // an external process. We only verify that null/empty required properties
            // don't cause NullReferenceException.
            var engine = new MockBuildEngine();
            var task = new RunCsWinRTGenerator
            {
                BuildEngine = engine,
                ReferenceAssemblyPaths = null,
                OutputAssemblyPath = null,
                InteropAssemblyDirectory = null,
                CsWinRTToolsDirectory = null,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
            };

            // Execute will fail validation due to null required properties,
            // but should not throw NullReferenceException
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

            threw.Should().BeFalse("null properties should be handled gracefully, not throw NRE");
            if (!result)
            {
                // Validation failed as expected — check warnings/errors are about missing inputs
                var allMessages = engine.Warnings.Select(w => w.Message)
                    .Concat(engine.Errors.Select(e => e.Message))
                    .ToList();
                allMessages.Should().NotBeEmpty("validation should produce diagnostic messages");
            }
        }

        [Fact]
        public void TaskEnvironmentProperty_IsWirable()
        {
            var task = new RunCsWinRTGenerator();

            var teProp = task.GetType().GetProperty("TaskEnvironment");
            teProp.Should().NotBeNull("task must have a TaskEnvironment property after migration");

            var env = TaskEnvironmentHelper.CreateForTest();
            teProp!.SetValue(task, env);
            task.TaskEnvironment.Should().NotBeNull();
        }
    }
}
