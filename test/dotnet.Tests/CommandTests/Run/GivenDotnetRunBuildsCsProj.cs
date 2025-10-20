// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Utils;

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

            new BuildCommand(testInstance)
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
                .Execute("--framework", ToolsetInfo.CurrentTargetFramework)
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
                .Execute("--framework", ToolsetInfo.CurrentTargetFramework)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItRunsPortableAppsFromADifferentPathAfterBuilding()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource();

            new BuildCommand(testInstance)
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
                         .And.HaveStdOutContaining("Hello World!")
                         .And.NotHaveStdOutContaining(CliCommandStrings.RunCommandProjectAbbreviationDeprecated);
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
                         .And.HaveStdOutContaining("Hello World!")
                         .And.NotHaveStdOutContaining(CliCommandStrings.RunCommandProjectAbbreviationDeprecated);
        }

        [Fact]
        public void ItWarnsWhenShortFormOfProjectArgumentIsUsed()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var projectFile = Path.Combine(testInstance.Path, testAppName + ".csproj");

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(Directory.GetParent(testInstance.Path).FullName)
                .Execute($"-p", projectFile)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World!")
                         .And.HaveStdOutContaining(CliCommandStrings.RunCommandProjectAbbreviationDeprecated);
        }

        [Theory]
        [InlineData("-p project1 -p project2")]
        [InlineData("--project project1 -p project2")]
        public void ItErrorsWhenMultipleProjectsAreSpecified(string args)
        {
            new DotnetCommand(Log, "run")
                .Execute(args.Split(" "))
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(CliStrings.OnlyOneProjectAllowed);
        }

        [Fact]
        public void ItRunsAppWhenRestoringToSpecificPackageDirectory()
        {
            var rootPath = _testAssetsManager.CreateTestDirectory().Path;

            string dir = "pkgs";
            string[] args = new string[] { "--packages", dir };

            string[] newArgs = new string[] { "console", "-o", rootPath, "--no-restore" };
            new DotnetNewCommand(Log)
                .WithVirtualHive()
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
                         .And.HaveStdOutContaining("Hello, World");
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
        public void ItCanPassOptionArgumentsToSubjectAppByDoubleDash()
        {
            const string testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--", "-d", "-a")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("echo args:-d;-a");
        }

        [Fact]
        public void ItCanPassOptionAndArgumentsToSubjectAppByDoubleDash()
        {
            const string testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--", "foo", "-d", "-a")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("echo args:foo;-d;-a");
        }

        [Fact]
        public void ItCanPassArgumentsToSubjectAppWithoutDoubleDash()
        {
            const string testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("foo", "bar", "baz")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("echo args:foo;bar;baz");
        }

        [Fact]
        public void ItCanPassUnrecognizedOptionArgumentsToSubjectAppWithoutDoubleDash()
        {
            const string testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("-x", "-y", "-z")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("echo args:-x;-y;-z");
        }

        [Fact]
        public void ItCanPassOptionArgumentsAndArgumentsToSubjectAppWithoutAndByDoubleDash()
        {
            const string testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("foo", "--", "-z")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("echo args:foo;-z");
        }

        [Fact]
        public void ItGivesAnErrorWhenAttemptingToUseALaunchProfileThatDoesNotExistWhenThereIsNoLaunchSettingsFile()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            var runResult = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "test");

            runResult
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World!")
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile, "test", $"""
                    {Path.Join(testInstance.Path, "Properties", "launchSettings.json")}
                    {Path.Join(testInstance.Path, "MSBuildTestApp.run.json")}
                    """));
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

            var cmd = new DotnetCommand(Log, "run", "--verbosity", "quiet")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute();

            cmd.Should().Pass()
                .And.NotHaveStdOutContaining(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
                .And.HaveStdOutContaining("First");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItSetsTheDotnetLaunchProfileEnvironmentVariableToDefaultLaunchProfileName()
        {
            var testAppName = "AppThatOutputsDotnetLaunchProfile";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute();

            cmd.Should().Pass()
                .And.HaveStdOutContaining("DOTNET_LAUNCH_PROFILE=<<<First>>>");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItSetsTheDotnetLaunchProfileEnvironmentVariableToSuppliedLaunchProfileName()
        {
            var testAppName = "AppThatOutputsDotnetLaunchProfile";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "Second");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("DOTNET_LAUNCH_PROFILE=<<<Second>>>");

            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void ItSetsTheDotnetLaunchProfileEnvironmentVariableToEmptyWhenInvalidProfileSpecified()
        {
            var testAppName = "AppThatOutputsDotnetLaunchProfile";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--launch-profile", "DoesNotExist");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("DOTNET_LAUNCH_PROFILE=<<<>>>");

            cmd.StdErr.Should().Contain("DoesNotExist");
        }

        [Fact]
        public void ItSetsTheDotnetLaunchProfileEnvironmentVariableToEmptyWhenNoLaunchProfileSwitchIsUsed()
        {
            var testAppName = "AppThatOutputsDotnetLaunchProfile";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;
            var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

            var cmd = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--no-launch-profile");

            cmd.Should().Pass()
                .And.HaveStdOutContaining("DOTNET_LAUNCH_PROFILE=<<<>>>");

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
                .And.HaveStdOutContaining(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath))
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
                         .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, "Third", "").Trim());
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
                         .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, "IIS Express", "").Trim());
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
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, CliCommandStrings.DefaultLaunchProfileDisplayName, "").Trim());
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
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, CliCommandStrings.DefaultLaunchProfileDisplayName, "").Trim());
        }

        [Fact]
        public void ItRunsWithTheSpecifiedVerbosity()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
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
        public void ItDoesNotShowImportantLevelMessageByDefaultWhenInteractivityDisabled()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeRestoreToProject);

            var result = new DotnetCommand(Log, "run", "--interactive", "false")
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            // this message should show because interactivity (and therefore nuget auth) is the default
            result.Should().Pass()
                .And.NotHaveStdOutContaining("Important text");
        }

        /// <summary>
        /// default verbosity for `run` is as quiet as possible, so it does not show important messages.
        /// NuGet authentication messages _are_ shown, but all other non-warning/-error messages are not.
        /// </summary>
        [Fact]
        public void ItDoesNotShowImportantLevelMessageWhenPassInteractive()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeRestoreToProject);

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--interactive");

            result.Should().Pass()
                .And.NotHaveStdOutContaining("Important text");
        }

        [Fact]
        public void ItShowsImportantLevelMessageWhenPassInteractiveAndVerbose()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeRestoreToProject);

            var result = new DotnetCommand(Log, "run", "/v", "d")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--interactive");

            result.Should().Pass()
                .And.HaveStdOutContaining("Important text");
        }


        [Fact]
        public void ItPrintsDuplicateArguments()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(testInstance.Path)
                .Execute("a", "b", "c", "a", "c");

            result.Should().Pass()
                .And.HaveStdOutContaining("echo args:a;b;c;a;c");
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

        [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.Linux | TestPlatforms.FreeBSD)]
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

        [Fact]
        public void ItDoesNotPrintBuildingMessageByDefault()
        {
            var expectedValue = "Building...";
            var testAppName = "TestAppSimple";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetCommand(Log, "run")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .NotHaveStdOutContaining(expectedValue);
        }

        [Fact]
        public void ItPrintsBuildingMessageIfLaunchSettingHasDotnetRunMessagesSet()
        {
            var expectedValue = "Building...";
            var testAppName = "TestAppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetCommand(Log, "run")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining(expectedValue);
        }

        [Fact]
        public void ItIncludesEnvironmentVariablesSpecifiedInLaunchSettings()
        {
            var expectedValue = "MyCoolEnvironmentVariableKey=MyCoolEnvironmentVariableValue";
            var testAppName = "TestAppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetCommand(Log, "run")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining(expectedValue);
        }

        [Fact]
        public void EnvVariablesSpecifiedInLaunchProfileOverrideImplicitlySetVariables()
        {
            var testAppName = "TestAppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            // Profile2 defines env variable DOTNET_LAUNCH_PROFILE=XYZ and ASPNETCORE_URLS=XYZ

            new DotnetCommand(Log, "run", "-lp", "Profile2")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining("env: DOTNET_LAUNCH_PROFILE=XYZ")
               .And
               .HaveStdOutContaining("env: ASPNETCORE_URLS=XYZ");
        }

        [Fact]
        public void ItIncludesCommandArgumentsSpecifiedInLaunchSettings()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppWithLaunchSettings")
                .WithSource();

            // launchSettings.json specifies commandLineArgs="TestAppCommandLineArguments SecondTestAppCommandLineArguments"

            new DotnetCommand(Log, "run")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining("TestAppCommandLineArguments")
               .And
               .HaveStdOutContaining("SecondTestAppCommandLineArguments");
        }

        [Fact]
        public void ItIgnoresCommandArgumentsSpecifiedInLaunchSettings()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppWithLaunchSettings")
                .WithSource();

            // launchSettings.json specifies commandLineArgs="TestAppCommandLineArguments SecondTestAppCommandLineArguments"

            new DotnetCommand(Log, "run", "--no-launch-profile-arguments")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .NotHaveStdOutContaining("TestAppCommandLineArguments")
               .And
               .NotHaveStdOutContaining("SecondTestAppCommandLineArguments");
        }

        [Fact]
        public void ItCLIArgsOverrideCommandArgumentsSpecifiedInLaunchSettings()
        {
            var expectedValue = "TestAppCommandLineArguments";
            var secondExpectedValue = "SecondTestAppCommandLineArguments";
            var testAppName = "TestAppWithLaunchSettings";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetCommand(Log, "run", "-- test")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .NotHaveStdOutContaining(expectedValue)
               .And
               .NotHaveStdOutContaining(secondExpectedValue);
        }

        [Fact]
        public void ItIncludesApplicationUrlSpecifiedInLaunchSettings()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppWithLaunchSettings")
                .WithSource();

            new DotnetCommand(Log, "run")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining("env: ASPNETCORE_URLS=http://localhost:5000");
        }

        [Theory]
        [InlineData("-e")]
        [InlineData("--environment")]
        public void EnvOptionOverridesCommandArgumentsSpecifiedInLaunchSettings(string optionName)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppWithLaunchSettings")
                .WithSource();

            new DotnetCommand(Log, "run", optionName, "MyCoolEnvironmentVariableKey=OverriddenEnvironmentVariableValue")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining("env: MyCoolEnvironmentVariableKey=OverriddenEnvironmentVariableValue");
        }

        [Fact]
        public void EnvOptionOverridesImplicitlySetVariables()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppWithLaunchSettings")
                .WithSource();

            //
            var dotnetLaunchProfile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "DOTNET_LAUNCH_profile"
                : "DOTNET_LAUNCH_PROFILE";

            new DotnetCommand(Log, "run", "-e", $"{dotnetLaunchProfile}=1", "-e", "ASPNETCORE_URLS=2")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining("env: DOTNET_LAUNCH_PROFILE=1")
               .And
               .HaveStdOutContaining("env: ASPNETCORE_URLS=2");
        }

        [Fact]
        public void EnvOptionNotAppliedToBuild()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppWithLaunchSettings")
                .WithSource();

            new DotnetCommand(Log, "run", "-e", "Configuration=XYZ")
               .WithWorkingDirectory(testInstance.Path)
               .Execute()
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining("env: Configuration=XYZ");
        }

        [Fact]
        public void ItProvidesConsistentErrorMessageWhenProjectFileDoesNotExistWithNoBuild()
        {
            var tempDir = _testAssetsManager.CreateTestDirectory();
            var nonExistentProject = Path.Combine(tempDir.Path, "nonexistent.csproj");

            var result = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(tempDir.Path)
                .Execute("--project", nonExistentProject, "--no-build");

            result.Should().Fail();
            if (!TestContext.IsLocalized())
            {
                // After the fix, we should get a clear error message about the file not existing
                var stderr = result.StdErr;

                // Should provide a clear error message about the project file not existing
                var hasExpectedErrorMessage = stderr.Contains("does not exist") ||
                                              stderr.Contains("not found") ||
                                              stderr.Contains("cannot find") ||
                                              stderr.Contains("could not find");

                hasExpectedErrorMessage.Should().BeTrue($"Expected error message to clearly indicate file doesn't exist, but got: {stderr}");
            }
        }
    }
}
