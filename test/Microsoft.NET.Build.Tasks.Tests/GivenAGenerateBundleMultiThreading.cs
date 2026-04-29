// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [CollectionDefinition(nameof(CwdSensitiveCollection), DisableParallelization = true)]
    public sealed class CwdSensitiveCollection
    {
    }

    /// <summary>
    /// Behavioral tests for <see cref="GenerateBundle"/>'s migration to <c>IMultiThreadableTask</c>.
    ///
    /// Fully driving the HostModel bundler end-to-end is impractical here because the test project
    /// does not reference <c>Microsoft.NET.HostModel</c> at runtime (the task project marks it with
    /// <c>ExcludeAssets="Runtime"</c>; HostModel is resolved from the SDK at MSBuild time). Execute
    /// coverage therefore uses the task's internal bundler seam, while direct helper tests still
    /// exercise <c>ResolveOutputDir</c> itself.
    ///
    /// The "decoy CWD" pattern is used throughout: the process's current directory is moved to a
    /// location different from the <c>TaskEnvironment.ProjectDirectory</c> so a bug that fell
    /// back to <c>Environment.CurrentDirectory</c> / <c>Path.GetFullPath</c> would surface here.
    /// </summary>
    [Collection(nameof(CwdSensitiveCollection))]
    public class GivenAGenerateBundleMultiThreading : IDisposable
    {
        private readonly List<string> _tempDirs = new();
        private readonly string _originalCwd;
        private readonly string _tempRoot;

        public GivenAGenerateBundleMultiThreading()
        {
            _originalCwd = Directory.GetCurrentDirectory();
            _tempRoot = Path.Combine(_originalCwd, $"{nameof(GivenAGenerateBundleMultiThreading)}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);
        }

        [Fact]
        public void GenerateBundle_IsRecognizedAsMsbuildMultiThreadable()
        {
            typeof(IMultiThreadableTask).IsAssignableFrom(typeof(GenerateBundle)).Should().BeTrue();
            typeof(GenerateBundle)
                .GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), inherit: false)
                .Should()
                .ContainSingle();
        }

        [Fact]
        public void ResolveOutputDir_RoutesRelativePathThroughTaskEnvironment_NotProcessCwd()
        {
            string projectDir = CreateTempDirectory("proj");
            string decoyCwd = CreateTempDirectory("decoy");
            Directory.SetCurrentDirectory(decoyCwd);

            var env = TaskEnvironmentHelper.CreateForTest(projectDir);

            string resolved = GenerateBundle.ResolveOutputDir(env, "publish/bundle");

            // TaskEnvironment.GetAbsolutePath concatenates paths preserving the original slash
            // style, so we normalise before comparing.
            string expected = Path.Combine(projectDir, "publish", "bundle");
            Path.GetFullPath(resolved).Should().Be(Path.GetFullPath(expected),
                "relative OutputDir must resolve under TaskEnvironment.ProjectDirectory");
            resolved.Should().NotStartWith(decoyCwd,
                "relative OutputDir must not leak the process CWD into the resolved path");
        }

        [Fact]
        public void ResolveOutputDir_PreservesAbsolutePath()
        {
            string projectDir = CreateTempDirectory("proj");
            string absoluteOut = Path.Combine(CreateTempDirectory("abs"), "bundle");
            Directory.SetCurrentDirectory(CreateTempDirectory("decoy"));

            var env = TaskEnvironmentHelper.CreateForTest(projectDir);

            string resolved = GenerateBundle.ResolveOutputDir(env, absoluteOut);

            // Path.GetFullPath normalisation is allowed but must not re-root under projectDir.
            Path.GetFullPath(resolved).Should().Be(Path.GetFullPath(absoluteOut));
            resolved.Should().NotStartWith(projectDir,
                "absolute OutputDir must not be re-anchored under ProjectDirectory");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ResolveOutputDir_DefaultsToProjectDirectory_WhenOutputDirIsNullOrEmpty(string outputDir)
        {
            string projectDir = CreateTempDirectory("proj");
            Directory.SetCurrentDirectory(CreateTempDirectory("decoy"));

            var env = TaskEnvironmentHelper.CreateForTest(projectDir);

            string resolved = GenerateBundle.ResolveOutputDir(env, outputDir);

            resolved.Should().Be(projectDir,
                "null/empty OutputDir must fall back to the project directory, not the process CWD");
        }

        [Fact]
        public void ExecuteWithRetry_InvokesBundlerWithTaskEnvironmentResolvedOutputDir()
        {
            string projectDir = CreateTempDirectory("proj");
            string decoyCwd = CreateTempDirectory("decoy");
            Directory.SetCurrentDirectory(decoyCwd);

            var task = CreateRecordingTask(projectDir, "publish/bundle");

            task.Execute().Should().BeTrue();

            task.InvocationCount.Should().Be(1);
            task.ObservedOutputDir.Should().NotBe(task.OutputDir,
                "ExecuteWithRetry must pass the resolved path to bundling, not raw OutputDir");
            Path.GetFullPath(task.ObservedOutputDir).Should().Be(
                Path.GetFullPath(Path.Combine(projectDir, "publish", "bundle")),
                "relative OutputDir must be resolved against TaskEnvironment.ProjectDirectory before bundling");
            Path.GetFullPath(task.ObservedOutputDir).Should().NotStartWith(Path.GetFullPath(decoyCwd),
                "the process CWD must not leak into the bundler output path");
        }

        [Fact]
        public async System.Threading.Tasks.Task ExecuteWithRetry_ConcurrentInstances_ResolveOutputDirPerTaskEnvironment()
        {
            const int concurrency = 64;
            Directory.SetCurrentDirectory(CreateTempDirectory("shared-decoy"));

            var projectDirs = new string[concurrency];
            var generateBundleTasks = new RecordingGenerateBundle[concurrency];
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            using var bundlersReady = new CountdownEvent(concurrency);
            using var releaseBundlers = new ManualResetEventSlim();

            for (int i = 0; i < concurrency; i++)
            {
                projectDirs[i] = CreateTempDirectory($"proj_{i}");
                generateBundleTasks[i] = CreateRecordingTask(projectDirs[i], "out", bundlersReady, releaseBundlers);
            }

            var executeResults = new bool[concurrency];
            var executions = new System.Threading.Tasks.Task[concurrency];

            for (int i = 0; i < concurrency; i++)
            {
                int taskIndex = i;
                executions[taskIndex] = System.Threading.Tasks.Task.Factory.StartNew(
                    () => executeResults[taskIndex] = generateBundleTasks[taskIndex].Execute(),
                    cancellationToken,
                    System.Threading.Tasks.TaskCreationOptions.LongRunning,
                    System.Threading.Tasks.TaskScheduler.Default);
            }

            bool allReachedBundling = bundlersReady.Wait(TimeSpan.FromSeconds(30), cancellationToken);
            releaseBundlers.Set();
            var allExecutions = System.Threading.Tasks.Task.WhenAll(executions);
            bool allFinished = await System.Threading.Tasks.Task.WhenAny(
                allExecutions,
                System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(30), cancellationToken)) == allExecutions;

            allReachedBundling.Should().BeTrue("all GenerateBundle instances should reach bundling before any are released");
            allFinished.Should().BeTrue("all GenerateBundle executions should complete after bundling is released");
            await allExecutions;

            for (int i = 0; i < concurrency; i++)
            {
                executeResults[i].Should().BeTrue($"instance {i} should complete successfully");
                generateBundleTasks[i].InvocationCount.Should().Be(1, $"instance {i} should invoke bundling exactly once");
                generateBundleTasks[i].ObservedOutputDir.Should().NotBe(generateBundleTasks[i].OutputDir,
                    $"instance {i} must pass the resolved path to bundling");
                Path.GetFullPath(generateBundleTasks[i].ObservedOutputDir).Should().Be(
                    Path.GetFullPath(Path.Combine(projectDirs[i], "out")),
                    $"instance {i} must resolve against its own TaskEnvironment");
            }
        }

        [Fact]
        public void GenerateBundle_ConstructedInTest_UsesAssignedTaskEnvironment()
        {
            // TaskEnvironment is assigned by MSBuild in production; unit tests must provide it
            // explicitly because MSBuild is not involved.
            string projectDir = CreateTempDirectory("proj");
            var task = new GenerateBundle
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };

            task.TaskEnvironment.Should().NotBeNull();
            Path.GetFullPath(GenerateBundle.ResolveOutputDir(task.TaskEnvironment, "rel"))
                .Should().Be(Path.GetFullPath(Path.Combine(projectDir, "rel")));
        }

        private static RecordingGenerateBundle CreateRecordingTask(
            string projectDir,
            string outputDir,
            CountdownEvent bundlingStarted = null,
            ManualResetEventSlim finishBundling = null)
        {
            return new RecordingGenerateBundle(bundlingStarted, finishBundling)
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                FilesToBundle = Array.Empty<ITaskItem>(),
                AppHostName = Path.Combine(projectDir, "apphost"),
                IncludeSymbols = false,
                IncludeNativeLibraries = false,
                IncludeAllContent = false,
                TargetFrameworkVersion = "8.0.0",
                RuntimeIdentifier = "win-x64",
                OutputDir = outputDir,
                ShowDiagnosticOutput = false,
                EnableCompressionInSingleFile = false,
                EnableMacOsCodeSign = false,
            };
        }

        private string CreateTempDirectory([CallerMemberName] string suffix = null)
        {
            string tempDir = Path.Combine(_tempRoot, $"{suffix}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            _tempDirs.Add(tempDir);
            return tempDir;
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalCwd);
            foreach (var dir in _tempDirs)
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }

            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        }

        private sealed class RecordingGenerateBundle : GenerateBundle
        {
            private readonly CountdownEvent _bundlingStarted;
            private readonly ManualResetEventSlim _finishBundling;

            public RecordingGenerateBundle(CountdownEvent bundlingStarted, ManualResetEventSlim finishBundling)
            {
                _bundlingStarted = bundlingStarted;
                _finishBundling = finishBundling;
            }

            public string ObservedOutputDir { get; private set; }

            public int InvocationCount { get; private set; }

            internal override System.Threading.Tasks.Task RunBundler(
                string resolvedOutputDir,
                OSPlatform targetOS,
                Architecture targetArch,
                Version version)
            {
                InvocationCount++;
                ObservedOutputDir = resolvedOutputDir;
                _bundlingStarted?.Signal();
                _finishBundling?.Wait();
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }
    }
}
