// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunCommandTests(ITestOutputHelper log) : SdkTest(log)
{
    private static string GetUniqueName()
        => Guid.NewGuid().ToString("N");

    private static readonly string s_environmentVariableName1 = $"TEST_VAR1_{GetUniqueName()}";
    private static readonly string s_environmentVariableName2 = $"TEST_VAR1_{GetUniqueName()}";
    private static readonly string s_environmentVariableNameUnset = $"TEST_VAR3_{GetUniqueName()}";

    static RunCommandTests()
    {
        Environment.SetEnvironmentVariable(s_environmentVariableName1, "ENV_VALUE1");
        Environment.SetEnvironmentVariable(s_environmentVariableName2, "ENV_VALUE2");
    }

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
            noLaunchProfileArguments,
            noRestore: false,
            noCache: false,
            interactive: false,
            MSBuildArgs.FromOtherArgs([]),
            applicationArgs: applicationArgs ?? [],
            readCodeFromStdin: false,
            environmentVariables: new Dictionary<string, string>(),
            msbuildRestoreProperties: new(new Dictionary<string, string>()));

    [Fact]
    public void EnvironmentVariableExpansion_Project()
    {
        var testAppName = "AppThatOutputsDotnetLaunchProfile";
        var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
            .WithSource();

        var testProjectDirectory = testInstance.Path;
        var launchSettingsPath = Path.Combine(testProjectDirectory, "Properties", "launchSettings.json");

        File.WriteAllText(launchSettingsPath, $$"""
            {
              "profiles": {
                "First": {
                  "commandName": "Project",
                  "commandLineArgs": "arg1 {{EnvironmentVariableReference("VAR1")}} arg3",
                  "environmentVariables": {
                    "TEST_VAR1": "{{EnvironmentVariableReference("VAR1")}}",
                    "TEST_VAR2": "{{EnvironmentVariableReference(s_environmentVariableNameUnset)}}",
                    "TEST_VAR3": "{{EnvironmentVariableReference("TEST_VAR2")}}"
                  }
                }
              }
            }
            """);

        var cmd = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testProjectDirectory)
            .WithEnvironmentVariable("VAR1", "VALUE1")
            .WithEnvironmentVariable("VAR2", "VALUE2")
            .Execute();

        cmd.Should().Pass()
            .And.HaveStdOutContaining("DOTNET_LAUNCH_PROFILE=<<<First>>>")
            .And.HaveStdOutContaining("TEST_VAR1=<<<VALUE1>>>")
            .And.HaveStdOutContaining($"TEST_VAR2=<<<{EnvironmentVariableReference(s_environmentVariableNameUnset)}>>>")
            .And.HaveStdOutContaining($"TEST_VAR3=<<<{EnvironmentVariableReference("TEST_VAR2")}>>>")
            .And.HaveStdOutContaining("ARGS=arg1,VALUE1,arg3");

        cmd.StdErr.Should().BeEmpty();
    }

    [Fact]
    public void Executable_EnvironmentVariableExpansionAndPathNormalization()
    {
        var root = _testAssetsManager.CreateTestDirectory().Path;
        var dir = Path.Combine(root, "dir");

        var launchSettingsPath = Path.Combine(dir, "launchSettings.json");
        var projectPath = Path.Combine(dir, "myproj.csproj");
        var workingDirectory = Path.Combine(dir, "wd");

        var model = new ExecutableLaunchSettingsModel()
        {
            LaunchProfileName = "MyProfile",
            ExecutablePath = $"../path/{EnvironmentVariableReference(s_environmentVariableName1)}/executable",
            CommandLineArgs = $"arg1 {EnvironmentVariableReference(s_environmentVariableName1)} arg3",
            WorkingDirectory = Path.Combine("..", EnvironmentVariableReference(s_environmentVariableName1)),
            EnvironmentVariables = ImmutableDictionary<string, string>.Empty
                .Add(s_environmentVariableNameUnset, EnvironmentVariableReference(s_environmentVariableName2))
                .Add("VAR1", EnvironmentVariableReference(s_environmentVariableNameUnset))
                .Add("VAR2", "ENV_VALUE2")
        };

        var runCommand = CreateRunCommand(projectPath);
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null);

        Assert.Equal("../path/ENV_VALUE1/executable", command.StartInfo.FileName);
        Assert.Equal(Path.Combine(root, "ENV_VALUE1"), command.StartInfo.WorkingDirectory);
        Assert.Equal("arg1 ENV_VALUE1 arg3", command.StartInfo.Arguments);
        Assert.Equal(
        [
            ("DOTNET_LAUNCH_PROFILE", "MyProfile"),
            (s_environmentVariableNameUnset, "ENV_VALUE2"),
            ("VAR1", EnvironmentVariableReference(s_environmentVariableNameUnset)),
            ("VAR2", "ENV_VALUE2")
        ], command.CustomEnvironmentVariables!.OrderBy(e => e.Key).Select(e => (e.Key, e.Value)));
    }

    [Fact]
    public void Executable_DefaultWorkingDirectory()
    {
        var root = _testAssetsManager.CreateTestDirectory().Path;
        var dir = Path.Combine(root, "dir");

        var launchSettingsPath = Path.Combine(dir, "launchSettings.json");
        var projectPath = Path.Combine(dir, "myproj.csproj");

        var model = new ExecutableLaunchSettingsModel()
        {
            LaunchProfileName = "MyProfile",
            ExecutablePath = "executable",
            EnvironmentVariables = []
        };

        var runCommand = CreateRunCommand(projectPath);
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null);

        Assert.Equal("executable", command.StartInfo.FileName);
        Assert.Equal(dir, command.StartInfo.WorkingDirectory);
        Assert.Equal("", command.StartInfo.Arguments);
    }

    [Fact]
    public void Executable_NoLaunchProfileArguments()
    {
        var root = _testAssetsManager.CreateTestDirectory().Path;
        var dir = Path.Combine(root, "dir");

        var launchSettingsPath = Path.Combine(dir, "launchSettings.json");
        var projectPath = Path.Combine(dir, "myproj.csproj");

        var model = new ExecutableLaunchSettingsModel()
        {
            LaunchProfileName = "MyProfile",
            CommandLineArgs = "arg1 arg2",
            ExecutablePath = "executable",
            EnvironmentVariables = []
        };

        var runCommand = CreateRunCommand(projectPath, noLaunchProfileArguments: true);
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null);

        Assert.Equal("", command.StartInfo.Arguments);
    }

    [Fact]
    public void Executable_ApplicationArguments()
    {
        var root = _testAssetsManager.CreateTestDirectory().Path;
        var dir = Path.Combine(root, "dir");

        var launchSettingsPath = Path.Combine(dir, "launchSettings.json");
        var projectPath = Path.Combine(dir, "myproj.csproj");

        var model = new ExecutableLaunchSettingsModel()
        {
            LaunchProfileName = "MyProfile",
            CommandLineArgs = "arg1 arg2",
            ExecutablePath = "executable",
            EnvironmentVariables = []
        };

        var runCommand = CreateRunCommand(projectPath, applicationArgs: ["app 1", "app 2"]);
        var command = (Command)runCommand.GetTargetCommand(model, projectFactory: null, cachedRunProperties: null);

        Assert.Equal("\"app 1\" \"app 2\"", command.StartInfo.Arguments);
    }
}
