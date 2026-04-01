// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

public class AspireHostLauncherTests
{
    private static AspireHostLauncher CreateLauncher(
        string entryPointPath,
        Optional<string?> launchProfileName = default,
        ImmutableArray<string> applicationArguments = default,
        string? workingDirectory = null,
        string? sdkDirectory = null)
    {
        return new AspireHostLauncher(
            globalOptions: new GlobalOptions()
            {
                LogLevel = LogLevel.Information,
                NoHotReload = false,
                NonInteractive = true,
            },
            environmentOptions: new EnvironmentOptions(
                WorkingDirectory: workingDirectory ?? "/work",
                SdkDirectory: sdkDirectory ?? "/sdk",
                LogMessagePrefix: AspireHostLauncher.LogMessagePrefix),
            entryPoint: ProjectRepresentation.FromProjectOrEntryPointFilePath(entryPointPath),
            applicationArguments: applicationArguments.IsDefault ? [] : applicationArguments,
            launchProfileName: launchProfileName);
    }

    private static void AssertCommonProperties(ProjectOptions options, AspireHostLauncher launcher)
    {
        Assert.True(options.IsMainProject);
        Assert.Equal("run", options.Command);
        Assert.Equal(launcher.EntryPoint, options.Representation);
        Assert.Empty(options.LaunchEnvironmentVariables);
    }

    [Fact]
    public void GetProjectOptions_ProjectFile_UsesProjectFlag()
    {
        var launcher = CreateLauncher("myapp.csproj");

        var options = launcher.GetProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.False(options.LaunchProfileName.HasValue);
        AssertEx.SequenceEqual(["--project", "myapp.csproj", "--no-launch-profile"], options.CommandArguments);
    }

    [Fact]
    public void GetProjectOptions_EntryPointFile_UsesFileFlag()
    {
        var launcher = CreateLauncher("Program.cs");

        var options = launcher.GetProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.False(options.LaunchProfileName.HasValue);
        AssertEx.SequenceEqual(["--file", "Program.cs", "--no-launch-profile"], options.CommandArguments);
    }

    [Fact]
    public void GetProjectOptions_WithLaunchProfile_AddsLaunchProfileArguments()
    {
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: "MyProfile");

        var options = launcher.GetProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.True(options.LaunchProfileName.HasValue);
        Assert.Equal("MyProfile", options.LaunchProfileName.Value);
        AssertEx.SequenceEqual(["--project", "myapp.csproj", "--launch-profile", "MyProfile"], options.CommandArguments);
    }

    [Fact]
    public void GetProjectOptions_NoLaunchProfile_AddsNoLaunchProfileFlag()
    {
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: Optional<string?>.NoValue);

        var options = launcher.GetProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.False(options.LaunchProfileName.HasValue);
        AssertEx.SequenceEqual(["--project", "myapp.csproj", "--no-launch-profile"], options.CommandArguments);
    }

    [Fact]
    public void GetProjectOptions_NullLaunchProfile_UsesDefault()
    {
        // null value (HasValue=true) means use default launch profile - no --launch-profile or --no-launch-profile flag
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: (string?)null);

        var options = launcher.GetProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.True(options.LaunchProfileName.HasValue);
        Assert.Null(options.LaunchProfileName.Value);
        AssertEx.SequenceEqual(["--project", "myapp.csproj"], options.CommandArguments);
    }

    [Fact]
    public void GetProjectOptions_WithApplicationArguments_AppendsArguments()
    {
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: "Profile", applicationArguments: ["arg1", "arg2"]);

        var options = launcher.GetProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.True(options.LaunchProfileName.HasValue);
        Assert.Equal("Profile", options.LaunchProfileName.Value);
        AssertEx.SequenceEqual(["--project", "myapp.csproj", "--launch-profile", "Profile", "arg1", "arg2"], options.CommandArguments);
    }

    [Fact]
    public void GetProjectOptions_SetsCustomWorkingDirectory()
    {
        var launcher = CreateLauncher("myapp.csproj", workingDirectory: "/custom/path");

        var options = launcher.GetProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.Equal("/custom/path", options.WorkingDirectory);
    }

    [Fact]
    public void GetProjectOptions_EntryPointFile_WithLaunchProfileAndArguments()
    {
        var launcher = CreateLauncher("Program.cs", launchProfileName: "Dev", applicationArguments: ["--port", "8080"]);

        var options = launcher.GetProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.True(options.LaunchProfileName.HasValue);
        Assert.Equal("Dev", options.LaunchProfileName.Value);
        AssertEx.SequenceEqual(["--file", "Program.cs", "--launch-profile", "Dev", "--port", "8080"], options.CommandArguments);
    }

    [Fact]
    public void GetProjectOptions_NoLaunchProfile_WithApplicationArguments()
    {
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: Optional<string?>.NoValue, applicationArguments: ["--urls", "http://localhost:5000"]);

        var options = launcher.GetProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.False(options.LaunchProfileName.HasValue);
        AssertEx.SequenceEqual(["--project", "myapp.csproj", "--no-launch-profile", "--urls", "http://localhost:5000"], options.CommandArguments);
    }
}
