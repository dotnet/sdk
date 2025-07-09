﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTests : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTests(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectWithNoTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Zero tests ran")
                    .And.Contain("total: 0")
                    .And.Contain("succeeded: 0")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.ZeroTests);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectWithWithRetryFeature_ShouldSucceed(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestAppSimpleWithRetry", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("(try 2)")
                    .And.NotContain("(try 3)")
                    .And.NotContain("(try 4)")
                    .And.Contain("total: 1 (+1 retried)")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunMultipleTestProjectsWithNoTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultipleTestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Zero tests ran")
                    .And.Contain("total: 0")
                    .And.Contain("succeeded: 0")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0")
                    .And.NotContain("Expected to find --arg-from-my-target");
            }

            result.ExitCode.Should().Be(ExitCodes.ZeroTests);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectWithTests_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

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

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectWithTestsAndLaunchSettings_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithLaunchSettings", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

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

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectWithTestsAndNoLaunchSettings_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithLaunchSettings", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(
                                        TestingPlatformOptions.ConfigurationOption.Name, configuration,
                                        TestingPlatformOptions.NoLaunchProfileOption.Name);

            result.StdOut.Should()
                .Contain("FAILED to find argument from launchSettings.json");
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectWithTestsAndNoLaunchSettingsArguments_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithLaunchSettings", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(
                                        TestingPlatformOptions.ConfigurationOption.Name, configuration,
                                        TestingPlatformOptions.NoLaunchProfileArgumentsOption.Name, "true");

            result.StdOut.Should()
                .Contain("FAILED to find argument from launchSettings.json");
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunMultipleTestProjectsWithFailingTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 5")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 2");
            }

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunMultipleTestProjectsWithDifferentFailures_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithDifferentFailures", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--minimum-expected-tests 2",
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, configuration, "8"), result.StdOut);
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Failed, true, configuration, "2"), result.StdOut);
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("AnotherTestProject", TestingConstants.Failed, true, configuration, "9"), result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 4")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectsWithHybridModeTestRunners_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("HybridTestRunnerTestProjects", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain(string.Format(CliCommandStrings.CmdUnsupportedVSTestTestApplicationsDescription, "AnotherTestProject.csproj"));
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunOnEmptyFolder_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("EmptyFolder", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain(CliCommandStrings.CmdNoProjectOrSolutionFileErrorDescription);
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunOnMultipleProjectFoldersWithoutSolutionFile_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultipleTestProjectsWithoutSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain(CliCommandStrings.CmdNoProjectOrSolutionFileErrorDescription);
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunOnProjectWithSolutionFile_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectFileAndSolutionFile", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain(CliCommandStrings.CmdMultipleProjectOrSolutionFilesErrorDescription);
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunOnProjectWithClassLibrary_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithClassLibrary", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 1")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Error output: Failed to load /private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), '/System/Volumes/Preboot/Cryptexes/OS/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (no such file), '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64'))
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunningWithGlobalPropertyShouldProperlyPropagate(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithConditionOnGlobalProperty", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFramework($"{DotnetVersionHelper.GetPreviousDotnetVersion()}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(
                                        TestingPlatformOptions.ConfigurationOption.Name, configuration,
                                        CommonOptions.PropertiesOption.Name, "PROPERTY_TO_ENABLE_MTP=1");

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 2")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }
    }
}
