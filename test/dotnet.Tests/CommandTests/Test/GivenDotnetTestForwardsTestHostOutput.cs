// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests;

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
    private const string FailingRunStandardOutputMarker = "FAILING_RUN_STANDARD_OUTPUT";
    private const string FailingRunStandardErrorMarker = "FAILING_RUN_STANDARD_ERROR";

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

        Exception? sentinelFailure = null;
        var command = new DotnetTestCommand(Log, disableNewOutput: false)
                                .WithWorkingDirectory(testInstance.Path)
                                .WithEnvironmentVariable(SentinelPathEnvironmentVariable, sentinelPath);

        // Execute retries the command on transient failures without re-copying the asset, so
        // clear the sentinel on every attempt: a file left behind by an earlier attempt would
        // let the app proceed immediately and quietly void what this test is proving.
        // This runs after the command's process has started but before it is registered with
        // the process reaper, so an exception escaping here would leave that process orphaned.
        command.ProcessStartedHandler = _ =>
        {
            try
            {
                File.Delete(sentinelPath);
            }
            catch (Exception ex)
            {
                sentinelFailure ??= ex;
            }
        };
        command.CommandOutputHandler = line =>
        {
            if (line.Contains(StandardOutputMarker, StringComparison.Ordinal) && !File.Exists(sentinelPath))
            {
                try
                {
                    File.WriteAllText(sentinelPath, string.Empty);
                }
                catch (Exception ex)
                {
                    // This runs on the only thread draining the command's standard output.
                    // Letting the exception escape would stop that drain and hang the run, so
                    // record it and let the assertion below report it.
                    sentinelFailure ??= ex;
                }
            }
        };

        CommandResult result = command.Execute("-c", configuration);

        sentinelFailure.Should().BeNull("the sentinel file should be writable and deletable");

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

    /// <summary>
    /// Regression test for https://github.com/dotnet/sdk/issues/55549. A non-zero exit code makes
    /// 'dotnet test' render its exit code summary, which used to replay the whole captured standard
    /// output and error even though live output had already shown them, so every line appeared twice.
    /// </summary>
    [DataRow(TestingConstants.Debug)]
    [DataRow(TestingConstants.Release)]
    [TestMethod]
    public void RunFailingTestProjectWritingToConsole_ShouldNotAlsoReplayTheOutputInTheExitCodeSummary(string configuration)
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithFailingTestAndConsoleOutput", Guid.NewGuid().ToString())
            .WithSource();

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                .WithWorkingDirectory(testInstance.Path)
                                .Execute("-c", configuration);

        // Exactly once: the live stream is the complete copy and the only one the user needs. The
        // summary's copy is additionally lossy (it is truncated for long output), so it is the one
        // that has to go.
        CountOccurrences(result.StdOut, FailingRunStandardOutputMarker).Should().Be(1);
        CountOccurrences(result.StdOut, FailingRunStandardErrorMarker).Should().Be(1);

        // ...and that single copy has to be the live one. Live output is written verbatim, so the marker
        // sits alone on its line, whereas the summary folds it into an indented "<label>: <output>" line.
        // Without this the assertion above would also pass against a host that never streams at all, which
        // would silently stop exercising the regression. Comparing trimmed lines keeps it locale-agnostic.
        string[] trimmedLines = [.. (result.StdOut ?? string.Empty).Split('\n').Select(line => line.Trim())];
        trimmedLines.Should().Contain(FailingRunStandardOutputMarker,
            "the marker must be the live copy, not folded into the exit code summary");
        trimmedLines.Should().Contain(FailingRunStandardErrorMarker,
            "the marker must be the live copy, not folded into the exit code summary");

        result.ExitCode.Should().NotBe(ExitCodes.Success);
    }

    private static int CountOccurrences(string? text, string value)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int count = 0;
        int index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
