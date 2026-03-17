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

    public InstallWalkthrough(
        IDotnetInstallManager dotnetInstaller,
        InstallWorkflow.InstallWorkflowOptions options)
    {
        _dotnetInstaller = dotnetInstaller;
        _options = options;
    }

    /// <summary>
    /// Prompts the user to install a higher admin version when switching to user install.
    /// This is relevant when the user is setting up a user install and has a higher version in admin install.
    /// </summary>
    /// <param name="resolvedVersion">The version being installed.</param>
    /// <param name="setDefaultInstall">Whether the install will be set as default.</param>
    /// <returns>List of additional installs to migrate, empty if none.</returns>
    public List<DotnetInstall> GetAdditionalAdminVersionsToMigrate(
        ReleaseVersion? resolvedVersion,
        bool setDefaultInstall)
    {
        // Only prompt about admin installs when the user chose to modify PATH (options 2 or 3)
        // AND set-default-install is on. Option 1 (DotnetupDotnet) doesn't touch PATH,
        // so admin installs remain accessible.
        if (_options.PathPreference == PathPreference.DotnetupDotnet || !setDefaultInstall)
        {
            return [];
        }

        // Non-interactive fallback: copy all admin installs.
        var systemInstalls = _dotnetInstaller.GetExistingSystemInstalls();
        if (systemInstalls.Count == 0)
        {
            return [];
        }

        Activity.Current?.SetTag(TelemetryTagNames.InstallMigratingFromAdmin, true);

        var seenAll = new HashSet<(InstallComponent, string)>();
        if (resolvedVersion is not null)
        {
            seenAll.Add((InstallComponent.SDK, resolvedVersion.ToString()));
        }

        var uniqueInstalls = systemInstalls.Where(i => seenAll.Add((i.Component, i.Version.ToString()))).ToList();
        if (uniqueInstalls.Count > 0)
        {
            Activity.Current?.SetTag(TelemetryTagNames.InstallAdminVersionCopied, true);
        }

        return uniqueInstalls;
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
    internal static bool PromptAdminMigration(IDotnetInstallManager dotnetInstaller)
    {
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
}
