// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestBasedOnGlobbingFilter : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestBasedOnGlobbingFilter(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
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
                                    .Execute(TestingPlatformOptions.TestModulesFilterOption.Name, $"**/bin/**/Debug/{ToolsetInfo.CurrentTargetFramework}/TestProject.dll".Replace('/', Path.DirectorySeparatorChar));

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

        [Fact]
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
                                    .Execute(TestingPlatformOptions.TestModulesFilterOption.Name, filterExpression);

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

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }


        [Fact]
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
                                    .Execute(TestingPlatformOptions.TestModulesFilterOption.Name, $"**/bin/**/Debug/{ToolsetInfo.CurrentTargetFramework}/TestProject.dll".Replace('/', Path.DirectorySeparatorChar),
                                    TestingPlatformOptions.TestModulesRootDirectoryOption.Name, testInstance.TestRoot);

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
