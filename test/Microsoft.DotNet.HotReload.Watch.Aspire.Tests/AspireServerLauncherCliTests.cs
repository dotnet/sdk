// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

public class AspireServerLauncherCliTests
{
    [Fact]
    public void RequiredServerOption()
    {
        // --server option is missing
        var args = new[] { "server", "--sdk", "sdk" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void RequiredSdkOption()
    {
        // --sdk option is missing
        var args = new[] { "server", "--server", "pipe1" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void MinimalRequiredOptions()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk" };
        var launcher = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("pipe1", launcher.ServerPipeName);
        Assert.Equal(LogLevel.Information, launcher.GlobalOptions.LogLevel);
        Assert.Empty(launcher.ResourcePaths);
        Assert.Null(launcher.StatusPipeName);
        Assert.Null(launcher.ControlPipeName);
    }

    [Fact]
    public void ResourceOption_SingleValue()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--resource", "proj1.csproj" };
        var launcher = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        AssertEx.SequenceEqual(["proj1.csproj"], launcher.ResourcePaths);
    }

    [Fact]
    public void ResourceOption_MultipleValues()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--resource", "proj1.csproj", "proj2.csproj", "file.cs" };
        var launcher = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        AssertEx.SequenceEqual(["proj1.csproj", "proj2.csproj", "file.cs"], launcher.ResourcePaths);
    }

    [Fact]
    public void ResourceOption_MultipleFlags()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--resource", "proj1.csproj", "--resource", "proj2.csproj" };
        var launcher = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        AssertEx.SequenceEqual(["proj1.csproj", "proj2.csproj"], launcher.ResourcePaths);
    }

    [Fact]
    public void StatusPipeOption()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--status-pipe", "status1" };
        var launcher = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("status1", launcher.StatusPipeName);
    }

    [Fact]
    public void ControlPipeOption()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--control-pipe", "control1" };
        var launcher = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("control1", launcher.ControlPipeName);
    }

    [Fact]
    public void VerboseOption()
    {
        // With verbose flag
        var argsVerbose = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--verbose" };
        var launcherVerbose = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(argsVerbose));
        Assert.Equal(LogLevel.Debug, launcherVerbose.GlobalOptions.LogLevel);

        // Without verbose flag
        var argsNotVerbose = new[] { "server", "--server", "pipe1", "--sdk", "sdk" };
        var launcherNotVerbose = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(argsNotVerbose));
        Assert.Equal(LogLevel.Information, launcherNotVerbose.GlobalOptions.LogLevel);
    }

    [Fact]
    public void QuietOption()
    {
        // With quiet flag
        var argsQuiet = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--quiet" };
        var launcherQuiet = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(argsQuiet));
        Assert.Equal(LogLevel.Warning, launcherQuiet.GlobalOptions.LogLevel);

        // Without quiet flag
        var argsNotQuiet = new[] { "server", "--server", "pipe1", "--sdk", "sdk" };
        var launcherNotQuiet = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(argsNotQuiet));
        Assert.Equal(LogLevel.Information, launcherNotQuiet.GlobalOptions.LogLevel);
    }

    [Fact]
    public void ConflictingOptions()
    {
        // Cannot specify both --quiet and --verbose
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--quiet", "--verbose" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void AllOptionsSet()
    {
        var args = new[] { "server", "--server", "pipe1", "--sdk", "sdk", "--resource", "proj1.csproj", "proj2.csproj", "--status-pipe", "status1", "--control-pipe", "control1", "--verbose" };
        var launcher = Assert.IsType<AspireServerLauncher>(AspireLauncher.TryCreate(args));

        Assert.Equal("pipe1", launcher.ServerPipeName);
        Assert.Equal(LogLevel.Debug, launcher.GlobalOptions.LogLevel);
        AssertEx.SequenceEqual(["proj1.csproj", "proj2.csproj"], launcher.ResourcePaths);
        Assert.Equal("status1", launcher.StatusPipeName);
        Assert.Equal("control1", launcher.ControlPipeName);
    }
}
