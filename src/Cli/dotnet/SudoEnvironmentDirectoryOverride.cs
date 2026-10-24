// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.InternalAbstractions;
using NuGet.Configuration;
using NuGet.Protocol.Plugins;

namespace Microsoft.DotNet.Cli;

/// <summary>
///  https://github.com/dotnet/sdk/issues/20195
/// </summary>
public static class SudoEnvironmentDirectoryOverride
{
    /// <summary>
    /// Not for security use. Detect if command is running under sudo
    /// via if SUDO_UID being set.
    /// </summary>
    public static bool IsRunningUnderSudo()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUDO_UID")))
        {
            return true;
        }

        return false;
    }

    public static void OverrideEnvironmentVariableToTmp(ParseResult parseResult)
    {
        if (!OperatingSystem.IsWindows() && IsRunningUnderSudo() && IsRunningWorkloadCommand(parseResult))
        {
            string sudoHome = TemporaryDirectory.CreateSubdirectory();
            var homeBeforeOverride = CliFolderPathCalculator.DotnetHomePath;
            PreserveUserNuGetPlugins(homeBeforeOverride);
            Environment.SetEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName, sudoHome);
            Environment.SetEnvironmentVariable("HOME", sudoHome);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XDG_DATA_HOME")))
            {
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(sudoHome, ".local", "share"));
            }

            CopyUserNuGetConfigToOverriddenHome(homeBeforeOverride, sudoHome);
        }
    }

    private static void PreserveUserNuGetPlugins(string homeBeforeOverride)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUGET_NETCORE_PLUGIN_PATHS")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUGET_PLUGIN_PATHS")))
        {
            return;
        }

        var pluginDirectory = Path.Combine(homeBeforeOverride, ".nuget", "plugins", "netcore");
        var plugins = PluginDiscoveryUtility.GetConventionBasedPlugins([pluginDirectory]).ToArray();
        if (plugins.Length > 0)
        {
            // NuGet also searches PATH for tool-based plugins; include those directories
            // when overriding its conventional home-based plugin discovery.
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
            Environment.SetEnvironmentVariable("NUGET_PLUGIN_PATHS", string.Join(Path.PathSeparator, plugins.Concat(paths)));
        }
    }

    /// <summary>
    /// To make NuGet honor the user's NuGet config file.
    /// Copying instead of using the file directly to avoid existing file being set higher permission
    /// Try to delete the existing NuGet config file in "/tmp/dotnet_sudo_home/"
    /// to avoid different user's NuGet config getting mixed.
    /// </summary>
    private static void CopyUserNuGetConfigToOverriddenHome(string homeBeforeOverride, string sudoHome)
    {
        // https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Common/PathUtil/NuGetEnvironment.cs#L139
        // home is cache in NuGet we cannot directly use the call
        var userSettingsDir = Path.Combine(homeBeforeOverride, ".nuget", "NuGet");

        string userNuGetConfig = Settings.OrderedSettingsFileNames
            .Select(fileName => Path.Combine(userSettingsDir, fileName))
            .FirstOrDefault(f => File.Exists(f));

        var overriddenSettingsDir = Path.Combine(sudoHome, ".nuget", "NuGet");
        var overriddenNugetConfig = Path.Combine(overriddenSettingsDir, Settings.DefaultSettingsFileName);

        if (File.Exists(overriddenNugetConfig))
        {
            try
            {
                FileAccessRetrier.RetryOnIOException(
                    () => File.Delete(overriddenNugetConfig));
            }
            catch
            {
                // best effort to remove
            }
        }

        if (userNuGetConfig != default)
        {
            try
            {
                Directory.CreateDirectory(overriddenSettingsDir);
                FileAccessRetrier.RetryOnIOException(
                    () => File.Copy(userNuGetConfig, overriddenNugetConfig, overwrite: true));
            }
            catch
            {
                // best effort to copy
            }
        }
    }

    private static bool IsRunningWorkloadCommand(ParseResult parseResult) =>
        parseResult.RootSubCommandResult() == WorkloadCommandDefinition.Name;
}
