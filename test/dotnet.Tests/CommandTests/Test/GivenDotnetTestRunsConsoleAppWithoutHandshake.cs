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
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure, "dotnet test should fail with a meaningful error when run against console app without MTP handshake");
        }
    }
}