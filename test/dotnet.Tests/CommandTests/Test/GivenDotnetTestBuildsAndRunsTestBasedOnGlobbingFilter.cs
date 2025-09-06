// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestBasedOnGlobbingFilter : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestBasedOnGlobbingFilter(ITestOutputHelper log) : base(log)
        {
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunTestProjectWithFilterOfDll_ShouldReturnExitCodeSuccess()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}{Path.DirectorySeparatorChar}bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.TestModulesFilterOption.Name, $"**/bin/**/Debug/{ToolsetInfo.CurrentTargetFramework}/TestProject.dll".Replace('/', Path.DirectorySeparatorChar));

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory?.LastWriteTime);


            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Passed, true, TestingConstants.Debug), result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 2")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunTestProjectsWithFilterOfDll_ShouldReturnExitCodeGenericFailure()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            new BuildCommand(testInstance, "TestProject")
                .Execute()
                .Should().Pass();

            new BuildCommand(testInstance, "OtherTestProject")
              .Execute()
              .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}{Path.DirectorySeparatorChar}bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            string filterExpression = $"**/bin/**/Debug/{ToolsetInfo.CurrentTargetFramework}/*TestProject.dll".Replace('/', Path.DirectorySeparatorChar);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.TestModulesFilterOption.Name, filterExpression);

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory?.LastWriteTime);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, TestingConstants.Debug), result.StdOut);
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, true, TestingConstants.Debug), result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 5")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 2");
            }

            // TestProject produces 1 passed, 1 failed, and 1 skipped.
            // OtherTestProject produces 1 passed and 1 skipped.
            // We got 2 exit codes. Success and AtLeastOneTestFailed. So, we aggregate the final exit code to AtLeastOneTestFailed.
            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }


        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunTestProjectWithFilterOfDllWithRootDirectory_ShouldReturnExitCodeSuccess()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(MicrosoftTestingPlatformOptions.TestModulesFilterOption.Name, $"**/bin/**/Debug/{ToolsetInfo.CurrentTargetFramework}/TestProject.dll".Replace('/', Path.DirectorySeparatorChar),
                                    MicrosoftTestingPlatformOptions.TestModulesRootDirectoryOption.Name, testInstance.TestRoot);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Passed, true, TestingConstants.Debug), result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 2")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }
    }
}
