// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    [TestClass]
    public class GivenDotnetTestBuildsAndDiscoversTests : SdkTest
    {
        public GivenDotnetTestBuildsAndDiscoversTests()
        {
        }

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void DiscoverTestProjectWithNoTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--list-tests", "-c", configuration);

            if (!SdkTestContext.IsLocalized())
            {
                Assert.MatchesRegex(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 0 tests"), result.StdOut);

                result.StdOut
                    .Should().Contain("Discovered 0 tests.");
            }

            result.ExitCode.Should().Be(ExitCodes.ZeroTests);
        }

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void DiscoverMultipleTestProjectsWithNoTests_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("MultipleTestProjectSolution", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--list-tests", "-c", configuration);

            if (!SdkTestContext.IsLocalized())
            {
                Assert.MatchesRegex(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 0 tests"), result.StdOut);
                Assert.MatchesRegex(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", true, configuration, "Discovered 0 tests"), result.StdOut);
                Assert.MatchesRegex(RegexPatternHelper.GenerateProjectRegexPattern("AnotherTestProject", true, configuration, "Discovered 0 tests"), result.StdOut);
                Assert.MatchesRegex(@"Discovered 0 tests.*", result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.ZeroTests);
        }

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void DiscoverTestProjectWithTests_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithDiscoveredTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--list-tests", "-c", configuration);

            if (!SdkTestContext.IsLocalized())
            {
                Assert.MatchesRegex(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 1 tests", ["Test0"]), result.StdOut);
                Assert.MatchesRegex(@"Discovered 1 tests.*", result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void DiscoverMultipleTestProjectsWithTests_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithDiscoveredTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--list-tests", "-c", configuration);

            if (!SdkTestContext.IsLocalized())
            {
                Assert.MatchesRegex(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 2 tests", ["Test0", "Test2"]), result.StdOut);
                Assert.MatchesRegex(RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", true, configuration, "Discovered 1 tests", ["Test1"]), result.StdOut);
                Assert.MatchesRegex(@"Discovered 3 tests.*", result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void DiscoverTestProjectWithTestsInJsonFormat_EmitsMachineReadableJson(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithDiscoveredTests", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--list-tests", "json", "-c", configuration);

            result.ExitCode.Should().Be(ExitCodes.Success);

            // The discovery JSON is emitted as a single object. Extract it from stdout (which may also
            // contain restore/build output) and assert the versioned, container-grouped shape.
            string stdout = result.StdOut!;
            int start = stdout.IndexOf('{');
            int end = stdout.LastIndexOf('}');
            start.Should().BeGreaterThanOrEqualTo(0, $"expected a JSON document in the output. Full output:{Environment.NewLine}{stdout}");
            end.Should().BeGreaterThan(start);

            using var document = System.Text.Json.JsonDocument.Parse(stdout.Substring(start, end - start + 1));
            System.Text.Json.JsonElement root = document.RootElement;

            root.GetProperty("version").GetString().Should().Be("1.0");

            System.Text.Json.JsonElement containers = root.GetProperty("testContainers");
            containers.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

            bool foundTest0 = containers.EnumerateArray()
                .SelectMany(c => c.GetProperty("tests").EnumerateArray())
                .Any(t => t.GetProperty("uid").GetString() == "Test0");

            foundTest0.Should().BeTrue("the discovered test 'Test0' should appear in the JSON output.");
        }

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void DiscoverProjectWithMSTestMetaPackageAndMultipleTFMsWithTests_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("MSTestMetaPackageProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--list-tests", "-c", configuration);

            if (!SdkTestContext.IsLocalized())
            {
                Assert.MatchesRegex(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", false, configuration, "Discovered 3 tests", ["TestMethod1", "TestMethod2", "TestMethod3"]), result.StdOut);
                Assert.MatchesRegex(RegexPatternHelper.GenerateProjectRegexPattern("TestProject", true, configuration, "Discovered 2 tests", ["TestMethod1", "TestMethod3"]), result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void DiscoverTestProjectsWithHybridModeTestRunners_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("HybridTestRunnerTestProjects", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--list-tests", "-c", configuration);

            if (!SdkTestContext.IsLocalized())
            {
                result.StdErr.Should().Contain(string.Format(CliCommandStrings.CmdUnsupportedVSTestTestApplicationsDescription, "AnotherTestProject.csproj"));
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [DataRow(TestingConstants.Debug)]
        [DataRow(TestingConstants.Release)]
        [TestMethod]
        public void DiscoverTestProjectWithCustomRunArgumentsAndTestEscaping(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestAppPrintingCommandLineArguments", Guid.NewGuid().ToString())
                .WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--list-tests",
                                             "-c", configuration,
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
