// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestsWithDifferentOptions : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestsWithDifferentOptions(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithProjectPathWithFailingTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testProjectPath = $"TestProject{Path.DirectorySeparatorChar}TestProject.csproj";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "exec", addVersionAndArchPattern: false));

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithSolutionPathWithFailingTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testSolutionPath = "MultiTestProjectSolutionWithTests.sln";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, configuration), result.StdOut);
            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, true, configuration), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithSolutionFilterPathWithFailingTests_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testSolutionPath = "TestProjects.slnf";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, configuration), result.StdOut);
            Assert.DoesNotMatch(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, true, configuration), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithSolutionFilterPathInOtherDirectory_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory($"{testInstance.Path}{Path.DirectorySeparatorChar}SolutionFilter")
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, configuration), result.StdOut);
            Assert.DoesNotMatch(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, true, configuration), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithInvalidProjectExtension_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string invalidProjectPath = $"TestProject{Path.DirectorySeparatorChar}TestProject.txt";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, invalidProjectPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.StdOut.Should().Contain(string.Format(Tools.Test.LocalizableStrings.CmdInvalidProjectFileExtensionErrorDescription, invalidProjectPath));

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithInvalidSolutionExtension_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string invalidSolutionPath = "TestProjects.txt";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, invalidSolutionPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.StdOut.Should().Contain(string.Format(Tools.Test.LocalizableStrings.CmdInvalidSolutionFileExtensionErrorDescription, invalidSolutionPath));

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithBothProjectAndSolutionAndDirectoryOptions_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testProjectPath = $"TestProject{Path.DirectorySeparatorChar}TestProject.csproj";
            string testSolutionPath = "MultiTestProjectSolutionWithTests.sln";
            string testDirectoryPath = "TestProjects";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                             TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                             TestingPlatformOptions.DirectoryOption.Name, testDirectoryPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.StdOut.Should().Contain(Tools.Test.LocalizableStrings.CmdMultipleBuildPathOptionsErrorDescription);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithBothProjectAndSolutionOptions_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testProjectPath = $"TestProject{Path.DirectorySeparatorChar}TestProject.csproj";
            string testSolutionPath = "MultiTestProjectSolutionWithTests.sln";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                             TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.StdOut.Should().Contain(Tools.Test.LocalizableStrings.CmdMultipleBuildPathOptionsErrorDescription);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithNonExistentProjectPath_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testProjectPath = $"TestProject{Path.DirectorySeparatorChar}OtherTestProject.csproj";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            string fullProjectPath = $"{testInstance.TestRoot}{Path.DirectorySeparatorChar}{testProjectPath}";
            result.StdOut.Should().Contain(string.Format(Tools.Test.LocalizableStrings.CmdNonExistentFileErrorDescription, fullProjectPath));

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithNonExistentSolutionPath_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string solutionPath = "Solution.sln";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, solutionPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            string fullSolutionPath = $"{testInstance.TestRoot}{Path.DirectorySeparatorChar}{solutionPath}";
            result.StdOut.Should().Contain(string.Format(Tools.Test.LocalizableStrings.CmdNonExistentFileErrorDescription, fullSolutionPath));

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithNonExistentDirectoryPath_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string directoryPath = "Directory";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.DirectoryOption.Name, directoryPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.StdOut.Should().Contain(string.Format(Tools.Test.LocalizableStrings.CmdNonExistentDirectoryErrorDescription, directoryPath));
            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithArchOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ArchitectureOption.Name, "x64",
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            string runtime = CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(string.Empty, "x64");
            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Passed, true, configuration, runtime: runtime), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunSpecificCSProjRunsWithNoBuildAndNoRestoreOptions_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            new BuildCommand(testInstance)
                .Execute($"-p:Configuration={configuration}")
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}{Path.DirectorySeparatorChar}bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, "TestProject.csproj",
                                            TestingPlatformOptions.ConfigurationOption.Name, configuration,
                                            CommonOptions.NoRestoreOption.Name,
                                            TestingPlatformOptions.NoBuildOption.Name);

            // Assert that the bin folder hasn't been modified
            Assert.Equal(binDirectoryLastWriteTime, binDirectory?.LastWriteTime);

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithBinLogOption_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute("-bl", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.True(File.Exists(string.Format("{0}{1}{2}", testInstance.TestRoot, Path.DirectorySeparatorChar, CliConstants.BinLogFileName)));

            result.ExitCode.Should().Be(0);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunSpecificCSProjRunsWithMSBuildArgs_ShouldReturnZeroAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, "TestProject.csproj",
                                            TestingPlatformOptions.ConfigurationOption.Name, configuration,
                                            "--property:WarningLevel=2", $"--property:Configuration={configuration}");

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

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunOnSolutionWithMSBuildArgs_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration,
                                            "--property:WarningLevel=2");

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                  .Should().Contain("Test run summary: Failed!")
                  .And.Contain("total: 5")
                  .And.Contain("succeeded: 2")
                  .And.Contain("failed: 1")
                  .And.Contain("skipped: 2");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }
    }
}
