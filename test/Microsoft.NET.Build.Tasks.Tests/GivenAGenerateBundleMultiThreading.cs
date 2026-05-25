// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using FluentAssertions;
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
    /// coverage therefore uses the task's internal bundler seam, while direct helper tests exercise
    /// path resolution before values flow to HostModel file operations.
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
    }
}
