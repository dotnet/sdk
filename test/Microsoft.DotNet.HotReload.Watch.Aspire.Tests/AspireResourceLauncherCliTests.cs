// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class AspireResourceLauncherCliTests
{
    [TestMethod]
    public void RequiredServerOption()
    {
        // --server option is missing
        var args = new[] { "resource", "--entrypoint", "proj" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.IsNull(launcher);
    }

    [TestMethod]
    public void RequiredEntryPointOption()
    {
        // --entrypoint option is missing
        var args = new[] { "resource", "--server", "pipe1" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.IsNull(launcher);
    }

    [TestMethod]
    public void MinimalRequiredOptions()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj.csproj" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("pipe1", launcher.ServerPipeName);
        Assert.AreEqual("proj.csproj", launcher.EntryPoint);
        Assert.IsEmpty(launcher.ApplicationArguments);
        Assert.IsEmpty(launcher.EnvironmentVariables);
        Assert.IsTrue(launcher.LaunchProfileName.HasValue);
        Assert.IsNull(launcher.LaunchProfileName.Value);
        Assert.AreEqual(LogLevel.Information, launcher.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void ApplicationArguments()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "a", "b" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreSequenceEqual(["a", "b"], launcher.ApplicationArguments);
    }

    [TestMethod]
    public void EnvironmentOption_SingleVariable()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "KEY=value" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.ContainsSingle(launcher.EnvironmentVariables);
        Assert.AreEqual("value", launcher.EnvironmentVariables["KEY"]);
    }

    [TestMethod]
    public void EnvironmentOption_MultipleVariables()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "KEY1=val1", "-e", "KEY2=val2" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.HasCount(2, launcher.EnvironmentVariables);
        Assert.AreEqual("val1", launcher.EnvironmentVariables["KEY1"]);
        Assert.AreEqual("val2", launcher.EnvironmentVariables["KEY2"]);
    }

    [TestMethod]
    public void EnvironmentOption_ValueWithEquals()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "CONN=Server=localhost;Port=5432" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("Server=localhost;Port=5432", launcher.EnvironmentVariables["CONN"]);
    }

    [TestMethod]
    public void EnvironmentOption_EmptyValue()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "KEY=" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("", launcher.EnvironmentVariables["KEY"]);
    }

    [TestMethod]
    public void EnvironmentOption_NoEquals()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "KEY" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.AreEqual("", launcher.EnvironmentVariables["KEY"]);
    }

    [TestMethod]
    public void NoLaunchProfileOption()
    {
        // With no-launch-profile flag
        var argsNoProfile = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--no-launch-profile" };
        var launcherNoProfile = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsNoProfile));
        Assert.IsFalse(launcherNoProfile.LaunchProfileName.HasValue);

        // Without no-launch-profile flag
        var argsDefault = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj" };
        var launcherDefault = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsDefault));
        Assert.IsTrue(launcherDefault.LaunchProfileName.HasValue);
        Assert.IsNull(launcherDefault.LaunchProfileName.Value);
    }

    [TestMethod]
    public void LaunchProfileOption()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--launch-profile", "MyProfile" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.IsTrue(launcher.LaunchProfileName.HasValue);
        Assert.AreEqual("MyProfile", launcher.LaunchProfileName.Value);
    }

    [TestMethod]
    public void LaunchProfileOption_ShortForm()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-lp", "MyProfile" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.IsTrue(launcher.LaunchProfileName.HasValue);
        Assert.AreEqual("MyProfile", launcher.LaunchProfileName.Value);
    }

    [TestMethod]
    public void VerboseOption()
    {
        var argsVerbose = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--verbose" };
        var launcherVerbose = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsVerbose));
        Assert.AreEqual(LogLevel.Debug, launcherVerbose.GlobalOptions.LogLevel);

        var argsNotVerbose = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj" };
        var launcherNotVerbose = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsNotVerbose));
        Assert.AreEqual(LogLevel.Information, launcherNotVerbose.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void QuietOption()
    {
        var argsQuiet = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--quiet" };
        var launcherQuiet = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsQuiet));
        Assert.AreEqual(LogLevel.Warning, launcherQuiet.GlobalOptions.LogLevel);

        var argsNotQuiet = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj" };
        var launcherNotQuiet = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsNotQuiet));
        Assert.AreEqual(LogLevel.Information, launcherNotQuiet.GlobalOptions.LogLevel);
    }

    [TestMethod]
    public void ConflictingOptions()
    {
        // Cannot specify both --quiet and --verbose
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--quiet", "--verbose" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.IsNull(launcher);
    }

    [TestMethod]
    public void AllOptionsSet()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "myapp.csproj", "-e", "K1=V1", "-e", "K2=V2", "--launch-profile", "Dev", "--verbose", "arg1", "arg2" };
        var launcher = Assert.IsExactInstanceOfType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));

        Assert.AreEqual("pipe1", launcher.ServerPipeName);
        Assert.AreEqual("myapp.csproj", launcher.EntryPoint);
        Assert.AreEqual(LogLevel.Debug, launcher.GlobalOptions.LogLevel);
        Assert.IsTrue(launcher.LaunchProfileName.HasValue);
        Assert.AreEqual("Dev", launcher.LaunchProfileName.Value);
        Assert.AreSequenceEqual(["arg1", "arg2"], launcher.ApplicationArguments);
        Assert.HasCount(2, launcher.EnvironmentVariables);
        Assert.AreEqual("V1", launcher.EnvironmentVariables["K1"]);
        Assert.AreEqual("V2", launcher.EnvironmentVariables["K2"]);
    }

    [TestMethod]
    public void EnvironmentOption_Duplicates()
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", "A=1", "-e", "A=2"]);

        result.GetValue(command.EnvironmentOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = "2" });

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnvironmentOption_Duplicates_CasingDifference()
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", "A=1", "-e", "a=2"]);

        var expected = new Dictionary<string, string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            expected.Add("A", "2");
        }
        else
        {
            expected.Add("A", "1");
            expected.Add("a", "2");
        }

        result.GetValue(command.EnvironmentOption)
            .Should()
            .BeEquivalentTo(expected);

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnvironmentOption_MultiplePerToken()
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", "A=1;B=2,C=3 D=4", "-e", "B==Y=", "-e", "C;=;"]);

        result.GetValue(command.EnvironmentOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string>
            {
                ["A"] = "1;B=2,C=3 D=4",
                ["B"] = "=Y=",
                ["C;"] = ";"
            });

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnvironmentOption_NoValue()
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", "A"]);

        result.GetValue(command.EnvironmentOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = "" });

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void EnvironmentOption_WhitespaceTrimming()
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", " A \t\n\r\u2002 = X Y \t\n\r\u2002"]);

        result.GetValue(command.EnvironmentOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = " X Y \t\n\r\u2002" });

        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("=")]
    [DataRow("= X")]
    [DataRow("  \u2002 = X")]
    public void EnvironmentOption_Errors(string token)
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", token]);

        Assert.AreSequenceEqual(
        [
            $"Incorrectly formatted environment variables '{token}'"
        ], result.Errors.Select(e => e.Message));
    }
}