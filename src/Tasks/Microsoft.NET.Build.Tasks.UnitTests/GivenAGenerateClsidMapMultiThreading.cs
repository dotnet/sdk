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
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new GenerateClsidMap();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(GenerateClsidMap).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItResolvesAssemblyPathViaTaskEnvironment()
        {
            // Create a temp directory to act as a fake project dir (different from CWD).
            // Place a file at a relative path under the project dir.
            // If the task resolves via TaskEnvironment (correct), it finds the file.
            // If it resolves via CWD (incorrect), it gets FileNotFoundException.
            var projectDir = Path.Combine(Path.GetTempPath(), "clsidmap-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var assemblyRelativePath = Path.Combine("output", "test.dll");
                var assemblyAbsolutePath = Path.Combine(projectDir, assemblyRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(assemblyAbsolutePath)!);
                // Write a non-PE file so the task will catch BadImageFormatException
                File.WriteAllText(assemblyAbsolutePath, "not a real assembly");

                var clsidMapRelativePath = Path.Combine("output", "clsid.map");

                var task = new GenerateClsidMap
                {
                    IntermediateAssembly = assemblyRelativePath,
                    ClsidMapDestinationPath = clsidMapRelativePath,
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;

                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property (from IMultiThreadableTask)");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

                // Execute — the task should open the file (proving path resolution works).
                // BadImageFormatException will be caught internally and logged as an error.
                // If the path wasn't resolved via TaskEnvironment, we'd get a
                // FileNotFoundException thrown instead (since the file only exists under projectDir).
                task.Execute();

                // The task should have logged an error about the invalid assembly,
                // NOT thrown a FileNotFoundException, proving path resolution worked.
                mockEngine.Errors.Should().HaveCount(1);
                mockEngine.Errors[0].Message.Should().Contain(assemblyRelativePath);
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
