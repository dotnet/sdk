// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
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
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectSolutionWithTestsAndArtifacts", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CliConstants.HelpOptionKey, "-c", configuration);

            if (!SdkTestContext.IsLocalized())
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
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("ProjectSolutionForMultipleTFMs", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFramework($"{DotnetVersionHelper.GetPreviousDotnetVersion()}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CliConstants.HelpOptionKey, "-c", configuration);

            if (!SdkTestContext.IsLocalized())
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
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectSolutionWithTestsAndArtifacts", Guid.NewGuid().ToString()).WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CliConstants.HelpOptionKey, "-c", configuration);

            // Parse the help output to extract option names
            var helpOutput = result.StdOut;

            // Count occurrences of each option in the help output
            int outputOptionCount = CountOptionOccurrences(helpOutput!, "--output");
            int noAnsiOptionCount = CountOptionOccurrences(helpOutput!, "--no-ansi");

            // Assert that each option appears only once
            outputOptionCount.Should().Be(1, $"Option '--output' should not appear more than once in help output");
            noAnsiOptionCount.Should().Be(1, $"Option '--no-ansi' should not appear more than once in help output");

            result.ExitCode.Should().Be(ExitCodes.Success);
        }

        [InlineData(TestingConstants.Debug, "--help")]
        [InlineData(TestingConstants.Debug, "-?")]
        [InlineData(TestingConstants.Debug, "--list-tests")]
        [InlineData(TestingConstants.Release, "--help")]
        [InlineData(TestingConstants.Release, "-?")]
        [InlineData(TestingConstants.Release, "--list-tests")]
        [Theory]
        public void PassingHelpOrListTestsViaTestingPlatformCommandLineArguments_ShouldFailWithClearError(string configuration, string forbiddenOption)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests", identifier: $"{configuration}_{SanitizeForIdentifier(forbiddenOption)}").WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration, $"-p:TestingPlatformCommandLineArguments={forbiddenOption}");

            if (!SdkTestContext.IsLocalized())
            {
                string expectedSource = CliCommandStrings.UnsupportedOptionInTestApplicationArgumentsSource_RunArguments;
                string expectedMessage = string.Format(CliCommandStrings.UnsupportedOptionInTestApplicationArguments, forbiddenOption, expectedSource);
                result.StdErr.Should().Contain(expectedMessage);
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug, "--help")]
        [InlineData(TestingConstants.Debug, "--list-tests")]
        [InlineData(TestingConstants.Release, "--help")]
        [InlineData(TestingConstants.Release, "--list-tests")]
        [Theory]
        public void PassingHelpOrListTestsViaUnmatchedTokens_ShouldFailWithClearError(string configuration, string forbiddenOption)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests", identifier: $"{configuration}_{SanitizeForIdentifier(forbiddenOption)}_unmatched").WithSource();

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration, "--", forbiddenOption);

            if (!SdkTestContext.IsLocalized())
            {
                string expectedSource = CliCommandStrings.UnsupportedOptionInTestApplicationArgumentsSource_CliArguments;
                string expectedMessage = string.Format(CliCommandStrings.UnsupportedOptionInTestApplicationArguments, forbiddenOption, expectedSource);
                result.StdErr.Should().Contain(expectedMessage);
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug, "--help")]
        [InlineData(TestingConstants.Debug, "-?")]
        [InlineData(TestingConstants.Debug, "--list-tests")]
        [InlineData(TestingConstants.Release, "--help")]
        [InlineData(TestingConstants.Release, "-?")]
        [InlineData(TestingConstants.Release, "--list-tests")]
        [Theory]
        public void PassingHelpOrListTestsViaLaunchSettings_ShouldFailWithClearError(string configuration, string forbiddenOption)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithLaunchSettings", identifier: $"{configuration}_{SanitizeForIdentifier(forbiddenOption)}_launchsettings").WithSource();

            var launchSettingsPath = Path.Join(testInstance.Path, "Properties", "launchSettings.json");
            File.WriteAllText(launchSettingsPath, $$"""
                {
                    "profiles": {
                        "MyProfile": {
                            "commandName": "Project",
                            "commandLineArgs": "{{forbiddenOption}}"
                        }
                    }
                }
                """);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration);

            if (!SdkTestContext.IsLocalized())
            {
                string expectedSource = CliCommandStrings.UnsupportedOptionInTestApplicationArgumentsSource_LaunchSettings;
                string expectedMessage = string.Format(CliCommandStrings.UnsupportedOptionInTestApplicationArguments, forbiddenOption, expectedSource);
                result.StdErr.Should().Contain(expectedMessage);
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void PassingHelpInLaunchSettings_IsBypassedByNoLaunchProfileArguments(string configuration)
        {
            TestAsset testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithLaunchSettings", identifier: $"{configuration}_nolaunchprofileargs").WithSource();

            var launchSettingsPath = Path.Join(testInstance.Path, "Properties", "launchSettings.json");
            File.WriteAllText(launchSettingsPath, """
                {
                    "profiles": {
                        "MyProfile": {
                            "commandName": "Project",
                            "commandLineArgs": "--help"
                        }
                    }
                }
                """);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute("-c", configuration, "--no-launch-profile-arguments");

            if (!SdkTestContext.IsLocalized())
            {
                // The bypass is correct iff the launch-settings-sourced validation error never fires.
                // The test asset itself will still fail (it throws when '--from-launch-settings' isn't
                // present), but that failure mode is unrelated to this PR and proves the launch
                // settings args were not forwarded.
                string unsupportedFragment = string.Format(CliCommandStrings.UnsupportedOptionInTestApplicationArguments, "--help", CliCommandStrings.UnsupportedOptionInTestApplicationArgumentsSource_LaunchSettings);
                result.StdErr.Should().NotContain(unsupportedFragment);
                result.StdOut.Should().NotContain(unsupportedFragment);
                result.StdOut.Should().Contain("FAILED to find argument from launchSettings.json");
            }
        }

        private static string SanitizeForIdentifier(string value)
        {
            // Test identifiers are appended to the test-asset path; some forbidden options (e.g. '-?')
            // contain characters that are invalid in Windows file paths.
            var sb = new System.Text.StringBuilder(value.Length);
            foreach (char c in value)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return sb.ToString();
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
