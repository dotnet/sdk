// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class CreateNewImageTests
{
    [TestMethod]
    // Entrypoint, backwards compatibility.
    [DataRow("", "entrypointArg", "appCommand", "", "", null, new[] { "appCommand" }, new[] { "entrypointArg" })]
    // When no entrypoint is specified, emit the AppCommand as the Entrypoint.
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "appCommand", "appCommandArgs" }, new[] { "defaultArgs" })]
    // Set all properties. When an entrypoint is specified, emit the AppCommand as Cmd.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs",
                "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    public void EntrypointAndCmd_NoInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [TestMethod]
    // Set all properties.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs",
                                                                       "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // No Entrypoint, AppCommand specified, base entrypoint is preserved.
    [DataRow("", "", "appCommand", "", "", "", null, new[] { "appCommand" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "", "", null, new[] { "appCommand", "appCommandArgs" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "", null, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    [DataRow("", "", "appCommand", "", "", "baseEntrypoint", new[] { "baseEntrypoint" }, new[] { "appCommand" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "", "baseEntrypoint", new[] { "baseEntrypoint" }, new[] { "appCommand", "appCommandArgs" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "baseEntrypoint" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // No Entrypoint, AppCommand specified, 'dotnet' base entrypoint is ignored.
    [DataRow("", "", "appCommand", "", "", "dotnet", null, new[] { "appCommand" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "", "dotnet", null, new[] { "appCommand", "appCommandArgs" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "dotnet", null, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // No Entrypoint, AppCommand specified, '/usr/bin/dotnet' base entrypoint is ignored.
    [DataRow("", "", "appCommand", "", "", "/usr/bin/dotnet", null, new[] { "appCommand" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "", "/usr/bin/dotnet", null, new[] { "appCommand", "appCommandArgs" })]
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "/usr/bin/dotnet", null, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    public void EntrypointAndCmd_DefaultArgsInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("DefaultArgs", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [TestMethod]
    // Set all properties except entrypoint and entrypointArgs.
    [DataRow("", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "appCommand", "appCommandArgs" }, new[] { "defaultArgs" })]
    // Can't set entrypoint or entrypointArgs with instruction 'Entrypoint'.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [DataRow("", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [DataRow("entrypoint", "", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    public void EntrypointAndCmd_EntrypointInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("Entrypoint", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [TestMethod]
    // Set all properties except appCommand and appCommandArgs.
    [DataRow("entrypoint", "entrypointArgs", "", "", "defaultArgs", "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "defaultArgs" })]
    // Can't set appCommand or appCommandArgs with instruction 'None'.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [DataRow("entrypoint", "entrypointArgs", "", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "", "defaultArgs", "baseEntrypoint", null, null)]
    public void EntrypointAndCmd_NoneInstruction(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("None", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    [TestMethod]
    // Set all properties accepted.
    [DataRow("entrypoint", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", new[] { "entrypoint", "entrypointArgs" }, new[] { "appCommand", "appCommandArgs", "defaultArgs" })]
    // Set all properties except entrypoint fails: can't set entrypointArgs without setting entrypoint.
    [DataRow("", "entrypointArgs", "appCommand", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    // Set all properties except appCommand fails: can't set appCommandArgs without setting appCommand.
    [DataRow("entrypoint", "entrypointArgs", "", "appCommandArgs", "defaultArgs", "baseEntrypoint", null, null)]
    public void EntrypointAndCmd_RequiredProperties(string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
        => ValidateArgsAndCmd("DefaultArgs", entrypoint, entrypointArgs, appCommand, appCommandArgs, defaultArgs, baseImageEntrypoint, expectedEntrypoint, expectedCmd);

    private static void ValidateArgsAndCmd(string appCommandInstruction, string entrypoint, string entrypointArgs, string appCommand, string appCommandArgs, string defaultArgs, string? baseImageEntrypoint, string[]? expectedEntrypoint, string[]? expectedCmd)
    {
        var newImage = new CreateNewImage()
        {
            Entrypoint = CreateTaskItems(entrypoint),
            EntrypointArgs = CreateTaskItems(entrypointArgs),
            DefaultArgs = CreateTaskItems(defaultArgs),
            AppCommand = CreateTaskItems(appCommand),
            AppCommandArgs = CreateTaskItems(appCommandArgs),
            AppCommandInstruction = appCommandInstruction,
            BuildEngine = new Mock<IBuildEngine>().Object
        };

        (string[] imageEntrypoint, string[] imageCmd) = newImage.DetermineEntrypointAndCmd(baseImageEntrypoint?.Split(';', StringSplitOptions.RemoveEmptyEntries));

        Assert.AreEqual(newImage.Log.HasLoggedErrors, imageEntrypoint.Length == 0 && imageCmd.Length == 0);
        Assert.AreSequenceEqual(expectedEntrypoint ?? Array.Empty<string>(), imageEntrypoint);
        Assert.AreSequenceEqual(expectedCmd ?? Array.Empty<string>(), imageCmd);

        static ITaskItem[] CreateTaskItems(string value)
            => value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => new TaskItem(s)).ToArray();
    }
}
