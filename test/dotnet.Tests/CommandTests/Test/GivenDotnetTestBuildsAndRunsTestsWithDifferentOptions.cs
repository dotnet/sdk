// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

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
        public void RunWithProjectPathWithFailingTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testProjectPath = $"TestProject{Path.DirectorySeparatorChar}TestProject.csproj";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "exec", addVersionAndArchPattern: false));

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunWithSolutionPathWithFailingTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testSolutionPath = "MultiTestProjectSolutionWithTests.sln";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, configuration), result.StdOut);
            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, true, configuration), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunWithSolutionFilterPathWithFailingTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testSolutionPath = "TestProjects.slnf";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, configuration), result.StdOut);
            Assert.DoesNotMatch(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, true, configuration), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunWithSolutionFilterPathInOtherDirectory_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory($"{testInstance.Path}{Path.DirectorySeparatorChar}SolutionFilter")
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, configuration), result.StdOut);
            Assert.DoesNotMatch(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, true, configuration), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithInvalidProjectExtension_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string invalidProjectPath = $"TestProject{Path.DirectorySeparatorChar}TestProject.txt";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, invalidProjectPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.StdOut.Should().Contain(string.Format(CliCommandStrings.CmdInvalidProjectFileExtensionErrorDescription, invalidProjectPath));

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunWithDirectoryAsProjectOption_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string projectPath = $"TestProject";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, projectPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Validate that only TestProject ran
            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, configuration), result.StdOut);
            Assert.DoesNotMatch(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, true, configuration), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithInvalidSolutionExtension_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string invalidSolutionPath = "TestProjects.txt";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, invalidSolutionPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.StdOut.Should().Contain(string.Format(CliCommandStrings.CmdInvalidSolutionFileExtensionErrorDescription, invalidSolutionPath));

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithBothProjectAndSolutionOptions_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testProjectPath = $"TestProject{Path.DirectorySeparatorChar}TestProject.csproj";
            string testSolutionPath = "MultiTestProjectSolutionWithTests.sln";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                             TestingPlatformOptions.SolutionOption.Name, testSolutionPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.StdErr.Should().Contain(CliCommandStrings.CmdMultipleBuildPathOptionsErrorDescription);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithNonExistentProjectPath_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string testProjectPath = $"TestProject{Path.DirectorySeparatorChar}OtherTestProject.csproj";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ProjectOption.Name, testProjectPath,
                                    TestingPlatformOptions.ConfigurationOption.Name, configuration);

            string fullProjectPath = $"{testInstance.TestRoot}{Path.DirectorySeparatorChar}{testProjectPath}";
            result.StdOut.Should().Contain(string.Format(CliCommandStrings.CmdNonExistentFileErrorDescription, fullProjectPath));

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithNonExistentSolutionPath_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string solutionPath = "Solution.sln";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.SolutionOption.Name, solutionPath,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            string fullSolutionPath = $"{testInstance.TestRoot}{Path.DirectorySeparatorChar}{solutionPath}";
            result.StdOut.Should().Contain(string.Format(CliCommandStrings.CmdNonExistentFileErrorDescription, fullSolutionPath));

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunTestProjectSolutionWithArchOption_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            var arch = RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86";
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CommonOptions.ArchitectureOption.Name, arch,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            string runtime = CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(string.Empty, arch);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Passed, true, configuration, runtime: runtime), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunTestProjectSolutionWithRuntimeOption_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            string runtime = CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(string.Empty, "x64");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CommonOptions.RuntimeOptionName, runtime,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Passed, true, configuration, runtime: runtime), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunTestProjectSolutionWithArchAndRuntimeOptions_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            var arch = RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86";
            string runtime = CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(string.Empty, arch);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CommonOptions.RuntimeOptionName, runtime,
                                            CommonOptions.ArchitectureOption.Name, arch,
                                            TestingPlatformOptions.ConfigurationOption.Name, configuration);

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunTestProjectSolutionWithOSOption_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            string runtime = CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(string.Empty, string.Empty);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CommonOptions.OperatingSystemOption.Name, runtime.Split('-')[0],
                                            TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Passed, true, configuration, runtime: runtime), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunTestProjectSolutionWithArchAndOSOptions_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            var arch = RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86";
            string runtime = CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(string.Empty, arch);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CommonOptions.OperatingSystemOption.Name, runtime.Split('-')[0],
                                            CommonOptions.ArchitectureOption.Name, arch,
                                            TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Passed, true, configuration, runtime: runtime), result.StdOut);

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunSpecificCSProjRunsWithNoBuildAndNoRestoreOptions_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            new BuildCommand(testInstance)
                .Execute($"-p:Configuration={configuration}")
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}{Path.DirectorySeparatorChar}bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
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
        public void RunTestProjectSolutionWithBinLogOption_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-bl", TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.True(File.Exists(string.Format("{0}{1}{2}", testInstance.TestRoot, Path.DirectorySeparatorChar, CliConstants.BinLogFileName)));

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunSpecificCSProjRunsWithMSBuildArgs_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
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
        public void RunOnSolutionWithMSBuildArgs_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
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

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunTestProjectSolutionWithFrameworkOption_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.FrameworkOption.Name, ToolsetInfo.CurrentTargetFramework,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Passed, true, configuration), result.StdOut);

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
        public void RunMultiTFMsProjectSolutionWithPreviousFramework_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString()).WithSource();

            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");

            // NOTE:
            // TestProject is targeting both CurrentTargetFramework and Previous.
            // OtherTestProject is targeting CurrentTargetFramework only.
            // We invoke dotnet test with -f Previous.
            // Restore then fails for OtherTestProject, and we run nothing.
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.FrameworkOption.Name, DotnetVersionHelper.GetPreviousDotnetVersion(),
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Output looks similar to the following
            /*
                error NETSDK1005: Assets file 'path\to\OtherTestProject\obj\project.assets.json' doesn't have a target for 'net9.0'. Ensure that restore has run and that you have included 'net9.0' in the TargetFrameworks for your project.
                Get projects properties with MSBuild didn't execute properly with exit code: 1.

                Test run summary: Zero tests ran
                  total: 0
                  failed: 0
                  succeeded: 0
                  skipped: 0
                  duration: 33s 867ms
            */
            if (!TestContext.IsLocalized())
            {
                result.StdOut
                 .Should().Contain("Test run summary: Zero tests ran")
                 .And.Contain("total: 0")
                 .And.Contain("succeeded: 0")
                 .And.Contain("failed: 0")
                 .And.Contain("skipped: 0")
                 .And.Contain("NETSDK1005");
            }

            // This should fail because OtherTestProject is not built with the previous .NET version
            // Therefore, the build error will prevent the tests from running
            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }


        //  https://github.com/dotnet/sdk/issues/49665
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunMultiTFMsProjectSolutionWithCurrentFramework_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString()).WithSource();

            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");

            // NOTE:
            // TestProject is targeting both CurrentTargetFramework and Previous.
            // OtherTestProject is targeting CurrentTargetFramework only.
            // We invoke dotnet test with -f Current.
            // TestProject has 1 passing test, 1 skipped test, and 4 failing.
            // OtherTestProject has 1 passing test and 1 skipped.
            // In total, 2 passing, 2 skipped, 4 failing
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.FrameworkOption.Name, ToolsetInfo.CurrentTargetFramework,
                                             TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, true, configuration), result.StdOut);
                Assert.Matches(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, true, configuration), result.StdOut);
                Assert.DoesNotMatch(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, false, configuration), result.StdOut);

                result.StdOut
                 .Should().Contain("Test run summary: Failed!")
                 .And.Contain("total: 8")
                 .And.Contain("succeeded: 2")
                 .And.Contain("failed: 4")
                 .And.Contain("skipped: 2");
            }

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithTraceFileLogging_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string traceFile = "logs.txt";
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnvironmentVariable(CliConstants.TestTraceLoggingEnvVar, traceFile)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.True(File.Exists(Path.Combine(testInstance.Path, traceFile)), "Trace file should exist after test execution.");

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunWithTraceFileLoggingAndNonExistingDirectory_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();

            string traceFile = $"directory_{configuration}{Path.DirectorySeparatorChar}logs.txt";
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnvironmentVariable(CliConstants.TestTraceLoggingEnvVar, traceFile)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            Assert.True(File.Exists(Path.Combine(testInstance.Path, traceFile)), "Trace file should exist after test execution.");

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }
    }
}
