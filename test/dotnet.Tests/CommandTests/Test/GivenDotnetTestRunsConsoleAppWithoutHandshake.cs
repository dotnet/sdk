// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestRunsConsoleAppWithoutHandshake : SdkTest
    {
        public GivenDotnetTestRunsConsoleAppWithoutHandshake(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunConsoleAppDoesNothing_ShouldReturnCorrectExitCode(string configuration)
        {
            // This test validates the behavior when running `dotnet test` against a console application
            // that does "nothing" and doesn't handshake with us.
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("ConsoleAppDoesNothing", Guid.NewGuid().ToString())
                .WithSource();

            // Pass -bl so any future hang produces an MSBuild binlog that the test framework
            // uploads from the working directory (see https://github.com/dotnet/sdk/issues/54580).
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration, "-bl");

            result.ExitCode.Should().Be(ExitCodes.GenericFailure, "dotnet test should fail with a meaningful error when run against console app without MTP handshake");
        }

        /// <summary>
        /// End-to-end coverage for https://github.com/dotnet/sdk/issues/51608: when an assembly
        /// fails to hand-shake (here: a console app that exits without ever talking the MTP
        /// protocol), the runner must:
        ///   1) re-print a "Handshake failures:" recap at the end of the run that includes the
        ///      failing assembly identifier, so the user does not have to scroll back through
        ///      diagnostic output to find the actionable cause, and
        ///   2) print "Failed!" — not "Zero tests ran" — as the summary headline, so a handshake
        ///      failure is not masked by the empty-run wording.
        /// </summary>
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunConsoleAppDoesNothing_ShouldReprintHandshakeFailureRecapAndPrintFailedSummary(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("ConsoleAppDoesNothing", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);

            if (!SdkTestContext.IsLocalized())
            {
                string stdout = result.StdOut ?? string.Empty;

                // 1) End-of-run recap header is present and the recap section includes the failing assembly identifier.
                int recapHeaderIndex = stdout.IndexOf("Handshake failures:", StringComparison.Ordinal);
                recapHeaderIndex.Should().BeGreaterThanOrEqualTo(0, "the run should re-print a 'Handshake failures:' recap at the end so users do not have to scroll back to find the actionable cause");

                string recap = stdout.Substring(recapHeaderIndex);
                recap.Should().Contain("ConsoleAppDoesNothing", "the recap must identify which assembly failed to hand-shake");

                // 2) The summary headline must say "Failed!" instead of "Zero tests ran" (the previous,
                //    pre-fix behavior masked handshake failures as a benign empty run). The per-assembly
                //    progress can still legitimately say "Zero tests ran" because the assembly really
                //    did register zero tests; we scope the negative assertion to the summary line.
                int summaryIndex = stdout.IndexOf("Test run summary:", StringComparison.Ordinal);
                summaryIndex.Should().BeGreaterThanOrEqualTo(0, "the run should print a 'Test run summary:' section");

                int summaryEnd = stdout.IndexOf('\n', summaryIndex);
                string summaryLine = summaryEnd < 0 ? stdout.Substring(summaryIndex) : stdout.Substring(summaryIndex, summaryEnd - summaryIndex);

                summaryLine.Should().Contain("Failed!", "handshake/assembly failures must take precedence over the 'Zero tests ran' headline");
                summaryLine.Should().NotContain("Zero tests ran", "the summary headline must not mask a handshake failure as an empty run");
            }
        }
    }
}
