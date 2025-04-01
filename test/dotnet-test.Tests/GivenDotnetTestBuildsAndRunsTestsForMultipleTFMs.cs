// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestsForMultipleTFMs : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestsForMultipleTFMs(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunMultipleProjectWithDifferentTFMs_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("ProjectSolutionForMultipleTFMs", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFramework($"{DotnetVersionHelper.GetPreviousDotnetVersion()}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                MatchCollection previousDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: false, configuration));
                MatchCollection currentDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, useCurrentVersion: true, configuration));

                MatchCollection skippedTestsMatches = Regex.Matches(result.StdOut!, "skipped Test2");
                MatchCollection failedTestsMatches = Regex.Matches(result.StdOut!, "failed Test3");

                Assert.True(previousDotnetProjectMatches.Count > 1);
                Assert.True(currentDotnetProjectMatches.Count > 1);

                Assert.Single(failedTestsMatches);
                Assert.Multiple(() => Assert.Equal(2, skippedTestsMatches.Count));

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 5")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 2");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunProjectWithMultipleTFMs_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                MatchCollection previousDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: false, configuration));
                MatchCollection currentDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: true, configuration));
                MatchCollection currentDotnetOtherProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, useCurrentVersion: true, configuration));

                MatchCollection skippedTestsMatches = Regex.Matches(result.StdOut!, "skipped Test1");
                MatchCollection failedTestsMatches = Regex.Matches(result.StdOut!, "failed Test2");
                MatchCollection timeoutTestsMatches = Regex.Matches(result.StdOut!, @"failed \(canceled\) Test3");
                MatchCollection errorTestsMatches = Regex.Matches(result.StdOut!, "failed Test4");
                MatchCollection canceledTestsMatches = Regex.Matches(result.StdOut!, @"failed \(canceled\) Test5");

                Assert.True(previousDotnetProjectMatches.Count > 1);
                Assert.True(currentDotnetProjectMatches.Count > 1);
                Assert.True(currentDotnetOtherProjectMatches.Count > 1);

                Assert.Multiple(() => Assert.Equal(2, skippedTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, failedTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, timeoutTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, errorTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, skippedTestsMatches.Count));

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 14")
                    .And.Contain("succeeded: 3")
                    .And.Contain("failed: 8")
                    .And.Contain("skipped: 3");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunProjectWithMultipleTFMsWithArchOption_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");
            var arch = RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration,
                                    CommonOptions.ArchitectureOption.Name, arch);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 14")
                    .And.Contain("succeeded: 3")
                    .And.Contain("failed: 8")
                    .And.Contain("skipped: 3");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunProjectWithMSTestMetaPackageAndMultipleTFMs_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MSTestMetaPackageProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                MatchCollection previousDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: false, configuration));
                MatchCollection currentDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: true, configuration));

                MatchCollection failedTestsMatches = Regex.Matches(result.StdOut!, "failed TestMethod3");

                Assert.True(previousDotnetProjectMatches.Count > 1);
                Assert.True(currentDotnetProjectMatches.Count > 1);

                Assert.Multiple(() => Assert.Equal(2, failedTestsMatches.Count));

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 5")
                    .And.Contain("succeeded: 3")
                    .And.Contain("failed: 2")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }
    }
}
