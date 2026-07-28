// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

public class AspireHostLauncherCliTests
{
    [Fact]
    public void RequiredSdkOption()
    {
        // --sdk option is missing
        var args = new[] { "host", "--entrypoint", "proj", "a", "b" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void RequiredEntryPointOption()
    {
        // --entrypoint option is missing
        var args = new[] { "host", "--sdk", "sdk", "--verbose" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void ProjectAndSdkPaths()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "myproject.csproj" };
        var launcher = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("sdk", launcher.EnvironmentOptions.SdkDirectory);
        Assert.True(launcher.EntryPoint.IsProjectFile);
        Assert.Equal("myproject.csproj", launcher.EntryPoint.PhysicalPath);
        Assert.Empty(launcher.ApplicationArguments);
        Assert.Equal(LogLevel.Information, launcher.GlobalOptions.LogLevel);
    }

    [Fact]
    public void FilePath()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "file.cs" };
        var launcher = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("sdk", launcher.EnvironmentOptions.SdkDirectory);
        Assert.False(launcher.EntryPoint.IsProjectFile);
        Assert.Equal("file.cs", launcher.EntryPoint.EntryPointFilePath);
        Assert.Empty(launcher.ApplicationArguments);
        Assert.Equal(LogLevel.Information, launcher.GlobalOptions.LogLevel);
    }

    [Fact]
    public void ApplicationArguments()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--verbose", "a", "b" };
        var launcher = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        AssertEx.SequenceEqual(["a", "b"], launcher.ApplicationArguments);
        Assert.Equal(LogLevel.Debug, launcher.GlobalOptions.LogLevel);
    }

    [Fact]
    public void VerboseOption()
    {
        // With verbose flag
        var argsVerbose = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--verbose" };
        var launcherVerbose = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(argsVerbose));
        Assert.Equal(LogLevel.Debug, launcherVerbose.GlobalOptions.LogLevel);

        // Without verbose flag
        var argsNotVerbose = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj" };
        var launcherNotVerbose = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(argsNotVerbose));
        Assert.Equal(LogLevel.Information, launcherNotVerbose.GlobalOptions.LogLevel);
    }

    [Fact]
    public void QuietOption()
    {
        // With quiet flag
        var argsQuiet = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--quiet" };
        var launcherQuiet = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(argsQuiet));
        Assert.Equal(LogLevel.Warning, launcherQuiet.GlobalOptions.LogLevel);

        // Without quiet flag
        var argsNotQuiet = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj" };
        var launcherNotQuiet = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(argsNotQuiet));
        Assert.Equal(LogLevel.Information, launcherNotQuiet.GlobalOptions.LogLevel);
    }

    [Fact]
    public void NoLaunchProfileOption()
    {
        // With no-launch-profile flag
        var argsNoProfile = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--no-launch-profile" };
        var launcherNoProfile = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(argsNoProfile));
        Assert.False(launcherNoProfile.LaunchProfileName.HasValue);

        // Without no-launch-profile flag
        var argsDefault = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj" };
        var launcherDefault = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(argsDefault));
        Assert.True(launcherDefault.LaunchProfileName.HasValue);
        Assert.Null(launcherDefault.LaunchProfileName.Value);
    }

    [Fact]
    public void LaunchProfileOption()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--launch-profile", "MyProfile" };
        var launcher = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        Assert.True(launcher.LaunchProfileName.HasValue);
        Assert.Equal("MyProfile", launcher.LaunchProfileName.Value);
    }

    [Fact]
    public void ConflictingOptions()
    {
        // Cannot specify both --quiet and --verbose
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--quiet", "--verbose" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void EntryPoint_MultipleValues()
    {
        // EntryPoint option should only accept one value; extra values become application arguments
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj1", "proj2" };
        var launcher = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("proj1", launcher.EntryPoint.ProjectOrEntryPointFilePath);
        AssertEx.SequenceEqual(["proj2"], launcher.ApplicationArguments);
    }

    [Fact]
    public void AllOptionsSet()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "myapp.csproj", "--verbose", "--no-launch-profile", "arg1", "arg2", "arg3" };
        var launcher = Assert.IsType<AspireHostLauncher>(AspireLauncher.TryCreate(args));

        Assert.True(launcher.EntryPoint.IsProjectFile);
        Assert.Equal("myapp.csproj", launcher.EntryPoint.PhysicalPath);
        Assert.Equal("sdk", launcher.EnvironmentOptions.SdkDirectory);
        Assert.Equal(LogLevel.Debug, launcher.GlobalOptions.LogLevel);
        Assert.False(launcher.LaunchProfileName.HasValue);
        AssertEx.SequenceEqual(["arg1", "arg2", "arg3"], launcher.ApplicationArguments);
    }
}
