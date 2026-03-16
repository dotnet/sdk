// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

public class AspireResourceLauncherCliTests
{
    [Fact]
    public void RequiredServerOption()
    {
        // --server option is missing
        var args = new[] { "resource", "--entrypoint", "proj" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void RequiredEntryPointOption()
    {
        // --entrypoint option is missing
        var args = new[] { "resource", "--server", "pipe1" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void MinimalRequiredOptions()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj.csproj" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("pipe1", launcher.ServerPipeName);
        Assert.Equal("proj.csproj", launcher.EntryPoint);
        Assert.Empty(launcher.ApplicationArguments);
        Assert.Empty(launcher.EnvironmentVariables);
        Assert.True(launcher.LaunchProfileName.HasValue);
        Assert.Null(launcher.LaunchProfileName.Value);
        Assert.Equal(LogLevel.Information, launcher.GlobalOptions.LogLevel);
    }

    [Fact]
    public void ApplicationArguments()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "a", "b" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        AssertEx.SequenceEqual(["a", "b"], launcher.ApplicationArguments);
    }

    [Fact]
    public void EnvironmentOption_SingleVariable()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "KEY=value" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.Single(launcher.EnvironmentVariables);
        Assert.Equal("value", launcher.EnvironmentVariables["KEY"]);
    }

    [Fact]
    public void EnvironmentOption_MultipleVariables()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "KEY1=val1", "-e", "KEY2=val2" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal(2, launcher.EnvironmentVariables.Count);
        Assert.Equal("val1", launcher.EnvironmentVariables["KEY1"]);
        Assert.Equal("val2", launcher.EnvironmentVariables["KEY2"]);
    }

    [Fact]
    public void EnvironmentOption_ValueWithEquals()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "CONN=Server=localhost;Port=5432" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("Server=localhost;Port=5432", launcher.EnvironmentVariables["CONN"]);
    }

    [Fact]
    public void EnvironmentOption_EmptyValue()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "KEY=" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("", launcher.EnvironmentVariables["KEY"]);
    }

    [Fact]
    public void EnvironmentOption_NoEquals()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-e", "KEY" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.Equal("", launcher.EnvironmentVariables["KEY"]);
    }

    [Fact]
    public void NoLaunchProfileOption()
    {
        // With no-launch-profile flag
        var argsNoProfile = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--no-launch-profile" };
        var launcherNoProfile = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsNoProfile));
        Assert.False(launcherNoProfile.LaunchProfileName.HasValue);

        // Without no-launch-profile flag
        var argsDefault = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj" };
        var launcherDefault = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsDefault));
        Assert.True(launcherDefault.LaunchProfileName.HasValue);
        Assert.Null(launcherDefault.LaunchProfileName.Value);
    }

    [Fact]
    public void LaunchProfileOption()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--launch-profile", "MyProfile" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.True(launcher.LaunchProfileName.HasValue);
        Assert.Equal("MyProfile", launcher.LaunchProfileName.Value);
    }

    [Fact]
    public void LaunchProfileOption_ShortForm()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "-lp", "MyProfile" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));
        Assert.True(launcher.LaunchProfileName.HasValue);
        Assert.Equal("MyProfile", launcher.LaunchProfileName.Value);
    }

    [Fact]
    public void VerboseOption()
    {
        var argsVerbose = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--verbose" };
        var launcherVerbose = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsVerbose));
        Assert.Equal(LogLevel.Debug, launcherVerbose.GlobalOptions.LogLevel);

        var argsNotVerbose = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj" };
        var launcherNotVerbose = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsNotVerbose));
        Assert.Equal(LogLevel.Information, launcherNotVerbose.GlobalOptions.LogLevel);
    }

    [Fact]
    public void QuietOption()
    {
        var argsQuiet = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--quiet" };
        var launcherQuiet = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsQuiet));
        Assert.Equal(LogLevel.Warning, launcherQuiet.GlobalOptions.LogLevel);

        var argsNotQuiet = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj" };
        var launcherNotQuiet = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(argsNotQuiet));
        Assert.Equal(LogLevel.Information, launcherNotQuiet.GlobalOptions.LogLevel);
    }

    [Fact]
    public void ConflictingOptions()
    {
        // Cannot specify both --quiet and --verbose
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "proj", "--quiet", "--verbose" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void AllOptionsSet()
    {
        var args = new[] { "resource", "--server", "pipe1", "--entrypoint", "myapp.csproj", "-e", "K1=V1", "-e", "K2=V2", "--launch-profile", "Dev", "--verbose", "arg1", "arg2" };
        var launcher = Assert.IsType<AspireResourceLauncher>(AspireLauncher.TryCreate(args));

        Assert.Equal("pipe1", launcher.ServerPipeName);
        Assert.Equal("myapp.csproj", launcher.EntryPoint);
        Assert.Equal(LogLevel.Debug, launcher.GlobalOptions.LogLevel);
        Assert.True(launcher.LaunchProfileName.HasValue);
        Assert.Equal("Dev", launcher.LaunchProfileName.Value);
        AssertEx.SequenceEqual(["arg1", "arg2"], launcher.ApplicationArguments);
        Assert.Equal(2, launcher.EnvironmentVariables.Count);
        Assert.Equal("V1", launcher.EnvironmentVariables["K1"]);
        Assert.Equal("V2", launcher.EnvironmentVariables["K2"]);
    }

    [Fact]
    public void EnvironmentOption_Duplicates()
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", "A=1", "-e", "A=2"]);

        result.GetValue(command.EnvironmentOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = "2" });

        result.Errors.Should().BeEmpty();
    }

    [Fact]
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

    [Fact]
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

    [Fact]
    public void EnvironmentOption_NoValue()
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", "A"]);

        result.GetValue(command.EnvironmentOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = "" });

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void EnvironmentOption_WhitespaceTrimming()
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", " A \t\n\r\u2002 = X Y \t\n\r\u2002"]);

        result.GetValue(command.EnvironmentOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = " X Y \t\n\r\u2002" });

        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("=")]
    [InlineData("= X")]
    [InlineData("  \u2002 = X")]
    public void EnvironmentOption_Errors(string token)
    {
        var command = new AspireResourceCommandDefinition();
        var result = command.Parse(["--server", "S", "--entrypoint", "E", "-e", token]);

        AssertEx.SequenceEqual(
        [
            $"Incorrectly formatted environment variables '{token}'"
        ], result.Errors.Select(e => e.Message));
    }
}
