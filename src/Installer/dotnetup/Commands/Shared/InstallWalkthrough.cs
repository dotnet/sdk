// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Handles interactive prompts and decision-making during .NET component installation.
/// This includes resolving channel/version, set-default-install preferences, and global.json updates.
///
/// Note: Install path prompting is handled by <see cref="InstallPathResolver"/> to keep path resolution
/// logic self-contained. This class focuses on post-path-resolution decisions.
/// </summary>
internal class InstallWalkthrough
{
    private readonly IDotnetInstallManager _dotnetInstaller;
    private readonly ChannelVersionResolver _channelVersionResolver;
    private readonly InstallWorkflow.InstallWorkflowOptions _options;
    private InstallRootManager? _installRootManager;

    public InstallWalkthrough(
        IDotnetInstallManager dotnetInstaller,
        ChannelVersionResolver channelVersionResolver,
        InstallWorkflow.InstallWorkflowOptions options)
    {
        _dotnetInstaller = dotnetInstaller;
        _channelVersionResolver = channelVersionResolver;
        _options = options;
    }

    private InstallRootManager InstallRootManager => _installRootManager ??= new InstallRootManager(_dotnetInstaller);

    /// <summary>
    /// Prompts the user to install a higher admin version when switching to user install.
    /// This is relevant when the user is setting up a user install and has a higher version in admin install.
    /// </summary>
    /// <param name="resolvedVersion">The version being installed.</param>
    /// <param name="setDefaultInstall">Whether the install will be set as default.</param>
    /// <param name="currentInstallRoot">Current installation configuration.</param>
    /// <returns>List of additional versions to install, empty if none.</returns>
    public List<string> GetAdditionalAdminVersionsToMigrate(
        ReleaseVersion? resolvedVersion,
        bool setDefaultInstall,
        DotnetInstallRootConfiguration? currentInstallRoot)
    {
        var additionalVersions = new List<string>();

        if (setDefaultInstall && currentInstallRoot?.InstallType == InstallType.Admin)
        {
            // Track admin-to-user migration scenario
            Activity.Current?.SetTag("install.migrating_from_admin", true);

            if (_options.Interactive)
            {
                var latestAdminVersion = _dotnetInstaller.GetLatestInstalledAdminVersion();
                if (latestAdminVersion is not null && resolvedVersion < new ReleaseVersion(latestAdminVersion))
                {
                    SpectreAnsiConsole.WriteLine($"Since the admin installs of the {_options.ComponentDescription} will no longer be accessible, we recommend installing the latest admin installed " +
                        $"version ({latestAdminVersion}) to the new user install location. This will make sure this version of the {_options.ComponentDescription} continues to be used for projects that don't specify a .NET SDK version in global.json.");

                    if (SpectreAnsiConsole.Confirm($"Also install {_options.ComponentDescription} {latestAdminVersion}?",
                        defaultValue: true))
                    {
                        additionalVersions.Add(latestAdminVersion);
                        Activity.Current?.SetTag("install.admin_version_copied", true);
                    }
                }
            }
            //  TODO: Add command-line option for installing admin versions locally
        }

        return additionalVersions;
    }

    /// <summary>
    /// Resolves the channel or version to install, considering global.json and user input.
    /// </summary>
    /// <param name="channelFromGlobalJson">The channel resolved from global.json, if any.</param>
    /// <param name="globalJsonPath">Path to the global.json file, for display purposes.</param>
    /// <param name="defaultChannel">The default channel to use if none specified (typically "latest").</param>
    /// <returns>The resolved channel or version string.</returns>
    public string ResolveChannel(
        string? channelFromGlobalJson,
        string? globalJsonPath,
        string defaultChannel = "latest")
    {
        if (channelFromGlobalJson is not null)
        {
            SpectreAnsiConsole.WriteLine($"{_options.ComponentDescription} {channelFromGlobalJson} will be installed since {globalJsonPath} specifies that version.");
            return channelFromGlobalJson;
        }

        if (_options.VersionOrChannel is not null)
        {
            return _options.VersionOrChannel;
        }

        if (_options.Interactive)
        {
            // Feature bands (like 9.0.1xx) are SDK-specific, don't show them for runtimes
            bool includeFeatureBands = _options.Component == InstallComponent.SDK;
            SpectreAnsiConsole.WriteLine("Available supported channels: " + string.Join(' ', _channelVersionResolver.GetSupportedChannels(includeFeatureBands)));

            // Use appropriate version example for SDK vs Runtime
            string versionExample = _options.Component == InstallComponent.SDK ? "9.0.304" : "9.0.12";
            SpectreAnsiConsole.WriteLine($"You can also specify a specific version (for example {versionExample}).");

            return SpectreAnsiConsole.Prompt(
                new TextPrompt<string>($"Which channel of the {_options.ComponentDescription} do you want to install?")
                    .DefaultValue(defaultChannel));
        }

        return defaultChannel;
    }

    /// <summary>
    /// Determines whether global.json should be updated based on channel mismatch.
    /// </summary>
    /// <param name="channelFromGlobalJson">The channel from global.json.</param>
    /// <returns>True if global.json should be updated, false otherwise, or null if not determined.</returns>
    public bool? ResolveUpdateGlobalJson(string? channelFromGlobalJson)
    {
        if (channelFromGlobalJson is not null && _options.VersionOrChannel is not null &&
            //  TODO: Should channel comparison be case-sensitive?
            !channelFromGlobalJson.Equals(_options.VersionOrChannel, StringComparison.OrdinalIgnoreCase))
        {
            if (_options.Interactive && _options.UpdateGlobalJson == null)
            {
                return SpectreAnsiConsole.Confirm(
                    $"The channel specified in global.json ({channelFromGlobalJson}) does not match the channel specified ({_options.VersionOrChannel}). Do you want to update global.json to match the specified channel?",
                    defaultValue: true);
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves whether the installation should be set as the default .NET install.
    /// </summary>
    /// <param name="currentDotnetInstallRoot">The current .NET installation configuration.</param>
    /// <param name="resolvedInstallPath">The resolved installation path.</param>
    /// <param name="installPathCameFromGlobalJson">True if the install path came from global.json, which typically means it's repo-local.</param>
    /// <returns>True if the installation should be set as default, false otherwise.</returns>
    public bool ResolveSetDefaultInstall(
        DotnetInstallRootConfiguration? currentDotnetInstallRoot,
        string resolvedInstallPath,
        bool installPathCameFromGlobalJson)
    {
        bool? resolvedSetDefaultInstall = _options.SetDefaultInstall;

        if (resolvedSetDefaultInstall == null)
        {
            //  If global.json specified an install path, we don't prompt for setting the default install path (since you probably don't want to do that for a repo-local path)
            if (_options.Interactive && !installPathCameFromGlobalJson)
            {
                if (currentDotnetInstallRoot == null)
                {
                    resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                        $"Do you want to set the install path ({resolvedInstallPath}) as the default dotnet install? This will update the PATH and DOTNET_ROOT environment variables.",
                        defaultValue: true);
                }
                else if (currentDotnetInstallRoot.InstallType == InstallType.User)
                {
                    if (DotnetupUtilities.PathsEqual(resolvedInstallPath, currentDotnetInstallRoot.Path))
                    {
                        //  If the current install is fully configured and matches the resolved path, skip the prompt
                        if (currentDotnetInstallRoot.IsFullyConfigured)
                        {
                            // Default install is already set up correctly, no need to prompt
                            resolvedSetDefaultInstall = false;
                        }
                        else
                        {
                            // Not fully configured - display what needs to be configured and prompt
                            if (OperatingSystem.IsWindows())
                            {
                                UserInstallRootChanges userInstallRootChanges = InstallRootManager.GetUserInstallRootChanges();

                                SpectreAnsiConsole.WriteLine($"The .NET installation at {resolvedInstallPath} is not fully configured.");
                                SpectreAnsiConsole.WriteLine("The following changes are needed:");

                                if (userInstallRootChanges.NeedsRemoveAdminPath)
                                {
                                    SpectreAnsiConsole.WriteLine("  - Remove admin .NET paths from system PATH");
                                }
                                if (userInstallRootChanges.NeedsAddToUserPath)
                                {
                                    SpectreAnsiConsole.WriteLine($"  - Add {userInstallRootChanges.UserDotnetPath} to user PATH");
                                }
                                if (userInstallRootChanges.NeedsSetDotnetRoot)
                                {
                                    SpectreAnsiConsole.WriteLine($"  - Set DOTNET_ROOT to {userInstallRootChanges.UserDotnetPath}");
                                }

                                resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                                    "Do you want to apply these configuration changes?",
                                    defaultValue: true);
                            }
                            else
                            {
                                // On non-Windows, we don't have detailed configuration info
                                //  No need to prompt here, the default install is already set up.
                            }
                        }
                    }
                    else
                    {
                        resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                            $"The default dotnet install is currently set to {currentDotnetInstallRoot.Path}.  Do you want to change it to {resolvedInstallPath}?",
                            defaultValue: false);
                    }
                }
                else if (currentDotnetInstallRoot.InstallType == InstallType.Admin)
                {
                    SpectreAnsiConsole.WriteLine($"You have an existing admin install of .NET in {currentDotnetInstallRoot.Path}. We can configure your system to use the new install of .NET " +
                        $"in {resolvedInstallPath} instead. This would mean that the admin install of .NET would no longer be accessible from the PATH or from Visual Studio.");
                    SpectreAnsiConsole.WriteLine("You can change this later with the \"dotnetup defaultinstall\" command.");
                    resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                        $"Do you want to set the user install path ({resolvedInstallPath}) as the default dotnet install? This will update the PATH and DOTNET_ROOT environment variables.",
                        defaultValue: true);
                }

                //  TODO: Add checks for whether PATH and DOTNET_ROOT need to be updated, or if the install is in an inconsistent state
            }
            else
            {
                resolvedSetDefaultInstall = false; // Default to not setting the default install path if not specified
            }
        }

        return resolvedSetDefaultInstall ?? false;
    }
}
