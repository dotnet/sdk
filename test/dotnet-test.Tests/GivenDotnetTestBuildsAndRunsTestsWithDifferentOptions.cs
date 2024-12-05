// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using dotnet.Tests;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestsWithDifferentOptions : SdkTest
    {
        private const string TestApplicationArgsSeparator = $" {CliConstants.ParametersSeparator} ";
        private const string TestApplicationArgsPattern = @".*(Test application arguments).*";
        private const string MSBuildArgsPattern = @".*(MSBuild command line arguments).*";

        public GivenDotnetTestBuildsAndRunsTestsWithDifferentOptions(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunSpecificCSProjWithFailingTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string testProjectPath = "TestProject\\TestProject.csproj";
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var testAppArgs = Regex.Matches(result.StdOut, TestApplicationArgsPattern);
            Assert.Contains($"{TestingPlatformOptions.ProjectOption.Name} \"{testInstance.TestRoot}\\{testProjectPath}\"", testAppArgs.FirstOrDefault().Value.Split(TestApplicationArgsSeparator)[0]);

            result.ExitCode.Should().Be(1);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunSpecificNonExistentCSProj_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, @"TestProject\TestProject1.csproj",
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(1);
            result.StdOut.Contains("MSBUILD : error MSB1009: Project file does not exist.");
        }


        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithNoBuildOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}/bin").Directory;
            var binDirectoryLastWriteTime = binDirectory.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.NoBuildOption.Name,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory.LastWriteTime);

            var testAppArgs = Regex.Matches(result.StdOut, TestApplicationArgsPattern);
            Assert.Contains(TestingPlatformOptions.NoBuildOption.Name, testAppArgs.FirstOrDefault().Value.Split(TestApplicationArgsSeparator)[0]);

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithNoRestoreOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}/bin").Directory;
            var binDirectoryLastWriteTime = binDirectory.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.NoRestoreOption.Name,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory.LastWriteTime);

            var testAppArgs = Regex.Matches(result.StdOut, TestApplicationArgsPattern);
            Assert.Contains(TestingPlatformOptions.NoRestoreOption.Name, testAppArgs.FirstOrDefault().Value.Split(TestApplicationArgsSeparator)[0]);

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithConfigurationOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var testAppArgs = Regex.Matches(result.StdOut, TestApplicationArgsPattern);
            Assert.Contains($"{TestingPlatformOptions.ConfigurationOption.Name} {configuration}", testAppArgs.FirstOrDefault().Value.Split(TestApplicationArgsSeparator)[0]);

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithArchOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();
            string arch = "x64";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ArchitectureOption.Name, arch,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var testAppArgs = Regex.Matches(result.StdOut, TestApplicationArgsPattern);
            Assert.Contains($"{TestingPlatformOptions.ArchitectureOption.Name} {arch}", testAppArgs.FirstOrDefault().Value.Split(TestApplicationArgsSeparator)[0]);

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunSpecificCSProjWithNoBuildAndNoRestoreOptions_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}/bin").Directory;
            var binDirectoryLastWriteTime = binDirectory.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, @"TestProject.csproj",
                                            TestingPlatformOptions.NoRestoreOption.Name, TestingPlatformOptions.NoBuildOption.Name,
                                            TestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory.LastWriteTime);

            var testAppArgs = Regex.Matches(result.StdOut, TestApplicationArgsPattern);
            Assert.Contains($"{TestingPlatformOptions.NoRestoreOption.Name} {TestingPlatformOptions.NoBuildOption.Name}", testAppArgs.FirstOrDefault().Value.Split(TestApplicationArgsSeparator)[0]);

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithMSBuildExtraParamsOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();
            string msBuildParams = "-m:10";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.AdditionalMSBuildParametersOption.Name, msBuildParams,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var msbuildArgs = Regex.Matches(result.StdOut, MSBuildArgsPattern);
            Assert.Contains(msBuildParams, msbuildArgs.FirstOrDefault().Value);

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithBinLogOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .WithTraceOutput()
                                    .Execute("-bl", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var msbuildArgs = Regex.Matches(result.StdOut, MSBuildArgsPattern);
            Assert.Contains("-bl", msbuildArgs.FirstOrDefault().Value);

            Assert.True(File.Exists(string.Format("{0}\\{1}", testInstance.TestRoot, "msbuild.binlog")));

            result.ExitCode.Should().Be(0);
        }
    }
}
