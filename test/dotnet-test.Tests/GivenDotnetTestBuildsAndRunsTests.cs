// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTests : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void RunTestProjectWithNoTests_ShouldReturnOneAsExitCode()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute();

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Zero tests ran")
                    .And.Contain("total: 0")
                    .And.Contain("succeeded: 0")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void RunMultipleTestProjectsWithNoTests_ShouldReturnOneAsExitCode()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultipleTestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute();

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Zero tests ran")
                    .And.Contain("total: 0")
                    .And.Contain("succeeded: 0")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void RunTestProjectWithTests_ShouldReturnZeroAsExitCode()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute();

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("skipped Test1")
                    .And.Contain("total: 2")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(0);
        }

        [Fact]
        public void RunMultipleTestProjectsWithFailingTests_ShouldReturnOneAsExitCode()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute();

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 5")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 2");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void RunMultipleTestProjectsWithDifferentFailures_ShouldReturnOneAsExitCode()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithDifferentFailures", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute("--minimum-expected-tests 2");

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@".+\\net8\.0\\TestProject\.dll\s+\(net8.0\|[a-zA-Z][1-9]+\)\spassed.*\s+Exit code: 8", result.StdOut);
                Assert.Matches(@".+\\net8\.0\\OtherTestProject\.dll\s+\(net8.0\|[a-zA-Z][1-9]+\)\sfailed.*\s+Exit code: 2", result.StdOut);
                Assert.Matches(@".+\\net8\.0\\AnotherTestProject\.dll\s+\(net8.0\|[a-zA-Z][1-9]+\)\spassed.*\s+Exit code: 9", result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 4")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(1);
        }
    }
}
