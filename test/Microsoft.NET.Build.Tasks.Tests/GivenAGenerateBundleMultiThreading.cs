// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Behavioral tests for <see cref="GenerateBundle"/>'s migration to <c>IMultiThreadableTask</c>.
    ///
    /// Fully driving the bundler end-to-end is impractical here because the test project does not
    /// reference <c>Microsoft.NET.HostModel</c> at runtime (the task project marks it with
    /// <c>ExcludeAssets="Runtime"</c>; HostModel is resolved from the SDK at MSBuild time). We
    /// therefore target the single migration-relevant unit — path resolution — via the task's
    /// internal <c>ResolveOutputDir</c> helper, exercising the actual production code path rather
    /// than only the <c>TaskEnvironment</c> helper's own methods.
    ///
    /// The "decoy CWD" pattern is used throughout: the process's current directory is moved to a
    /// location different from the <c>TaskEnvironment.ProjectDirectory</c> so a bug that fell
    /// back to <c>Environment.CurrentDirectory</c> / <c>Path.GetFullPath</c> would surface here.
    /// </summary>
    [Collection("CwdSensitive")]
    public class GivenAGenerateBundleMultiThreading : IDisposable
    {
        private readonly List<string> _tempDirs = new();
        private readonly string _originalCwd;

        public GivenAGenerateBundleMultiThreading()
        {
            _originalCwd = Directory.GetCurrentDirectory();
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
        public void ResolveOutputDir_ConcurrentInstances_ProduceIsolatedResults()
        {
            const int concurrency = 8;
            Directory.SetCurrentDirectory(CreateTempDirectory("shared-decoy"));

            var projectDirs = new string[concurrency];
            var environments = new Microsoft.Build.Framework.TaskEnvironment[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                projectDirs[i] = CreateTempDirectory($"proj_{i}");
                environments[i] = TaskEnvironmentHelper.CreateForTest(projectDirs[i]);
            }

            var results = new string[concurrency];

            Parallel.For(0, concurrency, i =>
            {
                // Each iteration invokes ResolveOutputDir (the production code path) with the
                // same relative OutputDir but a distinct TaskEnvironment; results must not cross.
                results[i] = GenerateBundle.ResolveOutputDir(environments[i], "out");
            });

            for (int i = 0; i < concurrency; i++)
            {
                Path.GetFullPath(results[i]).Should().Be(Path.GetFullPath(Path.Combine(projectDirs[i], "out")),
                    $"instance {i} must resolve against its own TaskEnvironment");
            }
        }

        [Fact]
        public void GenerateBundle_ConstructedInTest_HasUsableTaskEnvironment_OnNetCore()
        {
            // Regression guard for D8 (NRE prevention): on .NET Core the TaskEnvironment property
            // is a bare auto-property with no lazy fallback. Tests that forget to assign it would
            // throw NullReferenceException the moment ResolveOutputDir is called. This test proves
            // the supported wiring — new Task + assign TaskEnvironment — works end-to-end.
            string projectDir = CreateTempDirectory("proj");
            var task = new GenerateBundle
            {
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
            };

            task.TaskEnvironment.Should().NotBeNull();
            Path.GetFullPath(GenerateBundle.ResolveOutputDir(task.TaskEnvironment, "rel"))
                .Should().Be(Path.GetFullPath(Path.Combine(projectDir, "rel")));
        }

        private string CreateTempDirectory([CallerMemberName] string suffix = null)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"GenerateBundleTest_{suffix}_{Guid.NewGuid():N}");
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
        }
    }
}
