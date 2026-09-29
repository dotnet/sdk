// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using NuGet.Protocol.Plugins;

namespace dotnet.Tests;

[TestClass]
// Home variables are also read by other tests and inherited by their child processes.
[DoNotParallelize]
public class SudoEnvironmentDirectoryOverrideTests : SdkTest
{
    private readonly Dictionary<string, string?> _originalEnvironment = new();
    private string _home = null!;

    [TestInitialize]
    public void InitializeEnvironment()
    {
        foreach (string name in new[] { "HOME", "DOTNET_CLI_HOME", "SUDO_UID", "NUGET_PLUGIN_PATHS", "NUGET_NETCORE_PLUGIN_PATHS", "XDG_DATA_HOME" })
        {
            _originalEnvironment[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }

        _home = TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString()).Path;
        Environment.SetEnvironmentVariable("HOME", _home);
        Environment.SetEnvironmentVariable("SUDO_UID", "1000");
    }

    [TestCleanup]
    public void RestoreEnvironment()
    {
        string? overriddenHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
        foreach (var (name, value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        if (overriddenHome is not null && overriddenHome != _home)
        {
            Directory.Delete(overriddenHome, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [DataRow(false)]
    [DataRow(true)]
    public void PreservesConventionBasedNuGetPlugins(bool useCliHome)
    {
        if (useCliHome)
        {
            _home = Path.Combine(_home, "cli home");
            Directory.CreateDirectory(_home);
            Environment.SetEnvironmentVariable("DOTNET_CLI_HOME", _home);
        }

        string firstPlugin = CreatePlugin("FirstProvider");
        string secondPlugin = CreatePlugin("Second Provider");
        string dependency = Path.Combine(Path.GetDirectoryName(firstPlugin)!, "runtimes", "unix", "Dependency.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(dependency)!);
        File.WriteAllText(dependency, "dependency");

        SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(Parser.Parse(["workload", "update", "--interactive"]));

        string overriddenHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME")!;
        overriddenHome.Should().NotBe(_home);
        Environment.GetEnvironmentVariable("HOME").Should().Be(overriddenHome);
        Environment.GetEnvironmentVariable("NUGET_PLUGIN_PATHS").Should().BeNull();
        Environment.GetEnvironmentVariable("NUGET_NETCORE_PLUGIN_PATHS").Should().BeNull();
        string copiedFirstPlugin = Path.Combine(overriddenHome, Path.GetRelativePath(_home, firstPlugin));
        string copiedSecondPlugin = Path.Combine(overriddenHome, Path.GetRelativePath(_home, secondPlugin));
        PluginDiscoveryUtility.GetConventionBasedPlugins([Path.Combine(overriddenHome, ".nuget", "plugins", "netcore")])
            .Should().BeEquivalentTo([copiedFirstPlugin, copiedSecondPlugin]);
        File.ReadAllText(Path.Combine(overriddenHome, Path.GetRelativePath(_home, dependency))).Should().Be("dependency");
        File.WriteAllText(copiedFirstPlugin, "changed");
        File.ReadAllText(firstPlugin).Should().Be("");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [DataRow("NUGET_PLUGIN_PATHS")]
    [DataRow("NUGET_NETCORE_PLUGIN_PATHS")]
    public void PreservesExplicitNuGetPluginPaths(string variableName)
    {
        CreatePlugin("DefaultProvider");
        string explicitPaths = Path.Combine(_home, "CustomProvider.dll") + Path.PathSeparator + Path.Combine(_home, "OtherProvider.dll");
        Environment.SetEnvironmentVariable(variableName, explicitPaths);

        SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(Parser.Parse(["workload", "update"]));

        Environment.GetEnvironmentVariable(variableName).Should().Be(explicitPaths);
        string otherVariable = variableName == "NUGET_PLUGIN_PATHS" ? "NUGET_NETCORE_PLUGIN_PATHS" : "NUGET_PLUGIN_PATHS";
        Environment.GetEnvironmentVariable(otherVariable).Should().BeNull();
        Directory.Exists(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_CLI_HOME")!, ".nuget", "plugins")).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [DataRow(false)]
    [DataRow(true)]
    public void DoesNotSetPluginPathsWhenNoPluginsAreInstalled(bool createPluginDirectory)
    {
        if (createPluginDirectory)
        {
            Directory.CreateDirectory(Path.Combine(_home, ".nuget", "plugins", "netcore"));
        }

        SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(Parser.Parse(["workload", "list"]));

        Environment.GetEnvironmentVariable("DOTNET_CLI_HOME").Should().NotBe(_home);
        Environment.GetEnvironmentVariable("HOME").Should().Be(Environment.GetEnvironmentVariable("DOTNET_CLI_HOME"));
        Environment.GetEnvironmentVariable("NUGET_NETCORE_PLUGIN_PATHS").Should().BeNull();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void IsolatesDefaultCredentialProviderCache()
    {
        SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(Parser.Parse(["workload", "update"]));

        string overriddenHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME")!;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        localAppData.Should().Be(Path.Combine(overriddenHome, ".local", "share"));

        string cacheDirectory = Path.Combine(localAppData, "MicrosoftCredentialProvider");
        Directory.CreateDirectory(cacheDirectory);
        File.WriteAllText(Path.Combine(cacheDirectory, "SessionTokenCache.dat"), "cache");
        Directory.Exists(Path.Combine(_home, ".local")).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [DataRow("NuGet.Config")]
    [DataRow("NuGet.config")]
    [DataRow("nuget.config")]
    public void CopiesNuGetConfigToTemporaryHome(string configFileName)
    {
        string settingsDirectory = Path.Combine(_home, ".nuget", "NuGet");
        Directory.CreateDirectory(settingsDirectory);
        string originalConfig = Path.Combine(settingsDirectory, configFileName);
        const string config = """
            <configuration>
              <packageSources>
                <clear />
                <add key="private" value="https://example.invalid/nuget/v3/index.json" />
              </packageSources>
            </configuration>
            """;
        File.WriteAllText(originalConfig, config);

        SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(Parser.Parse(["workload", "update"]));

        string copiedConfig = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_CLI_HOME")!, ".nuget", "NuGet", "NuGet.Config");
        File.ReadAllText(copiedConfig).Should().Be(config);
        File.WriteAllText(copiedConfig, "<configuration />");
        File.ReadAllText(originalConfig).Should().Be(config);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [DataRow("workload", null)]
    [DataRow("restore", "1000")]
    public void DoesNotOverrideOtherCommandsOrNonSudoInvocations(string command, string? sudoUid)
    {
        CreatePlugin("DefaultProvider");
        Environment.SetEnvironmentVariable("SUDO_UID", sudoUid);

        SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(Parser.Parse([command]));

        Environment.GetEnvironmentVariable("HOME").Should().Be(_home);
        Environment.GetEnvironmentVariable("DOTNET_CLI_HOME").Should().BeNull();
        Environment.GetEnvironmentVariable("NUGET_NETCORE_PLUGIN_PATHS").Should().BeNull();
    }

    private string CreatePlugin(string name)
    {
        string pluginDirectory = Path.Combine(_home, ".nuget", "plugins", "netcore", name);
        Directory.CreateDirectory(pluginDirectory);
        string pluginPath = Path.Combine(pluginDirectory, name + ".dll");
        File.WriteAllText(pluginPath, "");
        return pluginPath;
    }
}
