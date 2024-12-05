// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using dotnet.Tests;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndDiscoversTests : SdkTest
    {
        public GivenDotnetTestBuildsAndDiscoversTests(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void DiscoverTestProjectWithNoTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute("--list-tests", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@"Discovered 0 tests.*\\TestProject.dll\s\(net8.0\|[a-zA-Z][0-9]+\)", result.StdOut);

                result.StdOut
                    .Should().Contain("Discovered 0 tests.");
            }

            result.ExitCode.Should().Be(1);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void DiscoverMultipleTestProjectsWithNoTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultipleTestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute("--list-tests", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@"Discovered 0 tests.*\\TestProject.dll\s\(net8.0\|[a-zA-Z][0-9]+\)", result.StdOut);
                Assert.Matches(@"Discovered 0 tests.*\\OtherTestProject.dll\s\(net8.0\|[a-zA-Z][0-9]+\)", result.StdOut);
                Assert.Matches(@"Discovered 0 tests.*\\AnotherTestProject.dll\s\(net8.0\|[a-zA-Z][0-9]+\)", result.StdOut);
                Assert.Matches(@"Discovered 0 tests.*", result.StdOut);
            }

            result.ExitCode.Should().Be(1);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void DiscoverTestProjectWithTests_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithDiscoveredTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute("--list-tests", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@"Discovered 1 tests.*\\TestProject.dll\s\(net8.0\|[a-zA-Z][0-9]+\)\s+Test0", result.StdOut);
                Assert.Matches(@"Discovered 1 tests.*", result.StdOut);
            }

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void DiscoverMultipleTestProjectsWithTests_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithDiscoveredTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute("--list-tests", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@"Discovered 2 tests.*\\TestProject.dll\s\(net8.0\|[a-zA-Z][0-9]+\)\s+Test0\s+Test2", result.StdOut);
                Assert.Matches(@"Discovered 1 tests.*\\OtherTestProject.dll\s\(net8.0\|[a-zA-Z][0-9]+\)\s+Test1", result.StdOut);
                Assert.Matches(@"Discovered 3 tests.*", result.StdOut);
            }

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void DiscoverProjectWithMSTestMetaPackageAndMultipleTFMsWithTests_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MSTestMetaPackageProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute("--list-tests", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@"Discovered 3 tests.*\\TestProject.dll\s\(net8.0\|[a-zA-Z][0-9]+\)\s+TestMethod1\s+TestMethod2\s+TestMethod3", result.StdOut);
                Assert.Matches(@"Discovered 2 tests.*\\TestProject.dll\s\(net9.0\|[a-zA-Z][0-9]+\)\s+TestMethod1\s+TestMethod3", result.StdOut);
            }

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void DiscoverTestProjectsWithHybridModeTestRunners_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("HybridTestRunnerTestProjects", Guid.NewGuid().ToString())
                .WithSource();

            //.WithTraceOutput() should be removed later on
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute("--list-tests", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Test application(s) that support VSTest are not supported.");
            }

            result.ExitCode.Should().Be(1);
        }
    }
}
