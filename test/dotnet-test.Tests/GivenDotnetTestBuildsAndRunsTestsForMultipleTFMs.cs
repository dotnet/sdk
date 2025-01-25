﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using dotnet.Tests;
using Microsoft.DotNet.Tools.Common;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestsForMultipleTFMs : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestsForMultipleTFMs(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunMultipleProjectWithDifferentTFMsWithFailingTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("ProjectSolutionForMultipleTFMs", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                MatchCollection net8ProjectMatches = Regex.Matches(result.StdOut!, $@".+{PathUtility.GetDirectorySeparatorChar()}net8\.0{PathUtility.GetDirectorySeparatorChar()}TestProjectWithNet8\.dll\s+\(net8.0\|[a-zA-Z][1-9]+\)\sfailed");
                MatchCollection net9ProjectMatches = Regex.Matches(result.StdOut!, $@".+{PathUtility.GetDirectorySeparatorChar()}net9\.0{PathUtility.GetDirectorySeparatorChar()}TestProjectWithNet9\.dll\s+\(net9.0\|[a-zA-Z][1-9]+\)\spassed");

                MatchCollection skippedTestsMatches = Regex.Matches(result.StdOut!, "skipped Test2");
                MatchCollection failedTestsMatches = Regex.Matches(result.StdOut!, "failed Test3");

                Assert.True(net8ProjectMatches.Count > 1);
                Assert.True(net9ProjectMatches.Count > 1);

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

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunProjectWithMultipleTFMsWithFailingTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                var net8ProjectMatches = Regex.Matches(result.StdOut!, $@".+{PathUtility.GetDirectorySeparatorChar()}net8\.0{PathUtility.GetDirectorySeparatorChar()}TestProject\.dll\s+\(net8.0\|[a-zA-Z][1-9]+\)\sfailed");
                var net9ProjectMatches = Regex.Matches(result.StdOut!, $@".+{PathUtility.GetDirectorySeparatorChar()}net9\.0{PathUtility.GetDirectorySeparatorChar()}TestProject\.dll\s+\(net9.0\|[a-zA-Z][1-9]+\)\sfailed");

                MatchCollection skippedTestsMatches = Regex.Matches(result.StdOut!, "skipped Test1");
                MatchCollection failedTestsMatches = Regex.Matches(result.StdOut!, "failed Test2");
                MatchCollection timeoutTestsMatches = Regex.Matches(result.StdOut!, @"failed \(canceled\) Test3");
                MatchCollection errorTestsMatches = Regex.Matches(result.StdOut!, "failed Test4");
                MatchCollection canceledTestsMatches = Regex.Matches(result.StdOut!, @"failed \(canceled\) Test5");

                Assert.True(net8ProjectMatches.Count > 1);
                Assert.True(net9ProjectMatches.Count > 1);

                Assert.Multiple(() => Assert.Equal(2, skippedTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, failedTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, timeoutTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, errorTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, skippedTestsMatches.Count));

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 12")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 8")
                    .And.Contain("skipped: 2");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }


        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunProjectWithMSTestMetaPackageAndMultipleTFMsWithFailingTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MSTestMetaPackageProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                MatchCollection net8ProjectMatches = Regex.Matches(result.StdOut!, $@".+{PathUtility.GetDirectorySeparatorChar()}net8\.0{PathUtility.GetDirectorySeparatorChar()}TestProject\.dll\s+\(net8.0\|[a-zA-Z][1-9]+\)\sfailed");
                MatchCollection net9ProjectMatches = Regex.Matches(result.StdOut!, $@".+{PathUtility.GetDirectorySeparatorChar()}net9\.0{PathUtility.GetDirectorySeparatorChar()}TestProject\.dll\s+\(net9.0\|[a-zA-Z][1-9]+\)\sfailed");

                MatchCollection failedTestsMatches = Regex.Matches(result.StdOut!, "failed TestMethod3");

                Assert.True(net8ProjectMatches.Count > 1);
                Assert.True(net9ProjectMatches.Count > 1);

                Assert.Multiple(() => Assert.Equal(2, failedTestsMatches.Count));

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 5")
                    .And.Contain("succeeded: 3")
                    .And.Contain("failed: 2")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }
    }
}
