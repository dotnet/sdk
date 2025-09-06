// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndDiscoversTests : SdkTest
    {
        public GivenDotnetTestBuildsAndDiscoversTests(ITestOutputHelper log) : base(log)
        {
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void DiscoverTestProjectWithNoTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ListTestsOption.Name, MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 0 tests"), result.StdOut);

                result.StdOut
                    .Should().Contain("Discovered 0 tests.");
            }

            result.ExitCode.Should().Be(ExitCodes.ZeroTests);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void DiscoverMultipleTestProjectsWithNoTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultipleTestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ListTestsOption.Name, MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 0 tests"), result.StdOut);
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", true, configuration, "Discovered 0 tests"), result.StdOut);
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("AnotherTestProject", true, configuration, "Discovered 0 tests"), result.StdOut);
                Assert.Matches(@"Discovered 0 tests.*", result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.ZeroTests);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void DiscoverTestProjectWithTests_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithDiscoveredTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ListTestsOption.Name, MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 1 tests", ["Test0"]), result.StdOut);
                Assert.Matches(@"Discovered 1 tests.*", result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void DiscoverMultipleTestProjectsWithTests_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithDiscoveredTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ListTestsOption.Name, MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 2 tests", ["Test0", "Test2"]), result.StdOut);
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", true, configuration, "Discovered 1 tests", ["Test1"]), result.StdOut);
                Assert.Matches(@"Discovered 3 tests.*", result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //   Error output: Failed to load /private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), 
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void DiscoverProjectWithMSTestMetaPackageAndMultipleTFMsWithTests_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MSTestMetaPackageProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ListTestsOption.Name, MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", false, configuration, "Discovered 3 tests", ["TestMethod1", "TestMethod2", "TestMethod3"]), result.StdOut);
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 2 tests", ["TestMethod1", "TestMethod3"]), result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void DiscoverTestProjectsWithHybridModeTestRunners_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("HybridTestRunnerTestProjects", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ListTestsOption.Name, MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain(string.Format(CliCommandStrings.CmdUnsupportedVSTestTestApplicationsDescription, "AnotherTestProject.csproj"));
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void DiscoverTestProjectWithCustomRunArgumentsAndTestEscaping(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestAppPrintingCommandLineArguments", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(MicrosoftTestingPlatformOptions.ListTestsOption.Name,
                                             MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration,
                                             "-p:RunArguments=--hello world \"\" world2",
                                             "Another arg with spaces",
                                             "My other arg with spaces",
                                             "Arg ending with backslash and containing spaces\\",
                                             "ArgWithoutSpacesEndingWith\\");

            result.StdOut.Should().Contain("""
                 args[0]=--hello
                  args[1]=world
                  args[2]=
                  args[3]=world2
                  args[4]=--list-tests
                  args[5]=Another arg with spaces
                  args[6]=My other arg with spaces
                  args[7]=Arg ending with backslash and containing spaces\
                  args[8]=ArgWithoutSpacesEndingWith\
                  args[9]=--server
                  args[10]=dotnettestcli
                  args[11]=--dotnet-test-pipe
                """);
        }
    }
}
