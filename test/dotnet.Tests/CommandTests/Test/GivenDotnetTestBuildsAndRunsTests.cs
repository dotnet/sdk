// Licensed to the .NET Foundation under one or more agreements.
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
        public void RunTestProjectWithNoTests_ShouldReturnExitCodeZeroTests(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

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
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

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
        public void RunMultipleTestProjectsWithNoTests_ShouldReturnExitCodeZeroTests(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultipleTestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

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
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

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

        [Theory, CombinatorialData]
        public void RunTestProjectWithTestsAndLaunchSettings_ShouldReturnExitCodeSuccess(
            [CombinatorialValues(TestingConstants.Debug, TestingConstants.Release)] string configuration, bool runJson)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithLaunchSettings", Guid.NewGuid().ToString())
                .WithSource();

            if (runJson)
            {
                File.Move(Path.Join(testInstance.Path, "Properties", "launchSettings.json"), Path.Join(testInstance.Path, "TestProjectWithLaunchSettings.run.json"));
            }

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Using launch settings from")
                    .And.Contain(runJson ? "TestProjectWithLaunchSettings.run.json..." : $"Properties{Path.DirectorySeparatorChar}launchSettings.json...")
                    .And.Contain("Test run summary: Passed!")
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
                                        MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration,
                                        MicrosoftTestingPlatformOptions.NoLaunchProfileOption.Name);

            result.StdOut.Should()
                .Contain("FAILED to find argument from launchSettings.json")
                .And.NotContain("Using launch settings from");
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
                                        MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration,
                                        MicrosoftTestingPlatformOptions.NoLaunchProfileArgumentsOption.Name, "true");

            result.StdOut.Should()
                .Contain("Using launch settings from")
                .And.Contain($"Properties{Path.DirectorySeparatorChar}launchSettings.json...")
                .And.Contain("FAILED to find argument from launchSettings.json");
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunMultipleTestProjectsWithFailingTests_ShouldReturnExitCodeAtLeastOneTestFailed(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

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

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunMultipleTestProjectsWithDifferentFailures_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithDifferentFailures", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--minimum-expected-tests 2",
                                    MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.ZeroTestsRan, true, configuration, "8"), result.StdOut);
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
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain(string.Format(CliCommandStrings.CmdUnsupportedVSTestTestApplicationsDescription, "AnotherTestProject.csproj"));
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectsWithClassLibraryHavingIsTestProjectAndMTPProject_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("ClassLibraryWithIsTestProjectAndOtherTestProjects", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

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
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

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
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

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
                                    .Execute(MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain(CliCommandStrings.CmdMultipleProjectOrSolutionFilesErrorDescription);
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [Theory]
        [CombinatorialData]
        public void RunOnProjectWithClassLibrary_ShouldReturnExitCodeSuccess(
            [CombinatorialValues(TestingConstants.Debug, TestingConstants.Release)] string configuration,
            [CombinatorialValues("TestProjectWithClassLibrary", "TestProjectWithClassLibraryDifferentTFMs")] string assetName,
            bool useFrameworkOption)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset(assetName, Guid.NewGuid().ToString())
                .WithSource();

            string[] args = useFrameworkOption
                ? new[] { MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration, MicrosoftTestingPlatformOptions.FrameworkOption.Name, ToolsetInfo.CurrentTargetFramework }
                : new[] { MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration };

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(args);

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
                                        MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration,
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

        [Fact]
        public void RunMTPProjectWithUseAppHostFalse_ShouldWork()
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectMTPWithUseAppHostFalse", Guid.NewGuid().ToString())
                .WithSource();

            // Run test with UseAppHost=false
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            // Verify the test runs successfully with UseAppHost=false
            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [Theory]
        [InlineData("3", ExitCodes.Success)]
        [InlineData("5", ExitCodes.Success)]
        [InlineData("7", ExitCodes.Success)]
        [InlineData("10", ExitCodes.Success)]
        [InlineData("11", ExitCodes.MinimumExpectedTestsPolicyViolation)]
        public void RunMTPSolutionWithMinimumExpectedTests(string value, int expectedExitCode)
        {
            // The solution has two test projects. Each reports 5 tests. So, total 10 tests.
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolutionTestingMinimumExpectedTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(MicrosoftTestingPlatformOptions.MinimumExpectedTestsOption.Name, value);

            result.ExitCode.Should().Be(expectedExitCode);
        }

        [Fact]
        public void RunMTPProjectThatCrashesWithExitCodeZero_ShouldFail()
        {
            // The solution has two test projects. Each reports 5 tests. So, total 10 tests.
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectMTPCrash", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            result.ExitCode.Should().NotBe(ExitCodes.Success);
            if (!TestContext.IsLocalized())
            {
                /*
                The following exception occurred when running the test module with RunCommand 'C:\Users\ygerges\Desktop\sdk\artifacts\tmp\Debug\testing\19cefafa-91c4---0D0799BD\TestProject1\bin\Debug\net10.0\TestProject1.exe' and RunArguments ' ':
                System.InvalidOperationException: A test session start event was received without a corresponding test session end.
                   at Microsoft.DotNet.Cli.Commands.Test.TestApplication.RunAsync() in C:\Users\ygerges\Desktop\sdk\src\Cli\dotnet\Commands\Test\MTP\TestApplication.cs:line 55
                   at Microsoft.DotNet.Cli.Commands.Test.TestApplicationActionQueue.Read(BuildOptions buildOptions, TestOptions testOptions, TerminalTestReporter output, Action`1 onHelpRequested) in C:\Users\ygerges\Desktop\sdk\src\Cli\dotnet\Commands\Test\MTP\TestApplicationActionQueue.cs:line 68
                 */
                result.StdErr.Should().MatchRegex("""
                    The following exception occurred when running the test module with RunCommand '.+?TestProject1(\..+?)?' and RunArguments ' ':
                    """);

                result.StdErr.Should().Contain("System.InvalidOperationException: A test session start event was received without a corresponding test session end.");

                // TODO: It's much better to introduce a new kind of "summary" indicating
                // that the test app exited with zero exit code before sending test session end event
                result.StdOut.Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 1")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 0");

                result.StdOut.Contains("Test run completed with non-success exit code: 1 (see: https://aka.ms/testingplatform/exitcodes)");
            }
        }

        [Theory]
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        public void RunTestProjectWithEnvVariable(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectShowingEnvVariable", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(
                                        MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration,
                                        CommonOptions.EnvOption.Name, "DUMMY_TEST_ENV_VAR=ENV_VAR_CMD_LINE");

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Using launch settings from")
                    .And.Contain($"Properties{Path.DirectorySeparatorChar}launchSettings.json...")
                    .And.Contain("Test run summary: Failed!")
                    .And.Contain("total: 1")
                    .And.Contain("succeeded: 0")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 0")
                    .And.Contain("DUMMY_TEST_ENV_VAR is 'ENV_VAR_CMD_LINE'");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }
    }
}
