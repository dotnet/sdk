// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Common;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsHelp : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsHelp(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunHelpOnTestProject_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolutionWithTestsAndArtifacts", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(CliConstants.HelpOptionKey, TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@"Extension Options:\s+--[\s\S]*", result.StdOut);
                Assert.Matches(@"Options:\s+--[\s\S]*", result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunHelpOnMultipleTestProjects_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("ProjectSolutionForMultipleTFMs", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFramework($"{DotnetVersionHelper.GetPreviousDotnetVersion()}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(CliConstants.ShortHelpOptionKey, TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@"Extension Options:\s+--[\s\S]*", result.StdOut);
                Assert.Matches(@"Options:\s+--[\s\S]*", result.StdOut);

                string directorySeparator = PathUtility.GetDirectorySeparatorChar();
                string otherTestProjectPattern = @$"Unavailable extension options:\s+.*{directorySeparator}{ToolsetInfo.CurrentTargetFramework}{directorySeparator}OtherTestProject\.dll.*\s+(--report-trx\s+--report-trx-filename|--report-trx-filename\s+--report-trx)";

                Assert.Matches(otherTestProjectPattern, result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }
    }
}
