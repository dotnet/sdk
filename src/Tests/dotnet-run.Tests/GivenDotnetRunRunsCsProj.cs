// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRunBuildsCsproj : SdkTest
    {
        public GivenDotnetRunBuildsCsproj(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItCanRunAMSBuildProject()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new BuildCommand(Log, testProjectDirectory)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenRunning()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItCanRunAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = _testAssetsManager.CopyTestAsset(
                    "NETFrameworkReferenceNETStandard20",
                    testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
                .WithSource();

            string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectDirectory)
                .Execute("--framework", "netcoreapp3.0")
                .Should().Pass()
                         .And.HaveStdOutContaining("This string came from the test library!");
        }

        [Fact]
        public void ItDoesNotImplicitlyBuildAProjectWhenRunningWithTheNoBuildOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--no-build", "-v:m");

            result.Should().Fail();
            if (!TestContext.IsLocalized())
            {
                result.Should().NotHaveStdOutContaining("Restore");
            }
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenRunningWithTheNoRestoreOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void ItBuildsTheProjectBeforeRunning()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItCanRunAMSBuildProjectWhenSpecifyingAFramework()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--framework", "netcoreapp3.0")
                .Should().Pass()
                         .And.HaveStdOut("Hello World!");
        }

        [Fact]
        public void ItRunsPortableAppsFromADifferentPathAfterBuilding()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource();

            new BuildCommand(Log, testInstance.Path)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute($"--no-build")
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItRunsPortableAppsFromADifferentPathWithoutBuilding()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var projectFile = Path.Combine(testInstance.Path, testAppName + ".csproj");

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(Directory.GetParent(testInstance.Path).FullName)
                .Execute($"--project", projectFile)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItRunsPortableAppsFromADifferentPathSpecifyingOnlyTheDirectoryWithoutBuilding()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(Directory.GetParent(testInstance.Path).FullName)
                .Execute("--project", testProjectDirectory)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItRunsAppWhenRestoringToSpecificPackageDirectory()
        {
            var rootPath = _testAssetsManager.CreateTestDirectory().Path;

            string dir = "pkgs";
            string [] args = new string[] { "--packages", dir };

            string [] newArgs = new string[] { "console", "-o", rootPath, "--no-restore" };
            new DotnetCommand(Log, "new")
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(rootPath)
                .Execute("--no-restore")
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItReportsAGoodErrorWhenProjectHasMultipleFrameworks()
        {
            var testAppName = "MSBuildAppWithMultipleFrameworks";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            // use --no-build so this test can run on all platforms.
            // the test app targets net451, which can't be built on non-Windows
            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--no-build")
                .Should().Fail()
                    .And.HaveStdErrContaining("--framework");
        }

        [Fact]
        public void ItCanPassArgumentsToSubjectAppByDoubleDash()
        {
            const string testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--", "foo", "bar", "baz")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("echo args:foo;bar;baz");
        }

        [Fact]
        public void ItGivesAnErrorWhenAttemptingToUseALaunchProfileThatDoesNotExistWhenThereIsNoLaunchSettingsFile()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "test")
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!")
                         .And.HaveStdErrContaining(LocalizableStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile);
        }

        [Fact]
        public void ItUsesLaunchProfileOfTheSpecifiedName()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "Second");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("Second");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItDefaultsToTheFirstUsableLaunchProfile()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute();

            cmd.Should().Pass()
                .And.NotHaveStdOutContaining(string.Format(LocalizableStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
                .And.HaveStdOutContaining("First");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItPrintsUsingLaunchSettingsMessageWhenNotQuiet()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("AppWithLaunchSettings")
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("-v:m");

            cmd.Should().Pass()
                .And.HaveStdOutContaining(string.Format(LocalizableStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
                .And.HaveStdOutContaining("First");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItPrefersTheValueOfAppUrlFromEnvVarOverTheProp()
        {
            var testAppName = "AppWithApplicationUrlInLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "First");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("http://localhost:12345/");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItUsesTheValueOfAppUrlIfTheEnvVarIsNotSet()
        {
            var testAppName = "AppWithApplicationUrlInLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "Second");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("http://localhost:54321/");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItGivesAnErrorWhenTheLaunchProfileNotFound()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "Third")
                .Should().Pass()
                         .And.HaveStdOutContaining("(NO MESSAGE)")
                         .And.HaveStdErrContaining(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, "Third", "").Trim());
        }

        [Fact]
        public void ItGivesAnErrorWhenTheLaunchProfileCanNotBeHandled()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "IIS Express")
                .Should().Pass()
                         .And.HaveStdOutContaining("(NO MESSAGE)")
                         .And.HaveStdErrContaining(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, "IIS Express", "").Trim());
        }

        [Fact]
        public void ItSkipsLaunchProfilesWhenTheSwitchIsSupplied()
        {
            var testAppName = "AppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--no-launch-profile");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("(NO MESSAGE)");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItSkipsLaunchProfilesWhenTheSwitchIsSuppliedWithoutErrorWhenThereAreNoLaunchSettings()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--no-launch-profile");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("Hello World!");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItSkipsLaunchProfilesWhenThereIsNoUsableDefault()
        {
            var testAppName = "AppWithLaunchSettingsNoDefault";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute();

            cmd.Should().Pass()
                .And.HaveStdOutContaining("(NO MESSAGE)")
                .And.HaveStdErrContaining(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, LocalizableStrings.DefaultLaunchProfileDisplayName, "").Trim());
        }

        [Fact]
        public void ItPrintsAnErrorWhenLaunchSettingsAreCorrupted()
        {
            var testAppName = "AppWithCorruptedLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute();

            cmd.Should().Pass()
                .And.HaveStdOutContaining("(NO MESSAGE)")
                .And.HaveStdErrContaining(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, LocalizableStrings.DefaultLaunchProfileDisplayName, "").Trim());
        }

        [Fact]
        public void ItRunsWithTheSpecifiedVerbosity()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory( testInstance.Path)
                .Execute("-v:n");

            result.Should().Pass()
                .And.HaveStdOutContaining("Hello World!");

            if (!TestContext.IsLocalized())
            {
                result.Should().HaveStdOutContaining("Restore")
                    .And.HaveStdOutContaining("CoreCompile");
            }
        }

        [Fact]
        public void ItDoesNotShowImportantLevelMessageByDefault()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeRestoreToProject);

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            result.Should().Pass()
                .And.NotHaveStdOutContaining("Important text");
        }

        [Fact]
        public void ItShowImportantLevelMessageWhenPassInteractive()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeRestoreToProject);

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--interactive");

            result.Should().Pass()
                .And.HaveStdOutContaining("Important text");
        }

        [Fact]
        public void ItRunsWithDotnetWithoutApphost()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("AppOutputsExecutablePath").WithSource();

            var command = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .WithEnvironmentVariable("UseAppHost", "false");

            command.Execute()
                   .Should()
                   .Pass()
                   .And
                   .HaveStdOutContaining($"dotnet{Constants.ExeSuffix}");
        }

        [PlatformSpecificFact(Platform.Windows, Platform.Linux, Platform.FreeBSD)]
        public void ItRunsWithApphost()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("AppOutputsExecutablePath").WithSource();

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            result.Should().Pass()
                .And.HaveStdOutContaining($"AppOutputsExecutablePath{Constants.ExeSuffix}");
        }

        [Fact]
        public void ItForwardsEmptyArgumentsToTheApp()
        {
            var testAppName = "TestAppSimple";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("a", "", "c")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining($"0 = a{Environment.NewLine}1 = {Environment.NewLine}2 = c");
        }
    }
}
