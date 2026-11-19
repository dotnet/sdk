// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using TestCommand = Microsoft.DotNet.Cli.Commands.Test.TestCommand;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    /// <summary>
    /// Covers how the SDK decides whether a test session produced artifacts to post-process. The session
    /// subdirectory inside the artifacts directory is the only thing the SDK interprets about the contents.
    /// </summary>
    [TestClass]
    public class VSTestArtifactDirectoryDiscoveryTests
    {
        private readonly List<string> _createdDirectories = [];

        [TestCleanup]
        public void Cleanup()
        {
            foreach (string directory in _createdDirectories)
            {
                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            _createdDirectories.Clear();
        }

        [TestMethod]
        public void ItFindsTheSessionSubdirectoryOfTheArtifactsDirectory()
        {
            string artifactsDirectory = VSTestArtifactsDirectory.GetOrCreate()!;
            string correlationId = NewCorrelationId();
            string expected = CreateDirectory(Path.Combine(artifactsDirectory, correlationId));

            TestCommand.TryFindArtifactDirectory(correlationId, out string? artifactDirectory).Should().BeTrue();
            artifactDirectory.Should().Be(expected);
        }

        [TestMethod]
        public void ItReportsNoArtifactDirectoryWhenTheSessionProducedNone()
        {
            VSTestArtifactsDirectory.GetOrCreate();

            TestCommand.TryFindArtifactDirectory(NewCorrelationId(), out string? artifactDirectory).Should().BeFalse();
            artifactDirectory.Should().BeNull();
        }

        [TestMethod]
        public void ItIgnoresTheSystemTemporaryDirectory()
        {
            // The correlation id appears on the vstest.console and MSBuild command lines, so it is readable by
            // any local user. Probing the world-writable system temporary directory for it would let another user
            // steer post-processing onto a directory they control, so it must not be probed at all.
            VSTestArtifactsDirectory.GetOrCreate();

            string correlationId = NewCorrelationId();
            CreateDirectory(Path.Combine(Path.GetTempPath(), correlationId));

            TestCommand.TryFindArtifactDirectory(correlationId, out string? artifactDirectory).Should().BeFalse();
            artifactDirectory.Should().BeNull();
        }

        [TestMethod]
        public void ItReportsNoArtifactDirectoryWhenNoVSTestConsoleWasStarted()
        {
            VSTestArtifactsDirectory.Delete();

            try
            {
                TestCommand.TryFindArtifactDirectory(NewCorrelationId(), out string? artifactDirectory).Should().BeFalse();
                artifactDirectory.Should().BeNull();
            }
            finally
            {
                VSTestArtifactsDirectory.GetOrCreate();
            }
        }

        [TestMethod]
        public void ItSkipsPostProcessingWithoutStartingVSTestWhenThereAreNoArtifacts()
        {
            if (FeatureFlag.Instance.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                Assert.Inconclusive($"{FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING} is set, post-processing returns early before the lookup.");
            }

            // The early return is what keeps a session that produced nothing from paying for a vstest.console
            // start-up. If the lookup regressed, this test would fail trying to run vstest.console.
            int exitCode = TestCommand.RunArtifactPostProcessingIfNeeded(
                NewCorrelationId(),
                diag: null,
                FeatureFlag.Instance);

            exitCode.Should().Be(0);
        }

        private string CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
            _createdDirectories.Add(path);
            return path;
        }

        private static string NewCorrelationId() => $"{Environment.ProcessId}_{Guid.NewGuid()}";
    }
}
