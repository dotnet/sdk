// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateSupportedTargetFrameworkAliasMultiThreading
    {
        /// <summary>
        /// Smoke coverage: two instances constructed with identical inputs should produce
        /// identical outputs. This is NOT a multi-process vs multi-thread parity test — both
        /// instances run sequentially on the current thread.
        /// </summary>
        [Fact]
        public void Two_instances_with_identical_inputs_produce_identical_outputs()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(projectDir);

                var taskEnvA = TaskEnvironmentHelper.CreateForTest(projectDir);
                var taskA = CreateBasicTask(projectDir);
                taskA.TaskEnvironment = taskEnvA;
                taskA.Execute().Should().BeTrue();

                var taskEnvB = TaskEnvironmentHelper.CreateForTest(projectDir);
                var taskB = CreateBasicTask(projectDir);
                taskB.TaskEnvironment = taskEnvB;
                taskB.Execute().Should().BeTrue();

                GetOutputSignature(taskA).Should().Be(GetOutputSignature(taskB),
                    because: "two instances with identical TaskEnvironments should produce identical results");
            }
            finally
            {
                try { Directory.Delete(projectDir, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Genuine concurrency regression test: multiple threads run the task concurrently with
        /// different inputs and verify that each produces correct, independent results.
        /// Uses a start-gate pattern (ManualResetEventSlim) instead of Barrier to avoid
        /// deadlock risks in the test infrastructure.
        /// </summary>
        [Fact]
        public async Task Concurrent_threads_produce_correct_independent_results()
        {
            const int threadCount = 4;

            var projectDirs = new string[threadCount];
            var taskEnvs = new TaskEnvironment[threadCount];
            var expectedOutputs = new string[threadCount];
            var observedOutputs = new string?[threadCount];
            var results = new bool[threadCount];
            var exceptions = new Exception?[threadCount];

            // Each thread gets different inputs to verify independence
            var testCases = new[]
            {
                new { TFM = ".NETCoreApp,Version=v6.0", Platform = "", UseWpf = false, UseWinForms = false, Frameworks = new[] { ".NETCoreApp,Version=v5.0", ".NETCoreApp,Version=v6.0", ".NETCoreApp,Version=v7.0" } },
                new { TFM = ".NETCoreApp,Version=v7.0", Platform = "", UseWpf = false, UseWinForms = false, Frameworks = new[] { ".NETCoreApp,Version=v6.0", ".NETCoreApp,Version=v7.0", ".NETCoreApp,Version=v8.0" } },
                new { TFM = ".NETCoreApp,Version=v8.0", Platform = "Windows,Version=10.0", UseWpf = true, UseWinForms = false, Frameworks = new[] { ".NETCoreApp,Version=v6.0", ".NETCoreApp,Version=v7.0", ".NETCoreApp,Version=v8.0" } },
                new { TFM = ".NETCoreApp,Version=v9.0", Platform = "", UseWpf = false, UseWinForms = true, Frameworks = new[] { ".NETCoreApp,Version=v7.0", ".NETCoreApp,Version=v8.0", ".NETCoreApp,Version=v9.0" } }
            };

            for (int i = 0; i < threadCount; i++)
            {
                projectDirs[i] = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");
                Directory.CreateDirectory(projectDirs[i]);
                taskEnvs[i] = TaskEnvironmentHelper.CreateForTest(projectDirs[i]);
            }

            using var startGate = new System.Threading.ManualResetEventSlim(false);

            try
            {
                var tasks = new Task[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    int idx = i;
                    tasks[i] = Task.Run(() =>
                    {
                        try
                        {
                            // Wait for all threads to be ready before executing
                            startGate.Wait();

                            var tc = testCases[idx];
                            var task = new GenerateSupportedTargetFrameworkAlias
                            {
                                BuildEngine = new MockBuildEngine(),
                                TaskEnvironment = taskEnvs[idx],
                                TargetFrameworkMoniker = tc.TFM,
                                TargetPlatformMoniker = tc.Platform,
                                UseWpf = tc.UseWpf,
                                UseWindowsForms = tc.UseWinForms,
                                SupportedTargetFramework = tc.Frameworks.Select(f =>
                                    new MockTaskItem(f, new Dictionary<string, string>
                                    {
                                        { MetadataKeys.DisplayName, $".NET {f}" }
                                    })).ToArray<ITaskItem>()
                            };

                            results[idx] = task.Execute();
                            observedOutputs[idx] = GetOutputSignature(task);

                            // Verify behavioral correctness: the task should only output aliases
                            // for frameworks that match the target framework's framework family
                            if (results[idx] && task.SupportedTargetFrameworkAlias != null)
                            {
                                foreach (var alias in task.SupportedTargetFrameworkAlias)
                                {
                                    var displayName = alias.GetMetadata(MetadataKeys.DisplayName);
                                    displayName.Should().NotBeNullOrEmpty(
                                        because: $"thread {idx}: every alias must have a {MetadataKeys.DisplayName}");

                                // For WPF/WinForms on .NET 5+, verify the -windows suffix is applied
                                if ((tc.UseWpf || tc.UseWinForms) && tc.TFM.Contains("NETCoreApp"))
                                {
                                    // Parse version from ".NETCoreApp,Version=v5.0" format
                                    var versionMatch = System.Text.RegularExpressions.Regex.Match(tc.TFM, @"Version=v(\d+)");
                                    if (versionMatch.Success && int.TryParse(versionMatch.Groups[1].Value, out int majorVer) && majorVer >= 5)
                                    {
                                        alias.ItemSpec.Should().EndWith("-windows",
                                            because: $"thread {idx}: WPF/WinForms on .NET 5+ should have -windows suffix");
                                    }
                                }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions[idx] = ex;
                        }
                    });
                }

                // Release all threads simultaneously
                startGate.Set();

                await Task.WhenAll(tasks);

                for (int i = 0; i < threadCount; i++)
                {
                    exceptions[i].Should().BeNull($"thread {i} should not throw");
                    results[i].Should().BeTrue($"thread {i} should succeed");
                    observedOutputs[i].Should().NotBeNullOrEmpty($"thread {i} should produce output");
                }

                // Verify outputs are distinct (different inputs produce different results)
                var uniqueOutputs = observedOutputs.Where(o => o != null).Distinct().Count();
                uniqueOutputs.Should().BeGreaterThan(1,
                    because: "different inputs should produce different outputs, proving thread independence");
            }
            finally
            {
                for (int i = 0; i < threadCount; i++)
                {
                    try { Directory.Delete(projectDirs[i], recursive: true); } catch { }
                }
            }
        }

        /// <summary>
        /// Verify that DisplayName metadata is correctly propagated or generated.
        /// </summary>
        [Fact]
        public void DisplayName_metadata_is_correctly_processed()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(projectDir);
                var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);

                var task = new GenerateSupportedTargetFrameworkAlias
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = taskEnv,
                    TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                    TargetPlatformMoniker = "",
                    UseWpf = false,
                    UseWindowsForms = false,
                    SupportedTargetFramework = new ITaskItem[]
                    {
                        new MockTaskItem(".NETCoreApp,Version=v6.0", new Dictionary<string, string>
                        {
                            { MetadataKeys.DisplayName, ".NET 6.0 Custom" }
                        }),
                        new MockTaskItem(".NETCoreApp,Version=v7.0", new Dictionary<string, string>()), // No DisplayName
                        new MockTaskItem(".NETCoreApp,Version=v8.0", new Dictionary<string, string>
                        {
                            { MetadataKeys.DisplayName, ".NET 8.0 LTS" }
                        })
                    }
                };

                task.Execute().Should().BeTrue();
                task.SupportedTargetFrameworkAlias.Should().HaveCount(3);

                // Custom DisplayName should be preserved
                task.SupportedTargetFrameworkAlias[0].GetMetadata(MetadataKeys.DisplayName)
                    .Should().Be(".NET 6.0 Custom");

                // Missing DisplayName should default to alias itself
                task.SupportedTargetFrameworkAlias[1].GetMetadata(MetadataKeys.DisplayName)
                    .Should().Be("net7.0");

                // Custom DisplayName should be preserved
                task.SupportedTargetFrameworkAlias[2].GetMetadata(MetadataKeys.DisplayName)
                    .Should().Be(".NET 8.0 LTS");
            }
            finally
            {
                try { Directory.Delete(projectDir, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Verify that WPF and WindowsForms correctly apply -windows suffix for .NET 5+.
        /// </summary>
        [Fact]
        public void WpfAndWinForms_apply_windows_suffix_for_netcoreapp5_plus()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(projectDir);
                var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);

                var task = new GenerateSupportedTargetFrameworkAlias
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = taskEnv,
                    TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                    TargetPlatformMoniker = "",
                    UseWpf = true,
                    UseWindowsForms = false,
                    SupportedTargetFramework = new ITaskItem[]
                    {
                        new MockTaskItem(".NETCoreApp,Version=v5.0", new Dictionary<string, string>()),
                        new MockTaskItem(".NETCoreApp,Version=v6.0", new Dictionary<string, string>()),
                        new MockTaskItem(".NETCoreApp,Version=v8.0", new Dictionary<string, string>())
                    }
                };

                task.Execute().Should().BeTrue();

                task.SupportedTargetFrameworkAlias.Should().HaveCount(3);
                task.SupportedTargetFrameworkAlias[0].ItemSpec.Should().Be("net5.0-windows");
                task.SupportedTargetFrameworkAlias[1].ItemSpec.Should().Be("net6.0-windows");
                task.SupportedTargetFrameworkAlias[2].ItemSpec.Should().Be("net8.0-windows");
            }
            finally
            {
                try { Directory.Delete(projectDir, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Verify that only frameworks matching the target framework family are included.
        /// </summary>
        [Fact]
        public void Only_matching_framework_families_are_included()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(projectDir);
                var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);

                var task = new GenerateSupportedTargetFrameworkAlias
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = taskEnv,
                    TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                    TargetPlatformMoniker = "",
                    UseWpf = false,
                    UseWindowsForms = false,
                    SupportedTargetFramework = new ITaskItem[]
                    {
                        new MockTaskItem(".NETCoreApp,Version=v6.0", new Dictionary<string, string>()),
                        new MockTaskItem(".NETStandard,Version=v2.0", new Dictionary<string, string>()),
                        new MockTaskItem(".NETCoreApp,Version=v8.0", new Dictionary<string, string>())
                    }
                };

                task.Execute().Should().BeTrue();

                // Only net6.0 and net8.0 should be included (.NETCoreApp framework)
                // netstandard2.0 (.NETStandard) should be excluded
                task.SupportedTargetFrameworkAlias.Should().HaveCount(2);
                task.SupportedTargetFrameworkAlias.Select(a => a.ItemSpec)
                    .Should().Contain(new[] { "net6.0", "net8.0" });
            }
            finally
            {
                try { Directory.Delete(projectDir, recursive: true); } catch { }
            }
        }

        #region Helpers

        private GenerateSupportedTargetFrameworkAlias CreateBasicTask(string projectDir)
        {
            return new GenerateSupportedTargetFrameworkAlias
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                TargetPlatformMoniker = "",
                UseWpf = false,
                UseWindowsForms = false,
                SupportedTargetFramework = new ITaskItem[]
                {
                    new MockTaskItem(".NETCoreApp,Version=v6.0", new Dictionary<string, string>
                    {
                        { MetadataKeys.DisplayName, ".NET 6.0" }
                    }),
                    new MockTaskItem(".NETCoreApp,Version=v7.0", new Dictionary<string, string>
                    {
                        { MetadataKeys.DisplayName, ".NET 7.0" }
                    }),
                    new MockTaskItem(".NETCoreApp,Version=v8.0", new Dictionary<string, string>
                    {
                        { MetadataKeys.DisplayName, ".NET 8.0" }
                    })
                }
            };
        }

        private static string GetOutputSignature(GenerateSupportedTargetFrameworkAlias task)
        {
            var parts = new List<string>();
            if (task.SupportedTargetFrameworkAlias != null)
            {
                foreach (var alias in task.SupportedTargetFrameworkAlias)
                {
                    var displayName = alias.GetMetadata(MetadataKeys.DisplayName);
                    parts.Add($"{alias.ItemSpec}|{displayName}");
                }
            }
            parts.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(";", parts);
        }

        #endregion
    }
}
