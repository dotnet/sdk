// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
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
    private readonly InstallWorkflow.InstallWorkflowOptions _options;

    /// <summary>
    /// Returns true when the given <see cref="PathPreference"/> implies we should
    /// replace the default dotnet installation (i.e. update PATH / DOTNET_ROOT).
    /// </summary>
    public static bool ShouldReplaceSystemConfiguration(PathPreference preference) =>
        preference == PathPreference.FullPathReplacement;

    /// <summary>
    /// Returns true when the user chose to convert existing system-level .NET installs
    /// into dotnetup-managed installs. This applies to any mode that shadows the system PATH.
    /// </summary>
    public static bool ShouldPromptToConvertSystemInstalls(PathPreference preference) =>
        preference != PathPreference.DotnetupDotnet;

    /// <summary>
    /// Returns true when the user chose full PATH replacement (Windows-only),
    /// meaning the system PATH entry for dotnet is replaced with the dotnetup path.
    /// </summary>
    public static bool ShouldReplaceSystemPath(PathPreference preference) =>
        preference == PathPreference.FullPathReplacement;

    public InstallWalkthrough(
        IDotnetInstallManager dotnetInstaller,
        InstallWorkflow.InstallWorkflowOptions options)
    {
        _dotnetInstaller = dotnetInstaller;
        _options = options;
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
        // Explicit version/channel from the user always takes priority
        if (_options.VersionOrChannel is not null)
        {
            return _options.VersionOrChannel;
        }

        if (channelFromGlobalJson is not null)
        {
            SpectreAnsiConsole.WriteLine($"{_options.ComponentDescription} {channelFromGlobalJson} will be installed since {globalJsonPath} specifies that version.");
            return channelFromGlobalJson;
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
    /// Prompts the user about copying admin-managed installs into the dotnetup-managed directory.
    /// </summary>
    /// <returns>True if the user wants to copy system installs, false if they decline or no system installs exist.</returns>
    internal static bool PromptAdminMigration(IDotnetInstallManager dotnetInstaller, PathPreference pathPreference)
    {
        if (!InstallWalkthrough.ShouldPromptToConvertSystemInstalls(pathPreference))
        {
            return false;
        }

        var systemInstalls = dotnetInstaller.GetExistingSystemInstalls();
        if (systemInstalls.Count == 0)
        {
            // Nothing to migrate — don't override the caller's initial choice.
            return false;
        }

        // Find the system install path for display purposes
        var currentInstall = dotnetInstaller.GetConfiguredInstallType();
        string systemPath = currentInstall?.InstallType == InstallType.Admin
            ? currentInstall.Path
            : DotnetInstallManager.GetSystemDotnetPaths().FirstOrDefault() ?? "the system .NET location";

        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine($"You have existing system install(s) of .NET in [{DotnetupTheme.Current.Accent}]{systemPath.EscapeMarkup()}[/].");

        var displayItems = systemInstalls
            .OrderBy(i => i.Component)
            .ThenByDescending(i => i.Version)
            .Select(i => string.Format(CultureInfo.InvariantCulture, "{0} {1}", i.Component.GetDisplayName(), i.Version))
            .ToList();

        bool userAcceptedMigration = SpectreDisplayHelpers.RenderScrollableListWithConfirm(
            displayItems,
            visibleCount: 3,
            "Do you want to copy the following installs into the dotnetup managed directory?");

        if (userAcceptedMigration)
        {
            SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]These will be installed after your setup completes. You can change this later with \"dotnetup defaultinstall\".[/]");
        }
        else
        {
            SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]You can change this later with \"dotnetup defaultinstall\".[/]");
        }
        return userAcceptedMigration;
    }

    /// <summary>
    /// Configures the default .NET installation if requested.
    /// </summary>
    /// <param name="dotnetInstaller">The install manager.</param>
    /// <param name="setDefaultInstall">Whether to set as default install.</param>
    /// <param name="installPath">The installation path.</param>
    public static void ConfigureDefaultInstallIfRequested(
        IDotnetInstallManager dotnetInstaller,
        bool setDefaultInstall,
        string installPath)
    {
        if (setDefaultInstall)
        {
            dotnetInstaller.ConfigureInstallType(InstallType.User, installPath);
        }
    }
}
