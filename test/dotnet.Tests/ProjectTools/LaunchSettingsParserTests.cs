// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ProjectTools.Tests;

public class LaunchSettingsParserTests
{
    private static readonly string s_environmentVariableName1 = $"TEST_VAR1_{GetUniqueName()}";
    private static readonly string s_environmentVariableName2 = $"TEST_VAR1_{GetUniqueName()}";
    private static readonly string s_environmentVariableNameUnset = $"TEST_VAR3_{GetUniqueName()}";

    static LaunchSettingsParserTests()
    {
        Environment.SetEnvironmentVariable(s_environmentVariableName1, "ENV_VALUE1");
        Environment.SetEnvironmentVariable(s_environmentVariableName2, "ENV_VALUE2");
    }

    // The same syntax works on Windows and Unix ($VAR does not get expanded Unix).
    private static string EnvironmentVariableReference(string name)
        => $"%{name}%";

    private static string GetUniqueName()
        => Guid.NewGuid().ToString("N");

    [Fact]
    public void MissingExecutablePath()
    {
        var parser = ExecutableLaunchProfileParser.Instance;

        Assert.Throws<JsonException>(() => parser.ParseProfile("path", "Execute", """
            {
                "commandName": "Executable"
            }
            """));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void DotNetRunMessages_Executable(string value, bool expected)
    {
        var parser = ExecutableLaunchProfileParser.Instance;

        var result = parser.ParseProfile("path", "name", $$"""
            {
                "commandName": "Executable",
                "executablePath": "executable",
                "dotnetRunMessages": {{value}}
            }
            """);

        Assert.True(result.Successful);
        Assert.NotNull(result.Profile);
        Assert.Equal(expected, result.Profile.DotNetRunMessages);
    }

    [Fact]
    public void DotNetRunMessages_Error_Executable()
    {
        var parser = ProjectLaunchProfileParser.Instance;

        Assert.Throws<JsonException>(() => parser.ParseProfile("path", "name", $$"""
            {
                "commandName": "Executable",
                "executablePath": "executable",
                "dotnetRunMessages": "true"
            }
            """));
    }

    [Fact]
    public void DotNetRunMessages_Error_Project()
    {
        var parser = ProjectLaunchProfileParser.Instance;

        Assert.Throws<JsonException>(() => parser.ParseProfile("path", "name", $$"""
            {
                "commandName": "Project",
                "dotnetRunMessages": "true"
            }
            """));
    }

    [Fact]
    public void EnvironmentVariableExpansion_Executable()
    {
        var root = Path.GetTempPath();
        var dir = Path.Combine(root, Guid.NewGuid().ToString());
        var launchSettingsPath = Path.Combine(dir, "launchSettings.json");

        var parser = ExecutableLaunchProfileParser.Instance;

        var settings = parser.ParseProfile(launchSettingsPath, "MyProfile", $$"""
            {
                "commandName": "Executable",
                "executablePath": "../path/{{EnvironmentVariableReference(s_environmentVariableName1)}}/executable",
                "commandLineArgs": "arg1 {{EnvironmentVariableReference(s_environmentVariableName1)}} arg3",
                "workingDirectory": "{{Path.Combine("..", EnvironmentVariableReference(s_environmentVariableName1)).Replace("\\", "\\\\")}}",
                "environmentVariables": {
                    "{{s_environmentVariableNameUnset}}": "{{EnvironmentVariableReference(s_environmentVariableName2)}}",
                    "VAR1": "{{EnvironmentVariableReference(s_environmentVariableNameUnset)}}",
                    "VAR2": "ENV_VALUE2"
                }
            }
            """);

        var model = Assert.IsType<ExecutableLaunchProfile>(settings.Profile);

        Assert.Equal("../path/ENV_VALUE1/executable", model.ExecutablePath);
        Assert.Equal(Path.Combine(root, "ENV_VALUE1"), model.WorkingDirectory);
        Assert.Equal("arg1 ENV_VALUE1 arg3", model.CommandLineArgs);
        Assert.Equal(
        [
            (s_environmentVariableNameUnset, "ENV_VALUE2"),
            ("VAR1", EnvironmentVariableReference(s_environmentVariableNameUnset)),
            ("VAR2", "ENV_VALUE2")
        ], model.EnvironmentVariables.OrderBy(e => e.Key).Select(e => (e.Key, e.Value)));
    }

    [Fact]
    public void EnvironmentVariableExpansion_Project()
    {
        var root = Path.GetTempPath();
        var dir = Path.Combine(root, Guid.NewGuid().ToString());
        var launchSettingsPath = Path.Combine(dir, "launchSettings.json");

        var parser = ProjectLaunchProfileParser.Instance;

        var settings = parser.ParseProfile(launchSettingsPath, "MyProfile", $$"""
            {
                "commandName": "Project",
                "commandLineArgs": "arg1 {{EnvironmentVariableReference(s_environmentVariableName1)}} arg3",
                "environmentVariables": {
                    "{{s_environmentVariableNameUnset}}": "{{EnvironmentVariableReference(s_environmentVariableName2)}}",
                    "VAR1": "{{EnvironmentVariableReference(s_environmentVariableNameUnset)}}",
                    "VAR2": "ENV_VALUE2"
                }
            }
            """);

        var model = Assert.IsType<ProjectLaunchProfile>(settings.Profile);

        Assert.Equal("arg1 ENV_VALUE1 arg3", model.CommandLineArgs);
        Assert.Equal(
        [
            (s_environmentVariableNameUnset, "ENV_VALUE2"),
            ("VAR1", EnvironmentVariableReference(s_environmentVariableNameUnset)),
            ("VAR2", "ENV_VALUE2")
        ], model.EnvironmentVariables.OrderBy(e => e.Key).Select(e => (e.Key, e.Value)));
    }
}
