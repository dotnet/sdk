// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    [TestClass]
    public class GivenDotnetRunRunsVbproj : SdkTest
    {
        public GivenDotnetRunRunsVbproj()
        {
        }

        [TestMethod]
        public void ItGivesAnErrorWhenAttemptingToUseALaunchProfileThatDoesNotExistWhenThereIsNoLaunchSettingsFile()
        {
            var testAppName = "VBTestApp";
            var testInstance = TestAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var runResult = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "test");

            runResult
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World!")
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile, "test", $"""
                    {Path.Join(testInstance.Path, "My Project", "launchSettings.json")}
                    {Path.Join(testInstance.Path, "VBTestApp.run.json")}
                    """));
        }

        [TestMethod]
        public void ItFailsWhenTryingToUseLaunchProfileSharingTheSameNameWithAnotherProfileButDifferentCapitalization()
        {
            var testAppName = "AppWithDuplicateLaunchProfiles";
            var testInstance = TestAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var runResult = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--launch-profile", "first");

            string expectedError = string.Format(ProjectTools.Resources.DuplicateCaseInsensitiveLaunchProfileNames, "\tfirst," + (OperatingSystem.IsWindows() ? "\r" : "") + "\n\tFIRST");
            runResult
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(expectedError);
        }

        [TestMethod]
        public void ItFailsWithSpecificErrorMessageIfLaunchProfileDoesntExist()
        {
            var testAppName = "VbAppWithLaunchSettings";
            var testInstance = TestAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            string invalidLaunchProfileName = "Invalid";

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--launch-profile", "Invalid")
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(string.Format(ProjectTools.Resources.LaunchProfileDoesNotExist, invalidLaunchProfileName));
        }

        [TestMethod]
        [DataRow("Second")]
        [DataRow("sEcoND")] // ItAcceptsLaunchProfileWithAlternativeCasing
        public void ItUsesLaunchProfileOfTheSpecifiedName(string launchProfileName)
        {
            var testAppName = "VbAppWithLaunchSettings";
            var testInstance = TestAssetsManager.CopyTestAsset(testAppName, identifier: $"LaunchProfileSuccess-{launchProfileName}")
                            .WithSource();

            var launchSettingsPath = Path.Combine(testInstance.Path, "My Project", "launchSettings.json");

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--launch-profile", launchProfileName)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Second")
                .And
                .HaveStdErrContaining(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath));
        }

        [TestMethod]
        public void ItDefaultsToTheFirstUsableLaunchProfile()
        {
            var testAppName = "VbAppWithLaunchSettings";
            var testInstance = TestAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "My Project", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute();

            cmd.Should().Pass()
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
                .And.HaveStdOutContaining("First");
        }

        [TestMethod]
        public void ItPrintsUsingLaunchSettingsMessageWhenNotQuiet()
        {
            var testInstance = TestAssetsManager.CopyTestAsset("VbAppWithLaunchSettings")
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "My Project", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("-v:m");

            cmd.Should().Pass()
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
                .And.HaveStdOutContaining("First");
        }

        [TestMethod]
        public void ItGivesAnErrorWhenTheLaunchProfileNotFound()
        {
            var testAppName = "VbAppWithLaunchSettings";
            var testInstance = TestAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "Third")
                .Should().Pass()
                         .And.HaveStdOutContaining("(NO MESSAGE)")
                         .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, "Third", "").Trim());
        }
    }
}
