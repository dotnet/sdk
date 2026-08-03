// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Utils;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

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
        public void RunTestProjectWithFailingTestsAndFileArtifacts_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectSolutionWithTestsAndArtifacts", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration);

            if (!SdkTestContext.IsLocalized())
            {
                Assert.Matches(@".*Test6.*testNodeFile.txt", result.StdOut);

                result.StdOut
                    .Should().Contain("In process file artifacts")
                    .And.Contain("file.txt")
                    .And.Contain("sessionFile.txt");

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 7")
                    .And.Contain("succeeded: 2")
                    .And.Contain("failed: 4")
                    .And.Contain("skipped: 1");
            }

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        [InlineData(TestingConstants.Debug, false)]
        [InlineData(TestingConstants.Release, false)]
        [InlineData(TestingConstants.Debug, true)]
        [InlineData(TestingConstants.Release, true)]
        [Theory]
        public void RunTestProjectWithCodeCoverage_ShouldReturnExitCodeAtLeastOneTestFailed(string configuration, bool includeTestAssembly)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectSolutionWithCodeCoverage", Guid.NewGuid().ToString()).WithSource();

            // Read MSTestPackageVersion from Version.Details.props and update the .csproj file
            // Search for Version.Details.props file from the current directory up to the root
            string? versionsPropsPath = PathUtility.FindFileInParentDirectories(SdkTestContext.Current.TestExecutionDirectory, $"eng{Path.DirectorySeparatorChar}Version.Details.props") ?? throw new FileNotFoundException("Version.Details.props file not found.");
            string msTestVersion = testInstance.ReadMSTestPackageVersionFromProps(versionsPropsPath);
            testInstance.UpdateProjectFileWithMSTestPackageVersion(Path.Combine($@"{testInstance.Path}{PathUtility.GetDirectorySeparatorChar()}TestProject", "TestProject.csproj"), msTestVersion);

            // Explicitly configure IncludeTestAssembly so the test behavior does not depend on
            // changes to the Microsoft.CodeCoverage default value. Also explicitly enable static
            // managed instrumentation and populate <IncludeDirectories> so the
            // StaticManagedInstrumenter actually rewrites the test assembly and its dependencies
            // (it short-circuits when both <IncludeDirectories> and <AdditionalFiles> are empty).
            // This is what surfaces the historic crash described in
            // https://github.com/dotnet/sdk/issues/52029 on Linux/macOS.
            string testProjectBinDir = Path.Combine(testInstance.Path, "TestProject", "bin", configuration);
            string coverageSettingsPath = Path.Combine(testInstance.Path, "coverage.config");
            File.WriteAllText(coverageSettingsPath, $"""
                <?xml version="1.0" encoding="utf-8"?>
                <Configuration>
                    <CodeCoverage>
                        <EnableStaticManagedInstrumentation>True</EnableStaticManagedInstrumentation>
                        <EnableStaticManagedInstrumentationRestore>True</EnableStaticManagedInstrumentationRestore>
                        <IncludeTestAssembly>{includeTestAssembly}</IncludeTestAssembly>
                        <ModulePaths>
                            <IncludeDirectories>
                                <Directory Recursive="true">{testProjectBinDir}</Directory>
                            </IncludeDirectories>
                        </ModulePaths>
                    </CodeCoverage>
                </Configuration>
                """);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("--coverage", "--coverage-settings", coverageSettingsPath, "-c", configuration);

            if (!SdkTestContext.IsLocalized())
            {
                string pattern = $@"In\sprocess\sfile\sartifacts\sproduced:\s+.*{PathUtility.GetDirectorySeparatorChar()}TestResults{PathUtility.GetDirectorySeparatorChar()}.*\.coverage";

                Assert.Matches(pattern, result.StdOut);

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 6")
                    .And.Contain("succeeded: 5")
                    .And.Contain("failed: 1")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }
    }
}
