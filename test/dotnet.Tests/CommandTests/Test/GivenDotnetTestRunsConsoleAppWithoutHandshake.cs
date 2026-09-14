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
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("ConsoleAppDoesNothing", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure, "dotnet test should fail with a meaningful error when run against console app without MTP handshake");

            if (!SdkTestContext.IsLocalized())
            {
                string stdout = result.StdOut ?? string.Empty;
                int recapHeaderIndex = stdout.IndexOf("Handshake failures:", StringComparison.Ordinal);
                recapHeaderIndex.Should().BeGreaterThanOrEqualTo(0);
                stdout.Substring(recapHeaderIndex).Should().Contain("ConsoleAppDoesNothing");

                int summaryIndex = stdout.IndexOf("Test run summary:", StringComparison.Ordinal);
                summaryIndex.Should().BeGreaterThanOrEqualTo(0);
                int summaryEnd = stdout.IndexOf('\n', summaryIndex);
                string summaryLine = summaryEnd < 0 ? stdout.Substring(summaryIndex) : stdout.Substring(summaryIndex, summaryEnd - summaryIndex);

                summaryLine.Should().Contain("Failed!");
                summaryLine.Should().NotContain("Zero tests ran");
            }
        }

        [Fact]
        public void RunConsoleAppWithInvalidOptionError_ShouldSurfaceErrorOutput()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("ConsoleAppDoesNothing", Guid.NewGuid().ToString())
                .WithSource();
            File.WriteAllText(
                Path.Combine(testInstance.Path, "Program.cs"),
                """
                System.Console.Error.WriteLine("Option '--unsupported-option' is not recognized.");
                return 5;
                """);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--unsupported-option");

            result.ExitCode.Should().Be(5);
            result.StdOut.Should().Contain("Option '--unsupported-option' is not recognized.");
            result.StdOut.Should().Contain("Test run summary: Failed!");
        }
    }
}
