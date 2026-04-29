// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [Collection("ProcessFrameworkReferences CWD")]
    public class GivenAProcessFrameworkReferencesMultiThreading : IDisposable
    {
        private readonly string _originalCwd;
        private readonly string _decoyDir;

        public GivenAProcessFrameworkReferencesMultiThreading()
        {
            _originalCwd = Directory.GetCurrentDirectory();
            _decoyDir = Path.Combine(Path.GetTempPath(), $"decoy_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_decoyDir);
            Directory.SetCurrentDirectory(_decoyDir);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalCwd);
            try { Directory.Delete(_decoyDir, recursive: true); } catch { }
        }

        [Fact]
        public void ProcessFrameworkReferences_declares_MSBuild_multithreadable_contract()
        {
            var taskType = typeof(ProcessFrameworkReferences);

            taskType.GetCustomAttributes(inherit: false)
                .Select(a => a.GetType().FullName)
                .Should().ContainSingle(
                    fullName => fullName == "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute",
                    because: "MSBuild recognizes the marker attribute by namespace and type name");

            taskType.GetInterfaces()
                .Select(i => i.FullName)
                .Should().ContainSingle(
                    fullName => fullName == "Microsoft.Build.Framework.IMultiThreadableTask",
                    because: "MSBuild recognizes the multithreadable task interface by namespace and type name");
        }

        [Fact]
        public void Workload_resolution_uses_TaskEnvironment_ProjectDirectory_for_global_json()
        {
            const string sdkVersion = "8.0.200";
            const string manifestId = "test";
            const string projectWorkloadSetVersion = "8.0.200";
            const string cwdWorkloadSetVersion = "8.0.201";
            const string projectPackVersion = "9.9.2";
            const string cwdPackVersion = "9.9.1";
            const string packName = "Microsoft.NETCore.App.Ref";

            var projectDir = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");
            var dotnetRoot = Path.Combine(Path.GetTempPath(), $"dotnet_{Guid.NewGuid():N}");
            var manifestRoot = Path.Combine(dotnetRoot, "sdk-manifests");
            var runtimeGraphPath = CreateRuntimeGraphFile(MinimalRuntimeGraph);

            Directory.CreateDirectory(projectDir);

            try
            {
                CreateGlobalJson(projectDir, projectWorkloadSetVersion);
                CreateGlobalJson(_decoyDir, cwdWorkloadSetVersion);

                CreateMockManifest(manifestRoot, sdkVersion, manifestId, "11.0.1", packName, cwdPackVersion);
                CreateMockManifest(manifestRoot, sdkVersion, manifestId, "11.0.2", packName, projectPackVersion);
                CreateMockWorkloadSet(manifestRoot, sdkVersion, cwdWorkloadSetVersion, manifestId, "11.0.1");
                CreateMockWorkloadSet(manifestRoot, sdkVersion, projectWorkloadSetVersion, manifestId, "11.0.2");

                var task = new ProcessFrameworkReferences
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = ToolsetInfo.CurrentTargetFrameworkVersion,
                    NETCoreSdkRuntimeIdentifier = "win-x64",
                    NetCoreRoot = dotnetRoot,
                    NETCoreSdkVersion = sdkVersion,
                    RuntimeGraphPath = runtimeGraphPath,
                    EnableTargetingPackDownload = true,
                    FrameworkReferences = new ITaskItem[]
                    {
                        new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                    },
                    KnownFrameworkReferences = new ITaskItem[]
                    {
                        CreateKnownFrameworkReference("Microsoft.NETCore.App",
                            ToolsetInfo.CurrentTargetFramework, "**FromWorkload**")
                    },
                };

                task.Execute().Should().BeTrue();

                task.TargetingPacks.Should().NotBeNullOrEmpty();
                task.TargetingPacks![0].GetMetadata(MetadataKeys.NuGetPackageVersion)
                    .Should().Be(projectPackVersion,
                        because: "global.json should be searched from TaskEnvironment.ProjectDirectory, not the process CWD");
            }
            finally
            {
                try { Directory.Delete(projectDir, recursive: true); } catch { }
                try { Directory.Delete(dotnetRoot, recursive: true); } catch { }
                TryDeleteFile(runtimeGraphPath);
            }
        }

        /// <summary>
        /// GetPackPath should use TaskEnvironment.GetEnvironmentVariable for WORKLOAD_PACK_ROOTS
        /// rather than the process-level Environment.GetEnvironmentVariable.
        /// </summary>
        [Fact]
        public void GetPackPath_uses_TaskEnvironment_for_workload_pack_roots_env_var()
        {
            // Arrange: create a pack root via the TaskEnvironment's env vars
            var projectDir = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");
            Directory.CreateDirectory(projectDir);
            var workloadPackRoot = Path.Combine(Path.GetTempPath(), $"wpr_{Guid.NewGuid():N}");
            var packName = "Microsoft.NETCore.App.Ref";
            var packVersion = "9.0.0";
            var packPath = Path.Combine(workloadPackRoot, "packs", packName, packVersion);
            Directory.CreateDirectory(packPath);

            try
            {
                var runtimeGraphPath = CreateRuntimeGraphFile(MinimalRuntimeGraph);
                try
                {
                    var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
                    taskEnv.SetEnvironmentVariable("DOTNETSDK_WORKLOAD_PACK_ROOTS", workloadPackRoot);

                    var task = new ProcessFrameworkReferences
                    {
                        BuildEngine = new MockBuildEngine(),
                        TaskEnvironment = taskEnv,
                        TargetFrameworkIdentifier = ".NETCoreApp",
                        TargetFrameworkVersion = ToolsetInfo.CurrentTargetFrameworkVersion,
                        NETCoreSdkRuntimeIdentifier = "win-x64",
                        NetCoreRoot = projectDir,
                        NETCoreSdkVersion = "9.0.100",
                        RuntimeGraphPath = runtimeGraphPath,
                        EnableTargetingPackDownload = true,
                        FrameworkReferences = new ITaskItem[]
                        {
                            new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                        },
                        KnownFrameworkReferences = new ITaskItem[]
                        {
                            CreateKnownFrameworkReference("Microsoft.NETCore.App",
                                ToolsetInfo.CurrentTargetFramework, packVersion)
                        },
                    };

                    task.Execute().Should().BeTrue(because: "the task should succeed");

                    task.TargetingPacks.Should().NotBeNullOrEmpty();
                    task.TargetingPacks![0].GetMetadata(MetadataKeys.PackageDirectory)
                        .Should().Be(packPath,
                            because: "the pack should be resolved from the TaskEnvironment's WORKLOAD_PACK_ROOTS, not the process env");
                }
                finally
                {
                    TryDeleteFile(runtimeGraphPath);
                }
            }
            finally
            {
                try { Directory.Delete(projectDir, recursive: true); } catch { }
                try { Directory.Delete(workloadPackRoot, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Smoke coverage: two instances constructed with identical inputs should produce
        /// identical outputs. This is NOT a multi-process vs multi-thread parity test ΓÇö both
        /// instances run sequentially on the current thread. The genuine concurrency coverage
        /// lives in <see cref="Concurrent_threads_observe_their_own_TaskEnvironment_not_process_env"/>.
        /// </summary>
        [Fact]
        public void Two_instances_with_identical_inputs_produce_identical_outputs()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");
            Directory.CreateDirectory(projectDir);
            var runtimeGraphPath = CreateRuntimeGraphFile(MinimalRuntimeGraph);

            try
            {
                // Run first instance with its own TaskEnvironment
                var taskEnvA = TaskEnvironmentHelper.CreateForTest(projectDir);
                var taskA = CreateBasicTask(runtimeGraphPath, targetingPackRoot: null, projectDir: projectDir);
                taskA.TaskEnvironment = taskEnvA;
                taskA.Execute().Should().BeTrue();

                // Run second instance with a separate TaskEnvironment
                var taskEnvB = TaskEnvironmentHelper.CreateForTest(projectDir);
                var taskB = CreateBasicTask(runtimeGraphPath, targetingPackRoot: null, projectDir: projectDir);
                taskB.TaskEnvironment = taskEnvB;
                taskB.Execute().Should().BeTrue();

                // Outputs should match
                GetOutputSignature(taskA).Should().Be(GetOutputSignature(taskB),
                    because: "two instances with identical TaskEnvironments should produce identical results");
            }
            finally
            {
                try { Directory.Delete(projectDir, recursive: true); } catch { }
                TryDeleteFile(runtimeGraphPath);
            }
        }

        /// <summary>
        /// Genuine concurrency regression test: each thread owns a TaskEnvironment with its
        /// own DOTNETSDK_WORKLOAD_PACK_ROOTS pointing at a thread-specific pack folder. While
        /// the threads are running, one of them also sets a PROCESS-level
        /// DOTNETSDK_WORKLOAD_PACK_ROOTS that points at a decoy directory with no pack.
        /// If GetPackPath were still reading the process env var (pre-migration behavior),
        /// at least one thread would resolve the wrong (or no) pack. After the migration,
        /// every thread must observe its own TaskEnvironment value and resolve its own pack.
        /// </summary>
        [Fact]
        public void Concurrent_threads_observe_their_own_TaskEnvironment_not_process_env()
        {
            const int threadCount = 64;
            const string envVarName = "DOTNETSDK_WORKLOAD_PACK_ROOTS";
            const string packName = "Microsoft.NETCore.App.Ref";
            const string packVersion = "9.0.0";

            var projectDirs = new string[threadCount];
            var runtimeGraphPaths = new string[threadCount];
            var workloadPackRoots = new string[threadCount];
            var expectedPackPaths = new string[threadCount];
            var observedPackPaths = new string?[threadCount];
            var results = new bool[threadCount];
            var exceptions = new Exception?[threadCount];

            // Decoy path used only at the process level to try to poison thread 0's resolution.
            var processDecoyRoot = Path.Combine(Path.GetTempPath(), $"decoy_proc_{Guid.NewGuid():N}");
            var originalProcessEnv = Environment.GetEnvironmentVariable(envVarName);

            for (int i = 0; i < threadCount; i++)
            {
                projectDirs[i] = Path.Combine(Path.GetTempPath(), $"proj_{Guid.NewGuid():N}");
                Directory.CreateDirectory(projectDirs[i]);
                runtimeGraphPaths[i] = CreateRuntimeGraphFile(MinimalRuntimeGraph);
                workloadPackRoots[i] = Path.Combine(Path.GetTempPath(), $"wpr_{Guid.NewGuid():N}");
                expectedPackPaths[i] = Path.Combine(workloadPackRoots[i], "packs", packName, packVersion);
                Directory.CreateDirectory(expectedPackPaths[i]);
            }

            // Barrier ensures all threads construct their TaskEnvironment (seeding the per-task
            // env var dictionary from the process env) BEFORE we mutate the process env var.
            // That way the test doesn't depend on seeding timing ΓÇö it purely verifies that
            // once the task is running, GetPackPath reads from the TaskEnvironment, not the
            // process env.
            using var setupBarrier = new System.Threading.Barrier(threadCount + 1);
            using var processEnvMutatedSignal = new System.Threading.ManualResetEventSlim(false);
            using var executeBarrier = new System.Threading.Barrier(threadCount);
            var cancellationToken = TestContext.Current.CancellationToken;

            try
            {
                var threads = new Thread[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    int idx = i;
                    threads[i] = new Thread(() =>
                    {
                        try
                        {
                            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDirs[idx]);
                            taskEnv.SetEnvironmentVariable(envVarName, workloadPackRoots[idx]);

                            // Wait until all threads have seeded their TaskEnvironments, then
                            // wait until the process-level env var has been poisoned before
                            // executing the task.
                            setupBarrier.SignalAndWait(cancellationToken);
                            processEnvMutatedSignal.Wait(cancellationToken);

                            var task = new ProcessFrameworkReferences
                            {
                                BuildEngine = new MockBuildEngine(),
                                TaskEnvironment = taskEnv,
                                TargetFrameworkIdentifier = ".NETCoreApp",
                                TargetFrameworkVersion = ToolsetInfo.CurrentTargetFrameworkVersion,
                                NETCoreSdkRuntimeIdentifier = "win-x64",
                                NetCoreRoot = projectDirs[idx],
                                NETCoreSdkVersion = "9.0.100",
                                RuntimeGraphPath = runtimeGraphPaths[idx],
                                EnableTargetingPackDownload = true,
                                FrameworkReferences = new ITaskItem[]
                                {
                                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                                },
                                KnownFrameworkReferences = new ITaskItem[]
                                {
                                    CreateKnownFrameworkReference("Microsoft.NETCore.App",
                                        ToolsetInfo.CurrentTargetFramework, packVersion)
                                },
                            };
                            executeBarrier.SignalAndWait(cancellationToken);
                            results[idx] = task.Execute();

                            if (task.TargetingPacks is { Length: > 0 })
                            {
                                observedPackPaths[idx] = task.TargetingPacks[0].GetMetadata(MetadataKeys.PackageDirectory);
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions[idx] = ex;
                        }
                    });
                    threads[i].Start();
                }

                // Wait until every thread has seeded its TaskEnvironment, then poison the
                // process-level env var. The threads only proceed to Execute() afterwards.
                setupBarrier.SignalAndWait(cancellationToken);
                Environment.SetEnvironmentVariable(envVarName, processDecoyRoot);
                processEnvMutatedSignal.Set();

                foreach (var t in threads) t.Join();

                for (int i = 0; i < threadCount; i++)
                {
                    exceptions[i].Should().BeNull($"thread {i} should not throw");
                    results[i].Should().BeTrue($"thread {i} should succeed");
                    observedPackPaths[i].Should().Be(expectedPackPaths[i],
                        because: $"thread {i} must resolve its pack from its own TaskEnvironment's " +
                                 $"{envVarName}, not from the poisoned process-level env var");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(envVarName, originalProcessEnv);

                for (int i = 0; i < threadCount; i++)
                {
                    try { Directory.Delete(projectDirs[i], recursive: true); } catch { }
                    try { Directory.Delete(workloadPackRoots[i], recursive: true); } catch { }
                    TryDeleteFile(runtimeGraphPaths[i]);
                }
            }
        }

        #region Helpers

        private const string MinimalRuntimeGraph = """
            {
                "runtimes": {
                    "any": {
                        "#import": ["base"]
                    },
                    "base": {
                        "#import": []
                    }
                }
            }
            """;

        private static MockTaskItem CreateKnownFrameworkReference(string name, string targetFramework, string version)
        {
            return new MockTaskItem(name, new Dictionary<string, string>
            {
                { "TargetFramework", targetFramework },
                { "RuntimeFrameworkName", name },
                { "DefaultRuntimeFrameworkVersion", version },
                { "LatestRuntimeFrameworkVersion", version },
                { "TargetingPackName", name.EndsWith(".Ref") ? name : $"{name}.Ref" },
                { "TargetingPackVersion", version },
            });
        }

        private ProcessFrameworkReferences CreateBasicTask(string runtimeGraphPath, string? targetingPackRoot, string? projectDir)
        {
            var effectiveProjectDir = projectDir ?? "C:/dotnet";
            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(effectiveProjectDir),
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = ToolsetInfo.CurrentTargetFrameworkVersion,
                NETCoreSdkRuntimeIdentifier = "win-x64",
                NetCoreRoot = effectiveProjectDir,
                NETCoreSdkVersion = "9.0.100",
                RuntimeGraphPath = runtimeGraphPath,
                EnableTargetingPackDownload = true,
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new ITaskItem[]
                {
                    CreateKnownFrameworkReference("Microsoft.NETCore.App",
                        ToolsetInfo.CurrentTargetFramework, "9.0.0")
                },
            };

            if (targetingPackRoot != null)
            {
                task.TargetingPackRoot = targetingPackRoot;
            }

            return task;
        }

        private static string CreateRuntimeGraphFile(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"rg_{Guid.NewGuid():N}.json");
            File.WriteAllText(path, content);
            return path;
        }

        private static string GetOutputSignature(ProcessFrameworkReferences task)
        {
            var parts = new List<string>();
            if (task.TargetingPacks != null)
            {
                foreach (var tp in task.TargetingPacks)
                    parts.Add($"TP:{tp.ItemSpec}@{tp.GetMetadata(MetadataKeys.NuGetPackageVersion)}");
            }
            if (task.RuntimeFrameworks != null)
            {
                foreach (var rf in task.RuntimeFrameworks)
                    parts.Add($"RF:{rf.ItemSpec}@{rf.GetMetadata(MetadataKeys.Version)}");
            }
            if (task.PackagesToDownload != null)
            {
                foreach (var pd in task.PackagesToDownload)
                    parts.Add($"PD:{pd.ItemSpec}@{pd.GetMetadata(MetadataKeys.Version)}");
            }
            parts.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("|", parts);
        }

        private static void TryDeleteFile(string path)
        {
            try { File.Delete(path); } catch { }
        }

        private static void CreateGlobalJson(string directory, string workloadSetVersion)
        {
            File.WriteAllText(Path.Combine(directory, "global.json"), $$"""
                {
                  "sdk": {
                    "workloadVersion": "{{workloadSetVersion}}"
                  }
                }
                """);
        }

        private static void CreateMockManifest(
            string manifestRoot,
            string featureBand,
            string manifestId,
            string manifestVersion,
            string packName,
            string packVersion)
        {
            var manifestDirectory = Path.Combine(manifestRoot, featureBand, manifestId, manifestVersion);
            Directory.CreateDirectory(manifestDirectory);

            File.WriteAllText(Path.Combine(manifestDirectory, "WorkloadManifest.json"), $$"""
                {
                  "version": "{{manifestVersion}}",
                  "packs": {
                    "{{packName}}" : {
                      "kind": "framework",
                      "version": "{{packVersion}}"
                    }
                  }
                }
                """);
        }

        private static void CreateMockWorkloadSet(
            string manifestRoot,
            string featureBand,
            string workloadSetVersion,
            string manifestId,
            string manifestVersion)
        {
            var workloadSetDirectory = Path.Combine(manifestRoot, featureBand, "workloadsets", workloadSetVersion);
            Directory.CreateDirectory(workloadSetDirectory);
            File.WriteAllText(Path.Combine(workloadSetDirectory, "workloadset.workloadset.json"), $$"""
                {
                  "{{manifestId}}": "{{manifestVersion}}/{{featureBand}}"
                }
                """);
        }

        #endregion
    }

    [CollectionDefinition("ProcessFrameworkReferences CWD", DisableParallelization = true)]
    public sealed class ProcessFrameworkReferencesCwdCollection
    {
    }
}
