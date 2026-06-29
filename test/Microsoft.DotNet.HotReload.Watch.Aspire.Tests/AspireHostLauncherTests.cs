// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
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
        Assert.IsTrue(options.IsMainProject);
        Assert.AreEqual("run", options.Command);
        Assert.AreEqual(launcher.EntryPoint, options.Representation);
        Assert.IsEmpty(options.LaunchEnvironmentVariables);
    }

    [TestMethod]
    public void GetProjectOptions_ProjectFile_UsesProjectFlag()
    {
        var launcher = CreateLauncher("myapp.csproj");

        var options = launcher.GetHostProjectOptions()!;

        AssertCommonProperties(options, launcher);
        Assert.IsFalse(options.LaunchProfileName.HasValue);
        Assert.AreSequenceEqual(["--project", "myapp.csproj", "--no-launch-profile"], options.CommandArguments);
    }

    [TestMethod]
    public void GetProjectOptions_EntryPointFile_UsesFileFlag()
    {
        var launcher = CreateLauncher("Program.cs");

        var options = launcher.GetHostProjectOptions()!;

        AssertCommonProperties(options, launcher);
        Assert.IsFalse(options.LaunchProfileName.HasValue);
        Assert.AreSequenceEqual(["--file", "Program.cs", "--no-launch-profile"], options.CommandArguments);
    }

    [TestMethod]
    public void GetProjectOptions_WithLaunchProfile_AddsLaunchProfileArguments()
    {
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: "MyProfile");

        var options = launcher.GetHostProjectOptions()!;

        AssertCommonProperties(options, launcher);
        Assert.IsTrue(options.LaunchProfileName.HasValue);
        Assert.AreEqual("MyProfile", options.LaunchProfileName.Value);
        Assert.AreSequenceEqual(["--project", "myapp.csproj", "--launch-profile", "MyProfile"], options.CommandArguments);
    }

    [TestMethod]
    public void GetProjectOptions_NoLaunchProfile_AddsNoLaunchProfileFlag()
    {
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: Optional<string?>.NoValue);

        var options = launcher.GetHostProjectOptions()!;

        AssertCommonProperties(options, launcher);
        Assert.IsFalse(options.LaunchProfileName.HasValue);
        Assert.AreSequenceEqual(["--project", "myapp.csproj", "--no-launch-profile"], options.CommandArguments);
    }

    [TestMethod]
    public void GetProjectOptions_NullLaunchProfile_UsesDefault()
    {
        // null value (HasValue=true) means use default launch profile - no --launch-profile or --no-launch-profile flag
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: (string?)null);

        var options = launcher.GetHostProjectOptions()!;

        AssertCommonProperties(options, launcher);
        Assert.IsTrue(options.LaunchProfileName.HasValue);
        Assert.IsNull(options.LaunchProfileName.Value);
        Assert.AreSequenceEqual(["--project", "myapp.csproj"], options.CommandArguments);
    }

    [TestMethod]
    public void GetProjectOptions_WithApplicationArguments_AppendsArguments()
    {
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: "Profile", applicationArguments: ["arg1", "arg2"]);

        var options = launcher.GetHostProjectOptions()!;

        AssertCommonProperties(options, launcher);
        Assert.IsTrue(options.LaunchProfileName.HasValue);
        Assert.AreEqual("Profile", options.LaunchProfileName.Value);
        Assert.AreSequenceEqual(["--project", "myapp.csproj", "--launch-profile", "Profile", "arg1", "arg2"], options.CommandArguments);
    }

    [TestMethod]
    public void GetProjectOptions_SetsCustomWorkingDirectory()
    {
        var launcher = CreateLauncher("myapp.csproj", workingDirectory: "/custom/path");

        var options = launcher.GetHostProjectOptions()!;

        AssertCommonProperties(options, launcher);
        Assert.AreEqual("/custom/path", options.WorkingDirectory);
    }

    [TestMethod]
    public void GetProjectOptions_EntryPointFile_WithLaunchProfileAndArguments()
    {
        var launcher = CreateLauncher("Program.cs", launchProfileName: "Dev", applicationArguments: ["--port", "8080"]);

        var options = launcher.GetHostProjectOptions()!;

        AssertCommonProperties(options, launcher);
        Assert.IsTrue(options.LaunchProfileName.HasValue);
        Assert.AreEqual("Dev", options.LaunchProfileName.Value);
        Assert.AreSequenceEqual(["--file", "Program.cs", "--launch-profile", "Dev", "--port", "8080"], options.CommandArguments);
    }

    [TestMethod]
    public void GetProjectOptions_NoLaunchProfile_WithApplicationArguments()
    {
        var launcher = CreateLauncher("myapp.csproj", launchProfileName: Optional<string?>.NoValue, applicationArguments: ["--urls", "http://localhost:5000"]);

        var options = launcher.GetHostProjectOptions();

        AssertCommonProperties(options, launcher);
        Assert.IsFalse(options.LaunchProfileName.HasValue);
        Assert.AreSequenceEqual(["--project", "myapp.csproj", "--no-launch-profile", "--urls", "http://localhost:5000"], options.CommandArguments);
    }
}