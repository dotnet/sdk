// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunCommandTests(ITestOutputHelper log) : SdkTest(log)
{
    // The same syntax works on Windows and Unix ($VAR does not get expanded Unix).
    private static string EnvironmentVariableReference(string name)
        => $"%{name}%";

    private static RunCommand CreateRunCommand(
        string projectPath,
        bool noLaunchProfileArguments = false,
        string[]? applicationArgs = null)
        => new(
            noBuild: true,
            projectFileFullPath: projectPath,
            entryPointFileFullPath: null,
            launchProfile: null,
            noLaunchProfile: false,
            noLaunchProfileArguments: noLaunchProfileArguments,
            device: null,
            listDevices: false,
            noRestore: false,
            noCache: false,
            interactive: false,
            msbuildArgs: MSBuildArgs.FromOtherArgs([]),
            applicationArgs: applicationArgs ?? [],
            readCodeFromStdin: false,
            environmentVariables: new Dictionary<string, string>());

    [Fact]
    public void EnvironmentVariableExpansion_Project()
    {
        var testAppName = "AppThatOutputsDotnetLaunchProfile";
        var testInstance = TestAssetsManager.CopyTestAsset(testAppName)
            .WithSource();

        var testProjectDirectory = testInstance.Path;
        var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

        File.WriteAllText(launchSettingsPath, $$"""
            {
              "profiles": {
                "First": {
                  "commandName": "Project",
                  "commandLineArgs": "arg1 arg2 arg3",
                  "environmentVariables": {
                    "TEST_VAR1": "{{EnvironmentVariableReference("VAR1")}}"
                  }
                }
              }
            }
            """);

        var cmd = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testProjectDirectory)
            .WithEnvironmentVariable("VAR1", "VALUE1")
            .Execute();

        cmd.Should().Pass()
            .And.HaveStdOutContaining("DOTNET_LAUNCH_PROFILE=<<<First>>>")
            .And.HaveStdOutContaining("TEST_VAR1=<<<VALUE1>>>")
            .And.HaveStdOutContaining("ARGS=arg1,arg2,arg3");

        cmd.StdErr.Should().BeEmpty();
    }

    [Fact]
    public void Executable_DefaultWorkingDirectory()
    {
        var root = TestAssetsManager.CreateTestDirectory().Path;
        var dir = Path.Combine(root, "dir");

        var launchSettingsPath = Path.Combine(dir, "launchSettings.json");
        var projectPath = Path.Combine(dir, "myproj.csproj");

        var model = new ExecutableLaunchProfile()
        {
            LaunchProfileName = "MyProfile",
            ExecutablePath = "executable",
            EnvironmentVariables = []
        };

        var runCommand = CreateRunCommand(projectPath);
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null, logger: null);

        Assert.Equal("executable", command.StartInfo.FileName);
        Assert.Equal(dir, command.StartInfo.WorkingDirectory);
        Assert.Equal("", command.StartInfo.Arguments);
    }

    [Fact]
    public void Executable_NoLaunchProfileArguments()
    {
        var root = TestAssetsManager.CreateTestDirectory().Path;
        var dir = Path.Combine(root, "dir");

        var launchSettingsPath = Path.Combine(dir, "launchSettings.json");
        var projectPath = Path.Combine(dir, "myproj.csproj");

        var model = new ExecutableLaunchProfile()
        {
            LaunchProfileName = "MyProfile",
            CommandLineArgs = "arg1 arg2",
            ExecutablePath = "executable",
            EnvironmentVariables = []
        };

        var runCommand = CreateRunCommand(projectPath, noLaunchProfileArguments: true);
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null, logger: null);

        Assert.Equal("", command.StartInfo.Arguments);
    }

    [Fact]
    public void Executable_ApplicationArguments()
    {
        var root = TestAssetsManager.CreateTestDirectory().Path;
        var dir = Path.Combine(root, "dir");

        var launchSettingsPath = Path.Combine(dir, "launchSettings.json");
        var projectPath = Path.Combine(dir, "myproj.csproj");

        var model = new ExecutableLaunchProfile()
        {
            LaunchProfileName = "MyProfile",
            CommandLineArgs = "arg1 arg2",
            ExecutablePath = "executable",
            EnvironmentVariables = []
        };

        var runCommand = CreateRunCommand(projectPath, applicationArgs: ["app 1", "app 2"]);
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null, logger: null);

        Assert.Equal("\"app 1\" \"app 2\"", command.StartInfo.Arguments);
    }
}
