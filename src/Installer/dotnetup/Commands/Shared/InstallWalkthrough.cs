// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Handles interactive prompts and decision-making during .NET component installation.
/// This includes resolving channel/version, set-default-install preferences, and global.json updates.
/// </summary>
internal class InstallWalkthrough
{
    private readonly IDotnetInstallManager _dotnetInstaller;
    private readonly ChannelVersionResolver _channelVersionResolver;
    private InstallRootManager? _installRootManager;

    public InstallWalkthrough(IDotnetInstallManager dotnetInstaller, ChannelVersionResolver channelVersionResolver)
    {
        _dotnetInstaller = dotnetInstaller;
        _channelVersionResolver = channelVersionResolver;
    }

    private InstallRootManager InstallRootManager => _installRootManager ??= new InstallRootManager(_dotnetInstaller);

    /// <summary>
    /// Resolves the channel or version to install, considering global.json and user input.
    /// </summary>
    /// <param name="explicitVersionOrChannel">The version/channel explicitly provided by the user.</param>
    /// <param name="channelFromGlobalJson">The channel resolved from global.json, if any.</param>
    /// <param name="globalJsonPath">Path to the global.json file, for display purposes.</param>
    /// <param name="interactive">Whether to prompt the user for input.</param>
    /// <param name="componentDescription">Description of the component (e.g., ".NET SDK", ".NET Runtime").</param>
    /// <param name="defaultChannel">The default channel to use if none specified (typically "latest").</param>
    /// <returns>The resolved channel or version string.</returns>
    public string ResolveChannel(
        string? explicitVersionOrChannel,
        string? channelFromGlobalJson,
        string? globalJsonPath,
        bool interactive,
        string componentDescription,
        string defaultChannel = "latest")
    {
        if (channelFromGlobalJson is not null)
        {
            SpectreAnsiConsole.WriteLine($"{componentDescription} {channelFromGlobalJson} will be installed since {globalJsonPath} specifies that version.");
            return channelFromGlobalJson;
        }

        if (explicitVersionOrChannel is not null)
        {
            return explicitVersionOrChannel;
        }

        if (interactive)
        {
            SpectreAnsiConsole.WriteLine("Available supported channels: " + string.Join(' ', _channelVersionResolver.GetSupportedChannels()));
            SpectreAnsiConsole.WriteLine("You can also specify a specific version (for example 9.0.304).");

            return SpectreAnsiConsole.Prompt(
                new TextPrompt<string>($"Which channel of the {componentDescription} do you want to install?")
                    .DefaultValue(defaultChannel));
        }

        return defaultChannel;
    }

    /// <summary>
    /// Determines whether global.json should be updated based on channel mismatch.
    /// </summary>
    /// <param name="channelFromGlobalJson">The channel from global.json.</param>
    /// <param name="explicitVersionOrChannel">The explicitly specified channel.</param>
    /// <param name="explicitUpdateGlobalJson">The explicitly specified update-global-json option.</param>
    /// <param name="interactive">Whether to prompt the user.</param>
    /// <returns>True if global.json should be updated, false otherwise, or null if not determined.</returns>
    public bool? ResolveUpdateGlobalJson(
        string? channelFromGlobalJson,
        string? explicitVersionOrChannel,
        bool? explicitUpdateGlobalJson,
        bool interactive)
    {
        if (channelFromGlobalJson is not null && explicitVersionOrChannel is not null &&
            //  TODO: Should channel comparison be case-sensitive?
            !channelFromGlobalJson.Equals(explicitVersionOrChannel, StringComparison.OrdinalIgnoreCase))
        {
            if (interactive && explicitUpdateGlobalJson == null)
            {
                return SpectreAnsiConsole.Confirm(
                    $"The channel specified in global.json ({channelFromGlobalJson}) does not match the channel specified ({explicitVersionOrChannel}). Do you want to update global.json to match the specified channel?",
                    defaultValue: true);
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves whether the installation should be set as the default .NET install.
    /// </summary>
    /// <param name="explicitSetDefaultInstall">The explicitly specified set-default-install option.</param>
    /// <param name="currentDotnetInstallRoot">The current .NET installation configuration.</param>
    /// <param name="resolvedInstallPath">The resolved installation path.</param>
    /// <param name="installPathFromGlobalJson">Whether the install path came from global.json.</param>
    /// <param name="interactive">Whether to prompt the user.</param>
    /// <returns>True if the installation should be set as default, false otherwise.</returns>
    public bool ResolveSetDefaultInstall(
        bool? explicitSetDefaultInstall,
        DotnetInstallRootConfiguration? currentDotnetInstallRoot,
        string resolvedInstallPath,
        string? installPathFromGlobalJson,
        bool interactive)
    {
        bool? resolvedSetDefaultInstall = explicitSetDefaultInstall;

        if (resolvedSetDefaultInstall == null)
        {
            //  If global.json specified an install path, we don't prompt for setting the default install path (since you probably don't want to do that for a repo-local path)
            if (interactive && installPathFromGlobalJson == null)
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
