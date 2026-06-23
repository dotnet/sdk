// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ProjectTools.Tests;

[TestClass]
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

    [TestMethod]
    public void MissingExecutablePath()
    {
        var parser = ExecutableLaunchProfileParser.Instance;

        Assert.ThrowsExactly<JsonException>(() => parser.ParseProfile("path", "Execute", """
            {
                "commandName": "Executable"
            }
            """));
    }

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("false", false)]
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

        Assert.IsTrue(result.Successful);
        Assert.IsNotNull(result.Profile);
        Assert.AreEqual(expected, result.Profile.DotNetRunMessages);
    }

    [TestMethod]
    public void DotNetRunMessages_Error_Executable()
    {
        var parser = ProjectLaunchProfileParser.Instance;

        Assert.ThrowsExactly<JsonException>(() => parser.ParseProfile("path", "name", $$"""
            {
                "commandName": "Executable",
                "executablePath": "executable",
                "dotnetRunMessages": "true"
            }
            """));
    }

    [TestMethod]
    public void DotNetRunMessages_Error_Project()
    {
        var parser = ProjectLaunchProfileParser.Instance;

        Assert.ThrowsExactly<JsonException>(() => parser.ParseProfile("path", "name", $$"""
            {
                "commandName": "Project",
                "dotnetRunMessages": "true"
            }
            """));
    }

    [TestMethod]
    public void CommentsAndTrailingCommas_Executable()
    {
        var parser = ExecutableLaunchProfileParser.Instance;

        var result = parser.ParseProfile("path", "name", """
            {
                // line comment
                "commandName": "Executable",
                "executablePath": "executable", /* block comment */
                "environmentVariables": {
                    "VAR1": "VALUE1", // trailing comma below
                },
            }
            """);

        Assert.IsTrue(result.Successful);
        var model = Assert.IsExactInstanceOfType<ExecutableLaunchProfile>(result.Profile);
        Assert.AreEqual("executable", model.ExecutablePath);
        Assert.AreSequenceEqual([("VAR1", "VALUE1")], model.EnvironmentVariables.Select(e => (e.Key, e.Value)));
    }

    [TestMethod]
    public void CommentsAndTrailingCommas_Project()
    {
        var parser = ProjectLaunchProfileParser.Instance;

        var result = parser.ParseProfile("path", "name", """
            {
                // line comment
                "commandName": "Project",
                "commandLineArgs": "arg1", /* block comment */
                "environmentVariables": {
                    "VAR1": "VALUE1", // trailing comma below
                },
            }
            """);

        Assert.IsTrue(result.Successful);
        var model = Assert.IsExactInstanceOfType<ProjectLaunchProfile>(result.Profile);
        Assert.AreEqual("arg1", model.CommandLineArgs);
        Assert.AreSequenceEqual([("VAR1", "VALUE1")], model.EnvironmentVariables.Select(e => (e.Key, e.Value)));
    }

    [TestMethod]
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

        var model = Assert.IsExactInstanceOfType<ExecutableLaunchProfile>(settings.Profile);

        Assert.AreEqual("../path/ENV_VALUE1/executable", model.ExecutablePath);
        Assert.AreEqual(Path.Combine(root, "ENV_VALUE1"), model.WorkingDirectory);
        Assert.AreEqual("arg1 ENV_VALUE1 arg3", model.CommandLineArgs);
        Assert.AreSequenceEqual(
        [
            (s_environmentVariableNameUnset, "ENV_VALUE2"),
            ("VAR1", EnvironmentVariableReference(s_environmentVariableNameUnset)),
            ("VAR2", "ENV_VALUE2")
        ], model.EnvironmentVariables.OrderBy(e => e.Key).Select(e => (e.Key, e.Value)));
    }

    [TestMethod]
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

        var model = Assert.IsExactInstanceOfType<ProjectLaunchProfile>(settings.Profile);

        Assert.AreEqual("arg1 ENV_VALUE1 arg3", model.CommandLineArgs);
        Assert.AreSequenceEqual(
        [
            (s_environmentVariableNameUnset, "ENV_VALUE2"),
            ("VAR1", EnvironmentVariableReference(s_environmentVariableNameUnset)),
            ("VAR2", "ENV_VALUE2")
        ], model.EnvironmentVariables.OrderBy(e => e.Key).Select(e => (e.Key, e.Value)));
    }
}
