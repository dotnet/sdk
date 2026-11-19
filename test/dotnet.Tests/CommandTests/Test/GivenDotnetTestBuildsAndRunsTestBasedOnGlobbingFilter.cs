// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    [TestClass]
    public class GivenDotnetTestBuildsAndRunsTestBasedOnGlobbingFilter : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestBasedOnGlobbingFilter()
        {
        }

        [TestMethod]
        public void RunTestProjectWithFilterOfDll_ShouldReturnExitCodeSuccess()
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}{Path.DirectorySeparatorChar}bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--test-modules", $"**/bin/**/Debug/{ToolsetInfo.CurrentTargetFramework}/TestProject.dll".Replace('/', Path.DirectorySeparatorChar));

            // Assert that the bin folder hasn't been modified
            Assert.AreEqual(binDirectoryLastWriteTime, binDirectory?.LastWriteTime);


            if (!SdkTestContext.IsLocalized())
            {
                Assert.MatchesRegex(GenerateModuleResultPattern("TestProject", TestingConstants.Passed), result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 2")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [TestMethod]
        public void RunTestProjectsWithFilterOfDll_ShouldReturnExitCodeGenericFailure()
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
                .WithSource();

            new BuildCommand(testInstance, "TestProject")
                .Execute()
                .Should().Pass();

            new BuildCommand(testInstance, "OtherTestProject")
              .Execute()
              .Should().Pass();

            var binDirectory = new FileInfo($"{testInstance.Path}{Path.DirectorySeparatorChar}bin").Directory;
            var binDirectoryLastWriteTime = binDirectory?.LastWriteTime;

            string filterExpression = $"**/bin/**/Debug/{ToolsetInfo.CurrentTargetFramework}/*TestProject.dll".Replace('/', Path.DirectorySeparatorChar);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--test-modules", filterExpression);

            // Assert that the bin folder hasn't been modified
            Assert.AreEqual(binDirectoryLastWriteTime, binDirectory?.LastWriteTime);

            if (!SdkTestContext.IsLocalized())
            {
                Assert.MatchesRegex(GenerateModuleResultPattern("TestProject", TestingConstants.Failed), result.StdOut);
                Assert.MatchesRegex(GenerateModuleResultPattern("OtherTestProject", TestingConstants.Passed), result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 5")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 2");
            }

            // TestProject produces 1 passed, 1 failed, and 1 skipped.
            // OtherTestProject produces 1 passed and 1 skipped.
            // We got 2 exit codes. Success and AtLeastOneTestFailed. So, we aggregate the final exit code to AtLeastOneTestFailed.
            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }


        [TestMethod]
        public void RunTestProjectWithFilterOfDllWithRootDirectory_ShouldReturnExitCodeSuccess()
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests", Guid.NewGuid().ToString())
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithTraceOutput()
                                    .Execute("--test-modules", $"**/bin/**/Debug/{ToolsetInfo.CurrentTargetFramework}/TestProject.dll".Replace('/', Path.DirectorySeparatorChar),
                                    "--root-directory", testInstance.TestRoot);

            if (!SdkTestContext.IsLocalized())
            {
                Assert.MatchesRegex(GenerateModuleResultPattern("TestProject", TestingConstants.Passed), result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Passed!")
                    .And.Contain("total: 2")
                    .And.Contain("succeeded: 1")
                    .And.Contain("failed: 0")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        private static string GenerateModuleResultPattern(string projectName, string result)
        {
            // Only newer platform peers can supply target metadata for module-only runs.
            string framework = Regex.Escape(ToolsetInfo.CurrentTargetFramework);
            const string architecture = "[a-z0-9]+";
            return RegexPatternHelper.GenerateProjectRegexPattern(
                projectName,
                useCurrentVersion: true,
                TestingConstants.Debug,
                prefix: string.Empty,
                suffix: [$@"\((?:{framework}\|{architecture}|{architecture}, runtime: {framework})\)", result],
                addVersionAndArchPattern: false);
        }
    }
}
