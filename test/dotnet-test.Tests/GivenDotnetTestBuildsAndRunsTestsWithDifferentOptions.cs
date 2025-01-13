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
        public void RunWithProjectPathWithFailingTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string testProjectPath = "TestProject\\TestProject.csproj";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var testAppArgs = Regex.Matches(result.StdOut!, TestApplicationArgsPattern);
            Assert.Contains($"{TestingPlatformOptions.ProjectOption.Name} \"{testInstance.TestRoot}\\{testProjectPath}\"", testAppArgs.FirstOrDefault()?.Value.Split(TestApplicationArgsSeparator)[0]);

            result.ExitCode.Should().Be(1);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunWithSolutionPathWithFailingTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string testSolutionPath = "MultiTestProjectSolutionWithTests.sln";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);


            var testAppArgs = Regex.Matches(result.StdOut!, TestApplicationArgsPattern)
                                   .Select(match => match.Value.Split(TestApplicationArgsSeparator)[0])
                                   .ToList();

            string expectedProjectPath = $"{testInstance.TestRoot}\\TestProject\\TestProject.csproj";
            string otherExpectedProjectPath = $"{testInstance.TestRoot}\\OtherTestProject\\OtherTestProject.csproj";

            bool containsExpectedPath = testAppArgs.Any(arg => arg.Contains(expectedProjectPath) || arg.Contains(otherExpectedProjectPath));

            Assert.True(containsExpectedPath,
                        $"Expected either '{expectedProjectPath}' or '{otherExpectedProjectPath}' to be present in the test application arguments.");

            result.ExitCode.Should().Be(1);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunWithInvalidProjectExtension_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string invalidProjectPath = "TestProject\\TestProject.txt";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, invalidProjectPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(1);
            result.StdOut.Should().Contain($"The provided project file has an invalid extension: {invalidProjectPath}.");
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunWithInvalidSolutionExtension_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string invalidSolutionPath = "TestProjects.txt";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, invalidSolutionPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(1);
            result.StdOut.Should().Contain($"The provided solution file has an invalid extension: {invalidSolutionPath}.");
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunWithBothProjectAndSolutionAndDirectoryOptions_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string testProjectPath = "TestProject\\TestProject.csproj";
            string testSolutionPath = "MultiTestProjectSolutionWithTests.sln";
            string testDirectoryPath = "TestProjects";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                             TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                             TestingPlatformOptions.DirectoryOption.Name, testDirectoryPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(1);
            result.StdOut?.Contains("Specify either the project, solution or directory option.");
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunWithBothProjectAndSolutionOptions_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string testProjectPath = "TestProject\\TestProject.csproj";
            string testSolutionPath = "MultiTestProjectSolutionWithTests.sln";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                             TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(1);
            result.StdOut?.Contains("Specify either the project or solution option.");
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunWithNonExistentProjectPath_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string testProjectPath = "TestProject\\OtherTestProject.csproj";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(1);
            result.StdOut?.Contains($"The provided file path does not exist: {testInstance.TestRoot}\\{testProjectPath}.");
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunWithNonExistentSolutionPath_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string solutionPath = "Solution.sln";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, solutionPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(1);
            result.StdOut.Should().Contain($"The provided file path does not exist: {testInstance.TestRoot}\\{solutionPath}.");
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunWithNonExistentDirectoryPath_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            string directoryPath = "Directory";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.DirectoryOption.Name, directoryPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(1);
            result.StdOut.Should().Contain($"The provided directory path does not exist: {testInstance.TestRoot}\\{directoryPath}.");
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithNoBuildOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            new BuildCommand(testInstance)
                .Execute($"/p:Configuration={configuration}")
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}/bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.NoBuildOption.Name,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory?.LastWriteTime);

            var testAppArgs = Regex.Matches(result.StdOut!, TestApplicationArgsPattern);
            Assert.Contains(TestingPlatformOptions.NoBuildOption.Name, testAppArgs.FirstOrDefault()?.Value.Split(TestApplicationArgsSeparator)[0]);

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithNoRestoreOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            new BuildCommand(testInstance)
                .Execute($"/p:Configuration={configuration}")
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}/bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.NoRestoreOption.Name,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory?.LastWriteTime);

            var testAppArgs = Regex.Matches(result.StdOut!, TestApplicationArgsPattern);
            Assert.Contains(TestingPlatformOptions.NoRestoreOption.Name, testAppArgs.FirstOrDefault()?.Value.Split(TestApplicationArgsSeparator)[0]);

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
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var testAppArgs = Regex.Matches(result.StdOut!, TestApplicationArgsPattern);
            Assert.Contains($"{TestingPlatformOptions.ConfigurationOption.Name} {configuration}", testAppArgs.FirstOrDefault()?.Value.Split(TestApplicationArgsSeparator)[0]);

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
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ArchitectureOption.Name, arch,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var testAppArgs = Regex.Matches(result.StdOut!, TestApplicationArgsPattern);
            Assert.Contains($"{TestingPlatformOptions.ArchitectureOption.Name} {arch}", testAppArgs.FirstOrDefault()?.Value.Split(TestApplicationArgsSeparator)[0]);

            result.ExitCode.Should().Be(0);
        }

        [InlineData(Constants.Debug)]
        [InlineData(Constants.Release)]
        [Theory]
        public void RunSpecificCSProjWithNoBuildAndNoRestoreOptions_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            new BuildCommand(testInstance)
                .Execute($"/p:Configuration={configuration}")
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}/bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, @"TestProject.csproj",
                                            TestingPlatformOptions.NoRestoreOption.Name, TestingPlatformOptions.NoBuildOption.Name,
                                            TestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory?.LastWriteTime);

            var testAppArgs = Regex.Matches(result.StdOut!, TestApplicationArgsPattern);
            Assert.Contains($"{TestingPlatformOptions.NoRestoreOption.Name} {TestingPlatformOptions.NoBuildOption.Name}", testAppArgs.FirstOrDefault()?.Value.Split(TestApplicationArgsSeparator)[0]);

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
                                    .WithTraceOutput()
                                    .Execute(TestingPlatformOptions.AdditionalMSBuildParametersOption.Name, msBuildParams,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var msbuildArgs = Regex.Matches(result.StdOut!, MSBuildArgsPattern);
            Assert.Contains(msBuildParams, msbuildArgs.FirstOrDefault()?.Value);

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
                                    .WithTraceOutput()
                                    .Execute("-bl", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            var msbuildArgs = Regex.Matches(result.StdOut!, MSBuildArgsPattern);
            Assert.Contains("-bl", msbuildArgs.FirstOrDefault()?.Value);

            Assert.True(File.Exists(string.Format("{0}\\{1}", testInstance.TestRoot, "msbuild.binlog")));

            result.ExitCode.Should().Be(0);
        }
    }
}
