// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    // This file intentionally contains minimal multithreading-specific coverage.
    // ValidateExecutableReferences is a pure metadata-validation task with no
    // filesystem, environment-variable, or CWD dependencies, so the only
    // migration-specific behavior worth asserting here is:
    //   - The TaskEnvironment property is accessible (interface compliance).
    //   - The task can be executed concurrently without interference.
    // Behavioral correctness of the validation logic is covered by the
    // end-to-end tests in the SDK test suite.
    public class GivenAValidateExecutableReferencesMultiThreading
    {
        /// <summary>
        /// TaskEnvironment property must be settable and gettable.
        /// </summary>
        [Fact]
        public void TaskEnvironmentPropertyIsAccessible()
        {
            var task = new ValidateExecutableReferences();
            var env = TaskEnvironmentHelper.CreateForTest(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
            task.TaskEnvironment = env;
            Assert.Same(env, task.TaskEnvironment);
        }

        /// <summary>
        /// Concurrent execution: multiple task instances run in parallel without interference.
        /// </summary>
        [Fact]
        public void ConcurrentExecution_ProducesConsistentResults()
        {
            const int concurrency = 8;
            var results = new ConcurrentBag<(bool success, int errorCount)>();

            Parallel.For(0, concurrency, _ =>
            {
                var referencedProject = CreateReferencedProject(
                    referencedIsExecutable: true,
                    referencedIsSelfContained: false);

                var engine = new MockBuildEngine6();
                var task = new ValidateExecutableReferences
                {
                    BuildEngine = engine,
                    SelfContained = true,
                    IsExecutable = true,
                    ReferencedProjects = new[] { referencedProject },
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(
                        Path.Combine(Path.GetTempPath(), "concurrent-" + Guid.NewGuid().ToString("N"))),
                };

                bool success = task.Execute();
                results.Add((success, engine.Errors.Count));
            });

            Assert.Equal(concurrency, results.Count);
            // All should fail with exactly 1 error (self-contained referencing non-self-contained)
            foreach (var (success, errorCount) in results)
            {
                Assert.False(success);
                Assert.Equal(1, errorCount);
            }
        }

        /// <summary>
        /// When IsExecutable is false, the task should succeed without errors regardless of references.
        /// </summary>
        [Fact]
        public void NonExecutableProject_SkipsValidation()
        {
            var referencedProject = CreateReferencedProject(
                referencedIsExecutable: true,
                referencedIsSelfContained: false);

            var engine = new MockBuildEngine6();
            var task = new ValidateExecutableReferences
            {
                BuildEngine = engine,
                SelfContained = true,
                IsExecutable = false,
                ReferencedProjects = new[] { referencedProject },
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(
                    Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)),
            };

            Assert.True(task.Execute());
            Assert.Empty(engine.Errors);
        }

        private static ITaskItem CreateReferencedProject(
            bool referencedIsExecutable,
            bool referencedIsSelfContained,
            bool shouldBeValidated = true,
            bool selfContainedWasSpecified = false,
            string targetFramework = "net8.0",
            string targetFrameworkMoniker = ".NETCoreApp,Version=v8.0")
        {
            var additionalPropertiesXml = new XElement("AdditionalProperties",
                new XElement(targetFramework,
                    new XElement("ShouldBeValidatedAsExecutableReference", shouldBeValidated.ToString()),
                    new XElement("_IsExecutable", referencedIsExecutable.ToString()),
                    new XElement("SelfContained", referencedIsSelfContained.ToString()),
                    new XElement("_SelfContainedWasSpecified", selfContainedWasSpecified.ToString()),
                    new XElement("IsRidAgnostic", "true")
                ));

            var item = new TaskItem("ReferencedProject.csproj");
            item.SetMetadata("NearestTargetFramework", targetFramework);
            item.SetMetadata("AdditionalPropertiesFromProject", additionalPropertiesXml.ToString());
            item.SetMetadata("TargetFrameworks", targetFramework);
            item.SetMetadata("TargetFrameworkMonikers", targetFrameworkMoniker);

            return item;
        }

        /// <summary>
        /// Mock IBuildEngine6 that provides GetGlobalProperties support.
        /// </summary>
        internal class MockBuildEngine6 : MockBuildEngine, IBuildEngine6
        {
            private readonly Dictionary<string, string> _globalProperties = new(StringComparer.OrdinalIgnoreCase);

            public void LogTelemetry(string eventName, IDictionary<string, string> properties) { }

            public IReadOnlyDictionary<string, string> GetGlobalProperties()
            {
                return _globalProperties;
            }

            public void SetGlobalProperty(string key, string value)
            {
                _globalProperties[key] = value;
            }
        }
    }
}
