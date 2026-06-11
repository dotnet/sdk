// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class AspireHostLauncherCliTests
{
    [TestMethod]
    public void RequiredSdkOption()
    {
        // --sdk option is missing
        var args = new[] { "host", "--entrypoint", "proj", "a", "b" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.IsNull(launcher);
    }

    [TestMethod]
    public void RequiredEntryPointOption()
    {
        // --entrypoint option is missing
        var args = new[] { "host", "--sdk", "sdk", "--verbose" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.IsNull(launcher);
    }

    [TestMethod]
    public void ProjectAndSdkPaths()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "myproject.csproj" };
        var launcher = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("sdk", launcher.EnvironmentOptions.SdkDirectory);
        Assert.IsTrue(launcher.EntryPoint.IsProjectFile);
        Assert.AreEqual("myproject.csproj", launcher.EntryPoint.PhysicalPath);
        Assert.IsEmpty(launcher.ApplicationArguments);
        Assert.AreEqual(LogLevel.Information, launcher.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void FilePath()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "file.cs" };
        var launcher = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("sdk", launcher.EnvironmentOptions.SdkDirectory);
        Assert.IsFalse(launcher.EntryPoint.IsProjectFile);
        Assert.AreEqual("file.cs", launcher.EntryPoint.EntryPointFilePath);
        Assert.IsEmpty(launcher.ApplicationArguments);
        Assert.AreEqual(LogLevel.Information, launcher.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void ApplicationArguments()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--verbose", "a", "b" };
        var launcher = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        AssertEx.SequenceEqual(["a", "b"], launcher.ApplicationArguments);
        Assert.AreEqual(LogLevel.Debug, launcher.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void VerboseOption()
    {
        // With verbose flag
        var argsVerbose = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--verbose" };
        var launcherVerbose = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(argsVerbose));
        Assert.AreEqual(LogLevel.Debug, launcherVerbose.GlobalOptions.LogLevel);

        // Without verbose flag
        var argsNotVerbose = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj" };
        var launcherNotVerbose = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(argsNotVerbose));
        Assert.AreEqual(LogLevel.Information, launcherNotVerbose.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void QuietOption()
    {
        // With quiet flag
        var argsQuiet = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--quiet" };
        var launcherQuiet = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(argsQuiet));
        Assert.AreEqual(LogLevel.Warning, launcherQuiet.GlobalOptions.LogLevel);

        // Without quiet flag
        var argsNotQuiet = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj" };
        var launcherNotQuiet = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(argsNotQuiet));
        Assert.AreEqual(LogLevel.Information, launcherNotQuiet.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void NoLaunchProfileOption()
    {
        // With no-launch-profile flag
        var argsNoProfile = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--no-launch-profile" };
        var launcherNoProfile = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(argsNoProfile));
        Assert.IsFalse(launcherNoProfile.LaunchProfileName.HasValue);

        // Without no-launch-profile flag
        var argsDefault = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj" };
        var launcherDefault = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(argsDefault));
        Assert.IsTrue(launcherDefault.LaunchProfileName.HasValue);
        Assert.IsNull(launcherDefault.LaunchProfileName.Value);
    }

    [TestMethod]
    public void LaunchProfileOption()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--launch-profile", "MyProfile" };
        var launcher = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        Assert.IsTrue(launcher.LaunchProfileName.HasValue);
        Assert.AreEqual("MyProfile", launcher.LaunchProfileName.Value);
    }

    [TestMethod]
    public void ConflictingOptions()
    {
        // Cannot specify both --quiet and --verbose
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj", "--quiet", "--verbose" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.IsNull(launcher);
    }

    [TestMethod]
    public void EntryPoint_MultipleValues()
    {
        // EntryPoint option should only accept one value; extra values become application arguments
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "proj1", "proj2" };
        var launcher = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("proj1", launcher.EntryPoint.ProjectOrEntryPointFilePath);
        AssertEx.SequenceEqual(["proj2"], launcher.ApplicationArguments);
    }

    [TestMethod]
    public void AllOptionsSet()
    {
        var args = new[] { "host", "--sdk", "sdk", "--entrypoint", "myapp.csproj", "--verbose", "--no-launch-profile", "arg1", "arg2", "arg3" };
        var launcher = Assert.IsExactInstanceOfType<AspireHostLauncher>(AspireLauncher.TryCreate(args));

        Assert.IsTrue(launcher.EntryPoint.IsProjectFile);
        Assert.AreEqual("myapp.csproj", launcher.EntryPoint.PhysicalPath);
        Assert.AreEqual("sdk", launcher.EnvironmentOptions.SdkDirectory);
        Assert.AreEqual(LogLevel.Debug, launcher.GlobalOptions.LogLevel);
        Assert.IsFalse(launcher.LaunchProfileName.HasValue);
        AssertEx.SequenceEqual(["arg1", "arg2", "arg3"], launcher.ApplicationArguments);
    }
}