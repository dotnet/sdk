// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestsForMultipleTFMs : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestsForMultipleTFMs(ITestOutputHelper log) : base(log)
        {
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Error output: Failed to load /private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), 
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunMultipleProjectWithDifferentTFMs_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("ProjectSolutionForMultipleTFMs", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFramework($"{DotnetVersionHelper.GetPreviousDotnetVersion()}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                MatchCollection previousDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: false, configuration));
                MatchCollection currentDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, useCurrentVersion: true, configuration));

                MatchCollection skippedTestsMatches = Regex.Matches(result.StdOut!, "skipped Test2");
                MatchCollection failedTestsMatches = Regex.Matches(result.StdOut!, "failed Test3");

                Assert.True(previousDotnetProjectMatches.Count > 1);
                Assert.True(currentDotnetProjectMatches.Count > 1);

                Assert.Single(failedTestsMatches);
                Assert.Multiple(() => Assert.Equal(2, skippedTestsMatches.Count));

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
        public void RunProjectWithMultipleTFMs_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                MatchCollection previousDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: false, configuration));
                MatchCollection currentDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: true, configuration));
                MatchCollection currentDotnetOtherProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("OtherTestProject", TestingConstants.Passed, useCurrentVersion: true, configuration));

                MatchCollection skippedTestsMatches = Regex.Matches(result.StdOut!, "skipped Test1");
                MatchCollection failedTestsMatches = Regex.Matches(result.StdOut!, "failed Test2");
                MatchCollection timeoutTestsMatches = Regex.Matches(result.StdOut!, @"failed \(canceled\) Test3");
                MatchCollection errorTestsMatches = Regex.Matches(result.StdOut!, "failed Test4");
                MatchCollection canceledTestsMatches = Regex.Matches(result.StdOut!, @"failed \(canceled\) Test5");

                Assert.True(previousDotnetProjectMatches.Count > 1);
                Assert.True(currentDotnetProjectMatches.Count > 1);
                Assert.True(currentDotnetOtherProjectMatches.Count > 1);

                Assert.Multiple(() => Assert.Equal(2, skippedTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, failedTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, timeoutTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, errorTestsMatches.Count));
                Assert.Multiple(() => Assert.Equal(2, skippedTestsMatches.Count));

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 14")
                    .And.Contain("succeeded: 3")
                    .And.Contain("failed: 8")
                    .And.Contain("skipped: 3");
            }

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Error output: Failed to load /private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), 
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData(true)]
        [InlineData(false)]
        public void RunProjectWithMultipleTFMs_ParallelizationTest_RunInParallelShouldFail(bool testTfmsInParallel)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithMultipleTFMsParallelization", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(CommonOptions.PropertiesOption.Name, $"TestTfmsInParallel={testTfmsInParallel}");

            if (testTfmsInParallel)
            {
                if (!TestContext.IsLocalized())
                {
                    result.StdOut
                        .Should().Contain("Test run summary: Failed!")
                        .And.Contain("total: 2")
                        .And.Contain("succeeded: 0")
                        .And.Contain("failed: 2")
                        .And.Contain("skipped: 0")
                        .And.Contain("This is run in parallel!");
                }
                else
                {
                    result.StdOut
                        .Should().Contain("This is run in parallel!");
                }

                result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
            }
            else
            {
                result.ExitCode.Should().Be(ExitCodes.Success);
            }
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Error output: Failed to load /private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), 
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunProjectWithMultipleTFMsWithArchOption_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}", "TestProject");
            var arch = RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86";

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration,
                                    CommonOptions.ArchitectureOption.Name, arch);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("error NETSDK1134: Building a solution with a specific RuntimeIdentifier is not supported. If you would like to publish for a single RID, specify the RID at the individual project level instead.");
            }
            else
            {
                result.StdOut.Should().Contain("NETSDK1134");
            }

            result.ExitCode.Should().Be(ExitCodes.GenericFailure);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Error output: Failed to load /private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), '/System/Volumes/Preboot/Cryptexes/OS/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (no such file), '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64'))
        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void RunProjectWithMSTestMetaPackageAndMultipleTFMs_ShouldReturnExitCodeGenericFailure(string configuration)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("MSTestMetaPackageProjectWithMultipleTFMsSolution", Guid.NewGuid().ToString())
                .WithSource();
            testInstance.WithTargetFrameworks($"{DotnetVersionHelper.GetPreviousDotnetVersion()};{ToolsetInfo.CurrentTargetFramework}");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .Execute(TestingPlatformOptions.ConfigurationOption.Name, configuration);

            if (!TestContext.IsLocalized())
            {
                MatchCollection previousDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: false, configuration));
                MatchCollection currentDotnetProjectMatches = Regex.Matches(result.StdOut!, RegexPatternHelper.GenerateProjectRegexPattern("TestProject", TestingConstants.Failed, useCurrentVersion: true, configuration));

                MatchCollection failedTestsMatches = Regex.Matches(result.StdOut!, "failed TestMethod3");

                Assert.True(previousDotnetProjectMatches.Count > 1);
                Assert.True(currentDotnetProjectMatches.Count > 1);

                Assert.Multiple(() => Assert.Equal(2, failedTestsMatches.Count));

                result.StdOut
                    .Should().Contain("Test run summary: Failed!")
                    .And.Contain("total: 5")
                    .And.Contain("succeeded: 3")
                    .And.Contain("failed: 2")
                    .And.Contain("skipped: 0");
            }

            result.ExitCode.Should().Be(ExitCodes.AtLeastOneTestFailed);
        }
    }
}
