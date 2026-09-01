// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Run.Tests;

[TestClass]
public sealed class RunCommandTests : SdkTest
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

    [TestMethod]
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

        cmd.StdErr.Should().Contain(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath));
    }

    [TestMethod]
    public void MSBuildPropertyExpansion_Project()
    {
        var testAppName = "AppThatOutputsDotnetLaunchProfile";
        var testInstance = TestAssetsManager.CopyTestAsset(testAppName)
            .WithSource();

        var testProjectDirectory = testInstance.Path;
        var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

        File.WriteAllText(launchSettingsPath, """
            {
              "profiles": {
                "First": {
                  "commandName": "Project",
                  "commandLineArgs": "\"$(MSBuildProjectDirectory)\" \"$([System.Int32]::MaxValue)\" \"@(Items)\" \"%(Identity)\"",
                  "environmentVariables": {
                    "TEST_VAR1": "$(MSBuildProjectDirectory)"
                  }
                }
              }
            }
            """);

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testProjectDirectory)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining($"ARGS={testProjectDirectory},{int.MaxValue},@(Items),%(Identity)")
            .And.HaveStdOutContaining($"TEST_VAR1=<<<{testProjectDirectory}>>>");
    }

    [TestMethod]
    public void InvalidMSBuildExpressionInLaunchSettingsDoesNotPreventRun()
    {
        TestAsset testInstance = TestAssetsManager.CopyTestAsset("AppThatOutputsDotnetLaunchProfile")
            .WithSource();

        string launchSettingsPath = Path.Combine(testInstance.Path, "Properties", "launchSettings.json");
        File.WriteAllText(launchSettingsPath, """
            {
              "profiles": {
                "First": {
                  "commandName": "Project",
                  "commandLineArgs": "$([)"
                }
              }
            }
            """);

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings,
                LaunchProfileParser.GetLaunchProfileDisplayName(launchProfile: null),
                "").Trim());
    }

    [TestMethod]
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
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null, runPropertiesFromEvaluation: false, logger: null);

        Assert.AreEqual("executable", command.StartInfo.FileName);
        Assert.AreEqual(dir, command.StartInfo.WorkingDirectory);
        Assert.AreEqual("", command.StartInfo.Arguments);
    }

    [TestMethod]
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
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null, runPropertiesFromEvaluation: false, logger: null);

        Assert.AreEqual("", command.StartInfo.Arguments);
    }

    [TestMethod]
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
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null, runPropertiesFromEvaluation: false, logger: null);

        Assert.AreEqual("\"app 1\" \"app 2\"", command.StartInfo.Arguments);
    }

    [TestMethod]
    public void Executable_MSBuildPropertyExpansion()
    {
        var root = TestAssetsManager.CreateTestDirectory().Path;
        var projectPath = Path.Combine(root, "myproj.csproj");
        var launchSettingsDirectory = Path.Combine(root, "Properties");
        Directory.CreateDirectory(launchSettingsDirectory);
        File.WriteAllText(projectPath, """
            <Project>
              <PropertyGroup>
                <LaunchExecutable>executable</LaunchExecutable>
                <LaunchArgument>expanded-argument</LaunchArgument>
                <LaunchWorkingDirectory>working</LaunchWorkingDirectory>
                <LaunchEnvironment>expanded-environment</LaunchEnvironment>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(launchSettingsDirectory, "launchSettings.json"), """
            {
              "profiles": {
                "MyProfile": {
                  "commandName": "Executable",
                  "executablePath": "$(LaunchExecutable)",
                  "commandLineArgs": "$(LaunchArgument)",
                  "workingDirectory": "$(LaunchWorkingDirectory)",
                  "environmentVariables": {
                    "VALUE": "$(LaunchEnvironment)"
                  }
                }
              }
            }
            """);

        var runCommand = CreateRunCommand(projectPath);
        var result = runCommand.ReadLaunchProfileSettings();

        var model = Assert.IsExactInstanceOfType<ExecutableLaunchProfile>(result.Profile);
        Assert.AreEqual("executable", model.ExecutablePath);
        Assert.AreEqual("expanded-argument", model.CommandLineArgs);
        Assert.AreEqual(Path.Combine(launchSettingsDirectory, "working"), model.WorkingDirectory);
        Assert.AreEqual("expanded-environment", model.EnvironmentVariables["VALUE"]);
    }

    [TestMethod]
    [DataRow("cached-argument", "cached-argument \"app arg\"")]
    [DataRow(null, "\"app arg\"")]
    public void Project_CachedRunPropertiesApplicationArguments(string? cachedArguments, string expectedArguments)
    {
        string root = TestAssetsManager.CreateTestDirectory().Path;
        string projectPath = Path.Combine(root, "myproj.csproj");
        var runCommand = CreateRunCommand(projectPath, applicationArgs: ["app arg"]);
        var runProperties = new RunProperties(
            Command: "executable",
            Arguments: cachedArguments,
            WorkingDirectory: root,
            RuntimeIdentifier: string.Empty,
            DefaultAppHostRuntimeIdentifier: string.Empty,
            TargetFrameworkVersion: string.Empty);

        var command = (Command)runCommand.GetTargetCommand(
            launchSettings: null,
            projectFactory: null,
            cachedRunProperties: runProperties,
            runPropertiesFromEvaluation: false,
            logger: null);

        Assert.AreEqual(expectedArguments, command.StartInfo.Arguments);
    }
}
