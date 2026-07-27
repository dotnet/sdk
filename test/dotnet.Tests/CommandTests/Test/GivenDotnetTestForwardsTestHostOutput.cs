// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    /// <summary>
    /// End-to-end coverage for https://github.com/dotnet/sdk/issues/51615: what a test application
    /// writes to the console must reach the user while the run is in progress, and not only be
    /// replayed when a test fails.
    /// </summary>
    [TestClass]
    public class GivenDotnetTestForwardsTestHostOutput : SdkTest
    {
        private const string SentinelPathEnvironmentVariable = "LIVE_OUTPUT_SENTINEL_PATH";
        private const string StandardOutputMarker = "LIVE_OUTPUT_STANDARD_OUTPUT";
        private const string StandardErrorMarker = "LIVE_OUTPUT_STANDARD_ERROR";
        private const string OutputDeviceSessionMessageMarker = "LIVE_OUTPUT_SESSION_MESSAGE";
        private const string OutputDeviceWarningMarker = "LIVE_OUTPUT_WARNING_MESSAGE";
        private const string OutputDeviceErrorMarker = "LIVE_OUTPUT_ERROR_MESSAGE";
        private const string OutputBeforeHandshakeMarker = "OUTPUT_BEFORE_HANDSHAKE";

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void RunTestProjectWritingToConsole_ShouldForwardOutputWhileTheRunIsInProgress(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithLiveOutput", Guid.NewGuid().ToString())
                .WithSource();

            // The test app blocks until this file exists, and the file is only created once the
            // marker it wrote has been observed on the live standard output of 'dotnet test'.
            // An implementation that buffers the test app's output until it exits therefore
            // deadlocks the app until its own timeout expires, which fails the run.
            string sentinelPath = Path.Combine(testInstance.Path, "live-output-observed.sentinel");

            var command = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnvironmentVariable(SentinelPathEnvironmentVariable, sentinelPath);
            command.CommandOutputHandler = line =>
            {
                if (line.Contains(StandardOutputMarker, StringComparison.Ordinal) && !File.Exists(sentinelPath))
                {
                    File.WriteAllText(sentinelPath, string.Empty);
                }
            };

            CommandResult result = command.Execute("-c", configuration);

            // The run succeeds, so nothing replays the test app's output as part of a failure
            // report: the markers can only be present because they were forwarded as produced.
            // The output device markers additionally cover the text the test app routes through
            // the platform's IOutputDevice, which reaches the SDK as protocol 1.3.0 display
            // messages and is rendered at its informational, warning and error levels.
            result.StdOut
                .Should().Contain(StandardOutputMarker)
                .And.Contain(StandardErrorMarker)
                .And.Contain(OutputDeviceSessionMessageMarker)
                .And.Contain(OutputDeviceWarningMarker)
                .And.Contain(OutputDeviceErrorMarker);

            if (!SdkTestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 1")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void RunTestProjectWritingToConsoleBeforeHandshake_ShouldForwardOutputOnceProtocolIsNegotiated(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithOutputBeforeHandshake", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration);

            // The test app only writes before the handshake completes, so this output has to be
            // buffered until the protocol version is known and then flushed.
            result.StdOut.Should().Contain(OutputBeforeHandshakeMarker);

            if (!SdkTestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 1")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }
    }
}
