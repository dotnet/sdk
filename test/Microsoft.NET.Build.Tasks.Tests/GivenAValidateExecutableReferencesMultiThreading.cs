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
    // filesystem, environment-variable, or CWD dependencies, so the
    // migration-specific behavior worth asserting here is that MSBuild can
    // recognize the task as multithreadable and execute it concurrently without
    // interference.
    // Behavioral correctness of the validation logic is covered by the
    // end-to-end tests in the SDK test suite.
    public class GivenAValidateExecutableReferencesMultiThreading
    {
        private const string MSBuildMultiThreadableTaskAttributeFullName =
            "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute";

        private static readonly TimeSpan ConcurrencyTimeout = TimeSpan.FromSeconds(30);

        [Fact]
        public void TaskIsRecognizedAsMSBuildMultiThreadable()
        {
            Assert.Contains(
                typeof(ValidateExecutableReferences).GetCustomAttributes(inherit: false),
                attribute => attribute.GetType().FullName == MSBuildMultiThreadableTaskAttributeFullName);
        }

        /// <summary>
        /// Concurrent execution: multiple task instances run in parallel without interference.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task ConcurrentExecution_ProducesConsistentResults()
        {
            const int concurrency = 64;
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            var results = new ConcurrentBag<(bool success, int errorCount)>();
            using var allWorkersReady = new CountdownEvent(concurrency);
            using var start = new ManualResetEventSlim();
            using var executionOverlap = new Barrier(concurrency);

            var tasks = Enumerable.Range(0, concurrency)
                .Select(_ => System.Threading.Tasks.Task.Factory.StartNew(
                    () =>
                    {
                        var engine = new MockBuildEngine6(() =>
                        {
                            if (!executionOverlap.SignalAndWait(ConcurrencyTimeout, cancellationToken))
                            {
                                throw new TimeoutException("Timed out waiting for task executions to overlap.");
                            }
                        });
                        var task = new ValidateExecutableReferences
                        {
                            BuildEngine = engine,
                            SelfContained = true,
                            IsExecutable = true,
                            ReferencedProjects = new[]
                            {
                                CreateReferencedProject(
                                    referencedIsExecutable: true,
                                    referencedIsSelfContained: false)
                            },
                        };

                        allWorkersReady.Signal();
                        if (!start.Wait(ConcurrencyTimeout, cancellationToken))
                        {
                            throw new TimeoutException("Timed out waiting for all workers to start.");
                        }

                        bool success = task.Execute();
                        results.Add((success, engine.Errors.Count));
                    },
                    cancellationToken,
                    System.Threading.Tasks.TaskCreationOptions.LongRunning,
                    System.Threading.Tasks.TaskScheduler.Default))
                .ToArray();

            Assert.True(allWorkersReady.Wait(ConcurrencyTimeout, cancellationToken), "Timed out waiting for all workers to be ready.");
            start.Set();
            await System.Threading.Tasks.Task.WhenAll(tasks);

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
            private readonly Action? _onGetGlobalProperties;

            public MockBuildEngine6(Action? onGetGlobalProperties = null)
            {
                _onGetGlobalProperties = onGetGlobalProperties;
            }

            public void LogTelemetry(string eventName, IDictionary<string, string> properties) { }

            public IReadOnlyDictionary<string, string> GetGlobalProperties()
            {
                _onGetGlobalProperties?.Invoke();
                return _globalProperties;
            }

            public void SetGlobalProperty(string key, string value)
            {
                _globalProperties[key] = value;
            }
        }
    }
}
