// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    /// <summary>
    /// End-to-end coverage for https://github.com/dotnet/sdk/issues/51615: the console output of a
    /// test application must reach the user while the run is in progress, not only when a test fails.
    /// </summary>
    [TestClass]
    public class GivenDotnetTestForwardsTestHostOutput : SdkTest
    {
        private const string OutputBeforeHandshake = "LIVE_OUTPUT_BEFORE_HANDSHAKE";
        private const string StandardOutputDuringRun = "LIVE_OUTPUT_STANDARD_OUTPUT";
        private const string StandardErrorDuringRun = "LIVE_OUTPUT_STANDARD_ERROR";

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void RunTestProjectWritingToConsole_ShouldForwardOutputEvenWhenAllTestsPass(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithLiveOutput", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration);

            // The test app writes to the console before the handshake and while the test session runs.
            // Both must show up even though the run succeeds and no output-related option was passed.
            result.StdOut
                .Should().Contain(OutputBeforeHandshake)
                .And.Contain(StandardOutputDuringRun)
                .And.Contain(StandardErrorDuringRun);

            if (!SdkTestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 1")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");

                // The output is streamed as it is produced, so it precedes the end-of-run summary
                // instead of being replayed as part of a failure report.
                result.StdOut!.IndexOf(StandardOutputDuringRun, StringComparison.Ordinal)
                    .Should().BeLessThan(result.StdOut!.IndexOf("Test run summary:", StringComparison.Ordinal));
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }
    }
}
