// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Common;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestsWithArtifacts : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestsWithArtifacts(ITestOutputHelper log) : base(log)
        {
        }


        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectWithFailingTestsAndFileArtifacts_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolutionWithTestsAndArtifacts", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@".*Test6.*testNodeFile.txt", result.StdOut);

                result.StdOut
                    .Should().Contain("In process file artifacts")
                    .And.Contain("file.txt")
                    .And.Contain("sessionFile.txt");

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 6")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 4")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectWithCodeCoverage_ShouldReturnOneAsExitCode(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolutionWithCodeCoverage", Guid.NewGuid().ToString()).WithSource();

            // Read MSTestVersion from Versions.props and update the .csproj file
            // Search for Versions.props file from the current directory up to the root

            Console.WriteLine($"CurrentDirectory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"BaseDirectory : {AppContext.BaseDirectory}");
            Console.WriteLine($"testInstance.Path : {testInstance.Path}");
            Console.WriteLine($"TestContext.Current.TestExecutionDirectory : {TestContext.Current.TestExecutionDirectory}");
            string? versionsPropsPath = PathUtility.FindFileInParentDirectories(TestContext.Current.TestExecutionDirectory, $"eng{Path.DirectorySeparatorChar}Versions.props") ?? throw new FileNotFoundException("Versions.props file not found.");
            string msTestVersion = testInstance.ReadMSTestVersionFromProps(versionsPropsPath);
            testInstance.UpdateProjectFileWithMSTestVersion(Path.Combine($@"{testInstance.Path}{PathUtility.GetDirectorySeparatorChar()}TestProject", "TestProject.csproj"), msTestVersion);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnableTestingPlatform()
                                    .Execute("--coverage", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                string pattern = $@"In\sprocess\sfile\sartifacts\sproduced:\s+.*{PathUtility.GetDirectorySeparatorChar()}TestResults{PathUtility.GetDirectorySeparatorChar()}.*\.coverage";

                Assert.Matches(pattern, result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 2")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }
    }
}
