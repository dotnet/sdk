﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using dotnet.Tests;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestBasedOnGlobbingFilter : SdkTest
    {
        private const string TestApplicationArgsPattern = @".*(Test application arguments).*";

        public GivenDotnetTestBuildsAndRunsTestBasedOnGlobbingFilter(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectWithFilterOfDll_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}/bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.TestModulesFilterOption.Name, "**/bin/**/Debug/net8.0/TestProject.dll",
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory?.LastWriteTime);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 2")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectWithFilterOfDllWithRootDirectory_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.TestModulesFilterOption.Name, "**/bin/**/Debug/net8.0/TestProject.dll",
                                    TestingPlatformOptions.TestModulesRootDirectoryOption.Name, testInstance.TestRoot,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);


            var testAppArgs = Regex.Matches(result.StdOut!, TestApplicationArgsPattern);
            Assert.Contains($"exec {testInstance.TestRoot}\\bin\\Debug\\net8.0\\TestProject.dll", testAppArgs.FirstOrDefault()?.Value);

            if (!TestContext.IsLocalized())
            {
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
