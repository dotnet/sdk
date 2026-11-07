// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using NuGet.Protocol.Plugins;

namespace dotnet.Tests;

[TestClass]
[DoNotParallelize]
public class SudoEnvironmentDirectoryOverrideTests : SdkTest
{
    private readonly Dictionary<string, string?> _originalEnvironment = new();
    private string _home = null!;

    [TestInitialize]
    public void Initialize()
    {
        foreach (var name in new[] { "HOME", "DOTNET_CLI_HOME", "SUDO_UID", "NUGET_PLUGIN_PATHS", "NUGET_NETCORE_PLUGIN_PATHS", "XDG_DATA_HOME", "PATH" })
        {
            _originalEnvironment[name] = Environment.GetEnvironmentVariable(name);
            if (name != "PATH")
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        _home = TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString()).Path;
        Environment.SetEnvironmentVariable("HOME", _home);
        Environment.SetEnvironmentVariable("SUDO_UID", "1000");
    }

    [TestCleanup]
    public void Cleanup()
    {
        var sudoHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
        foreach (var (name, value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        if (sudoHome is not null && sudoHome != _home && Directory.Exists(sudoHome))
        {
            Directory.Delete(sudoHome, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DiscoversOriginalPluginsWithoutCopying(bool useCliHome)
    {
        if (useCliHome)
        {
            _home = Path.Combine(_home, "cli home");
            Directory.CreateDirectory(_home);
            Environment.SetEnvironmentVariable("DOTNET_CLI_HOME", _home);
        }

        var pluginDirectory = Path.Combine(_home, ".nuget", "plugins", "netcore", "Test Provider");
        Directory.CreateDirectory(pluginDirectory);
        var plugin = Path.Combine(pluginDirectory, "Test Provider.dll");
        File.WriteAllText(plugin, "");
        var toolDirectory = Path.Combine(_home, "tools");
        Directory.CreateDirectory(toolDirectory);
        var toolPlugin = Path.Combine(toolDirectory, "nuget-plugin-test");
        File.WriteAllText(toolPlugin, "");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(toolPlugin, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
        Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, _originalEnvironment["PATH"], toolDirectory));

        SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(Parser.Parse(["workload", "update", "--interactive"]));

        var sudoHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME")!;
        sudoHome.Should().NotBe(_home);
        Environment.GetEnvironmentVariable("HOME").Should().Be(sudoHome);
        Environment.GetEnvironmentVariable("NUGET_NETCORE_PLUGIN_PATHS").Should().BeNull();
        using var discoverer = new PluginDiscoverer();
        var discovered = await discoverer.DiscoverAsync(CancellationToken.None);
        discovered.Select(result => result.PluginFile.Path).Should().Contain(plugin);
        discovered.Select(result => result.PluginFile.Path).Should().Contain(toolPlugin);
        Directory.Exists(Path.Combine(sudoHome, ".nuget", "plugins")).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [DataRow("NUGET_PLUGIN_PATHS")]
    [DataRow("NUGET_NETCORE_PLUGIN_PATHS")]
    public void PreservesExplicitPluginPaths(string variable)
    {
        var existing = Path.Combine(_home, "existing.dll");
        Environment.SetEnvironmentVariable(variable, existing);

        SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(Parser.Parse(["workload", "update"]));

        Environment.GetEnvironmentVariable(variable).Should().Be(existing);
        Environment.GetEnvironmentVariable(variable == "NUGET_PLUGIN_PATHS" ? "NUGET_NETCORE_PLUGIN_PATHS" : "NUGET_PLUGIN_PATHS").Should().BeNull();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void UsesIsolatedHomeForNuGetConfigAndCaches()
    {
        var settings = Path.Combine(_home, ".nuget", "NuGet");
        Directory.CreateDirectory(settings);
        File.WriteAllText(Path.Combine(settings, "NuGet.Config"), "<configuration />");
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(_home, ".local", "share"));

        SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(Parser.Parse(["workload", "update"]));

        var sudoHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME")!;
        File.ReadAllText(Path.Combine(sudoHome, ".nuget", "NuGet", "NuGet.Config")).Should().Be("<configuration />");
        Environment.GetEnvironmentVariable("HOME").Should().Be(sudoHome);
        Environment.GetEnvironmentVariable("XDG_DATA_HOME").Should().Be(Path.Combine(sudoHome, ".local", "share"));
        Environment.GetEnvironmentVariable("NUGET_PLUGIN_PATHS").Should().BeNull();
    }
}
