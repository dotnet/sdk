// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class AspireServerLauncherCliTests
{
    [TestMethod]
    public void RequiredServerOption()
    {
        // --server option is missing
        var args = new[] { "server", "--sdk", "sdk" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.IsNull(launcher);
    }

    [TestMethod]
    public void RequiredSdkOption()
    {
        // --sdk option is missing
        var args = new[] { "server", "--server", "pipe1" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.IsNull(launcher);
    }

    [TestMethod]
    public void MinimalRequiredOptions()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk" };
        var launcher = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("pipe1", launcher.ServerPipeName);
        Assert.AreEqual(LogLevel.Information, launcher.GlobalOptions.LogLevel);
        Assert.IsEmpty(launcher.ResourcePaths);
        Assert.IsNull(launcher.StatusPipeName);
        Assert.IsNull(launcher.ControlPipeName);
    }

    [TestMethod]
    public void ResourceOption_SingleValue()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--resource", "proj1.csproj" };
        var launcher = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        AssertEx.SequenceEqual(["proj1.csproj"], launcher.ResourcePaths);
    }

    [TestMethod]
    public void ResourceOption_MultipleValues()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--resource", "proj1.csproj", "proj2.csproj", "file.cs" };
        var launcher = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        AssertEx.SequenceEqual(["proj1.csproj", "proj2.csproj", "file.cs"], launcher.ResourcePaths);
    }

    [TestMethod]
    public void ResourceOption_MultipleFlags()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--resource", "proj1.csproj", "--resource", "proj2.csproj" };
        var launcher = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        AssertEx.SequenceEqual(["proj1.csproj", "proj2.csproj"], launcher.ResourcePaths);
    }

    [TestMethod]
    public void StatusPipeOption()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--status-pipe", "status1" };
        var launcher = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("status1", launcher.StatusPipeName);
    }

    [TestMethod]
    public void ControlPipeOption()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--control-pipe", "control1" };
        var launcher = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("control1", launcher.ControlPipeName);
    }

    [TestMethod]
    public void VerboseOption()
    {
        // With verbose flag
        var argsVerbose = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--verbose" };
        var launcherVerbose = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(argsVerbose));
        Assert.AreEqual(LogLevel.Debug, launcherVerbose.GlobalOptions.LogLevel);

        // Without verbose flag
        var argsNotVerbose = new[] { "server", "--server", "pipe1", "--sdk", "sdk" };
        var launcherNotVerbose = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(argsNotVerbose));
        Assert.AreEqual(LogLevel.Information, launcherNotVerbose.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void QuietOption()
    {
        // With quiet flag
        var argsQuiet = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--quiet" };
        var launcherQuiet = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(argsQuiet));
        Assert.AreEqual(LogLevel.Warning, launcherQuiet.GlobalOptions.LogLevel);

        // Without quiet flag
        var argsNotQuiet = new[] { "server", "--server", "pipe1", "--sdk", "sdk" };
        var launcherNotQuiet = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(argsNotQuiet));
        Assert.AreEqual(LogLevel.Information, launcherNotQuiet.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void ConflictingOptions()
    {
        // Cannot specify both --quiet and --verbose
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--quiet", "--verbose" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.IsNull(launcher);
    }

    [TestMethod]
    public void AllOptionsSet()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--resource", "proj1.csproj", "proj2.csproj", "--status-pipe", "status1", "--control-pipe", "control1", "--verbose" };
        var launcher = Assert.IsInstanceOfType<AspireServerLauncher>(AspireLauncher.TryCreate(args));

        Assert.AreEqual("pipe1", launcher.ServerPipeName);
        Assert.AreEqual(LogLevel.Debug, launcher.GlobalOptions.LogLevel);
        AssertEx.SequenceEqual(["proj1.csproj", "proj2.csproj"], launcher.ResourcePaths);
        Assert.AreEqual("status1", launcher.StatusPipeName);
        Assert.AreEqual("control1", launcher.ControlPipeName);
    }
}