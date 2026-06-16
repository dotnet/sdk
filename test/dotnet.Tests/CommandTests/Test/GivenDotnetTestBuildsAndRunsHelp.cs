// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Utils;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsHelp : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsHelp(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunHelpOnTestProject_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolutionWithTestsAndArtifacts", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CliConstants.HelpOptionKey, MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@"Extension Options:\s+--[\s\S]*", result.StdOut);
                Assert.Matches(@"Options:\s+--[\s\S]*", result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Error output: Failed to load /private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), '/System/Volumes/Preboot/Cryptexes/OS/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (no such file), '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64'))
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunHelpOnMultipleTestProjects_ShouldReturnExitCodeSuccess(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("ProjectSolutionForMultipleTFMs", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFramework($"{DotnetVersionHelper.GetPreviousDotnetVersion()}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CliConstants.HelpOptionKey, MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                Assert.Matches(@"Extension Options:\s+--[\s\S]*", result.StdOut);
                Assert.Matches(@"Options:\s+--[\s\S]*", result.StdOut);

                string directorySeparator = PathUtility.GetDirectorySeparatorChar();
                string otherTestProjectPattern = @$"Unavailable extension options:\s+.*{directorySeparator}{ToolsetInfo.CurrentTargetFramework}{directorySeparator}OtherTestProject\.dll.*\s+(--report-trx\s+--report-trx-filename|--report-trx-filename\s+--report-trx)";

                Assert.Matches(otherTestProjectPattern, result.StdOut);
            }

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void RunHelpCommand_ShouldNotShowDuplicateOptions(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectSolutionWithTestsAndArtifacts", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CliConstants.HelpOptionKey, MicrosoftTestingPlatformOptions.ConfigurationOption.Name, configuration);

            // Parse the help output to extract option names
            var helpOutput = result.StdOut;

            // Check for specific options we care about
            string outputOptionName = MicrosoftTestingPlatformOptions.OutputOption.Name; // --output
            string noAnsiOptionName = MicrosoftTestingPlatformOptions.NoAnsiOption.Name; // --no-ansi

            // Count occurrences of each option in the help output
            int outputOptionCount = CountOptionOccurrences(helpOutput!, outputOptionName);
            int noAnsiOptionCount = CountOptionOccurrences(helpOutput!, noAnsiOptionName);

            // Assert that each option appears only once
            outputOptionCount.Should().Be(1, $"Option '{outputOptionName}' should not appear more than once in help output");
            noAnsiOptionCount.Should().Be(1, $"Option '{noAnsiOptionName}' should not appear more than once in help output");

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        private static int CountOptionOccurrences(string helpOutput, string optionName)
        {
            // Split by lines and look for lines that start with the option (accounting for indentation)
            var lines = helpOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int count = 0;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                // Look for lines that start with the option name (e.g., "--output" or "--no-ansi")
                if (trimmedLine.StartsWith(optionName, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }


    }
}
