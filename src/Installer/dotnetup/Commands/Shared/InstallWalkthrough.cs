// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
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
#pragma warning disable IDE0032 // Lazy-init via ??=; not convertible to auto-property
    private InstallRootManager? _installRootManager;
#pragma warning restore IDE0032

    public InstallWalkthrough(
        IDotnetInstallManager dotnetInstaller,
        ChannelVersionResolver channelVersionResolver,
        InstallWorkflow.InstallWorkflowOptions options)
    {
        _dotnetInstaller = dotnetInstaller;
        _ = channelVersionResolver; // Reserved for future use
        _options = options;
    }

    private InstallRootManager InstallRootManager => _installRootManager ??= new InstallRootManager(_dotnetInstaller);

    /// <summary>
    /// Prompts the user to install a higher admin version when switching to user install.
    /// This is relevant when the user is setting up a user install and has a higher version in admin install.
    /// </summary>
    /// <param name="resolvedVersion">The version being installed.</param>
    /// <param name="setDefaultInstall">Whether the install will be set as default.</param>
    /// <returns>List of additional versions to install, empty if none.</returns>
    public List<string> GetAdditionalAdminVersionsToMigrate(
        ReleaseVersion? resolvedVersion,
        bool setDefaultInstall)
    {
        var additionalVersions = new List<string>();

        // Only prompt about admin installs when the user chose to modify PATH (options 2 or 3).
        // Option 1 (DotnetupDotnet) doesn't touch PATH, so admin installs remain accessible.
        if (_options.PathPreference == PathPreference.DotnetupDotnet)
        {
            return additionalVersions;
        }

        // Check for actual admin installs rather than relying solely on the current
        // install type, because a previous walkthrough may have switched to User while
        // admin SDKs still exist in Program Files.
        var adminSdkVersions = _dotnetInstaller.GetInstalledAdminSdkVersions();
        if (setDefaultInstall && adminSdkVersions.Count > 0)
        {
            // Track admin-to-user migration scenario
            Activity.Current?.SetTag(TelemetryTagNames.InstallMigratingFromAdmin, true);

            // Copy all admin SDK versions except the one already being installed.
            // The user confirmed copying via PromptAdminMigration, so no per-version prompt is needed.
            foreach (var version in adminSdkVersions)
            {
                if (resolvedVersion is null || version != resolvedVersion.ToString())
                {
                    additionalVersions.Add(version);
                }
            }

            if (additionalVersions.Count > 0)
            {
                Activity.Current?.SetTag(TelemetryTagNames.InstallAdminVersionCopied, true);
            }
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
                    resolvedSetDefaultInstall = ResolveDefaultInstallForUserInstall(currentDotnetInstallRoot, resolvedInstallPath);
                }
                else if (currentDotnetInstallRoot.InstallType == InstallType.Admin)
                {
                    resolvedSetDefaultInstall = PromptAdminMigration(_dotnetInstaller);
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

    /// <summary>
    /// Prompts the user about copying admin-managed SDK installs into the dotnetup-managed directory.
    /// </summary>
    /// <returns>True if the user wants to proceed (or no admin installs exist), false if they decline.</returns>
    internal static bool PromptAdminMigration(IDotnetInstallManager dotnetInstaller)
    {
        var adminSdks = dotnetInstaller.GetInstalledAdminSdkVersions();
        if (adminSdks.Count == 0)
        {
            return true;
        }

        // Find the admin install path for display purposes
        var currentInstall = dotnetInstaller.GetConfiguredInstallType();
        string adminPath = currentInstall?.InstallType == InstallType.Admin
            ? currentInstall.Path
            : OperatingSystem.IsWindows()
                ? WindowsPathHelper.GetProgramFilesDotnetPaths().FirstOrDefault() ?? "Program Files\\dotnet"
                : "the system .NET location";

        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine($"You have existing system install(s) of .NET in [{DotnetupTheme.Current.Accent}]{adminPath.EscapeMarkup()}[/].");

        bool result = RenderScrollableListWithConfirm(
            adminSdks,
            visibleCount: 3,
            "Do you want to copy the following installs into the dotnetup managed directory?");

        SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]You can change this later with \"dotnetup defaultinstall\".[/]");
        return result;
    }

    /// <summary>
    /// Resolves whether the user install should be set as default when the current install type is User.
    /// </summary>
    private bool? ResolveDefaultInstallForUserInstall(
        DotnetInstallRootConfiguration currentDotnetInstallRoot,
        string resolvedInstallPath)
    {
        if (DotnetupUtilities.PathsEqual(resolvedInstallPath, currentDotnetInstallRoot.Path))
        {
            //  If the current install is fully configured and matches the resolved path, skip the prompt
            if (currentDotnetInstallRoot.IsFullyConfigured)
            {
                // Default install is already set up correctly, no need to prompt
                return false;
            }

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

                return SpectreAnsiConsole.Confirm(
                    "Do you want to apply these configuration changes?",
                    defaultValue: true);
            }
            else
            {
                // On non-Windows, we don't have detailed configuration info
                //  No need to prompt here, the default install is already set up.
                return null;
            }
        }
        else
        {
            return SpectreAnsiConsole.Confirm(
                $"The default dotnet install is currently set to {currentDotnetInstallRoot.Path}.  Do you want to change it to {resolvedInstallPath}?",
                defaultValue: false);
        }
    }

    /// <summary>
    /// Renders a list of items with only <paramref name="visibleCount"/> shown initially.
    /// When running interactively, the user can scroll with arrow keys to see more.
    /// Falls back to a static truncated list when input is redirected.
    /// </summary>
    internal static void RenderScrollableList(List<string> items, int visibleCount)
    {
        if (items.Count == 0)
        {
            return;
        }

        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;

        if (items.Count <= visibleCount || Console.IsInputRedirected)
        {
            // All items fit or non-interactive — just print them all
            foreach (var item in items)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, item.EscapeMarkup()));
            }

            return;
        }

        // Interactive scrollable list
        RunInteractiveScrollLoop(items, visibleCount, confirmPrompt: null);
    }

    /// <summary>
    /// Renders a scrollable list with an inline confirmation prompt.
    /// The prompt is shown below the list and Enter accepts the default (yes).
    /// </summary>
    internal static bool RenderScrollableListWithConfirm(List<string> items, int visibleCount, string confirmPrompt)
    {
        if (items.Count == 0)
        {
            return true;
        }

        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;
        string brand = DotnetupTheme.Current.Brand;

        if (items.Count <= visibleCount || Console.IsInputRedirected)
        {
            foreach (var item in items)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, item.EscapeMarkup()));
            }

            SpectreAnsiConsole.Markup(string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n)[/] ", confirmPrompt, brand));
            return ReadYesNo(defaultValue: true);
        }

        return RunInteractiveScrollLoop(items, visibleCount, confirmPrompt);
    }

    /// <summary>
    /// Returns true (accept) or false (decline) when <paramref name="confirmPrompt"/> is set;
    /// always returns true when <paramref name="confirmPrompt"/> is null (plain scroll).
    /// </summary>
    private static bool RunInteractiveScrollLoop(List<string> items, int visibleCount, string? confirmPrompt)
    {
        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;
        int offset = 0;
        int maxOffset = items.Count - visibleCount;

        Console.Write("\x1b[?25l"); // hide cursor
        try
        {
            int startRow = Console.CursorTop;
            RenderListWindow(items, offset, visibleCount, startRow, firstRender: true, confirmPrompt: confirmPrompt);

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                            if (offset > 0)
                            {
                                offset--;
                                RenderListWindow(items, offset, visibleCount, startRow, firstRender: false, confirmPrompt: confirmPrompt);
                            }

                            break;
                        case ConsoleKey.DownArrow:
                            if (offset < maxOffset)
                            {
                                offset++;
                                RenderListWindow(items, offset, visibleCount, startRow, firstRender: false, confirmPrompt: confirmPrompt);
                            }

                            break;
                        case ConsoleKey.Enter:
                            // Collapse to final static view and exit — Enter means "yes" when confirming
                            CollapseToFinalView(items, startRow, dim, accent, confirmPrompt, accepted: true);
                            return true;
                        case ConsoleKey.N:
                            if (confirmPrompt is not null)
                            {
                                CollapseToFinalView(items, startRow, dim, accent, confirmPrompt, accepted: false);
                                return false;
                            }

                            break;
                    }
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }
        finally
        {
            Console.Write("\x1b[?25h"); // show cursor
        }
    }

    private static void CollapseToFinalView(List<string> items, int startRow, string dim, string accent, string? confirmPrompt, bool accepted)
    {
        string brand = DotnetupTheme.Current.Brand;
        Console.SetCursorPosition(0, startRow);
        Console.Write("\x1b[J");
        foreach (var item in items)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, item.EscapeMarkup()));
        }

        if (confirmPrompt is not null)
        {
            string answer = accepted ? "Yes" : "No";
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "{0} [{1}]{2}[/]", confirmPrompt, brand, answer));
        }
    }

    /// <summary>
    /// Reads a single y/n keypress. Returns <paramref name="defaultValue"/> on Enter.
    /// </summary>
    private static bool ReadYesNo(bool defaultValue)
    {
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]{1}[/]", DotnetupTheme.Current.Brand, defaultValue ? "Yes" : "No"));
                    return defaultValue;
                case ConsoleKey.Y:
                    SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]Yes[/]", DotnetupTheme.Current.Brand));
                    return true;
                case ConsoleKey.N:
                    SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "[{0}]No[/]", DotnetupTheme.Current.Brand));
                    return false;
            }
        }
    }

    private static void RenderListWindow(List<string> items, int offset, int visibleCount, int startRow, bool firstRender, string? confirmPrompt)
    {
        string dim = DotnetupTheme.Current.Dim;
        string accent = DotnetupTheme.Current.Accent;

        if (!firstRender)
        {
            Console.SetCursorPosition(0, startRow);
            Console.Write("\x1b[J");
        }

        if (offset > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]▲ {1} more above[/]", dim, offset));
        }

        for (int i = offset; i < offset + visibleCount && i < items.Count; i++)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]• [{1}]{2}[/][/]", dim, accent, items[i].EscapeMarkup()));
        }

        int remaining = items.Count - offset - visibleCount;
        if (remaining > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}]▼ {1} more below (use ↑↓ arrows)[/]", dim, remaining));
        }

        if (confirmPrompt is not null)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "{0} [{1}]([bold underline]Y[/]/n)[/]", confirmPrompt, DotnetupTheme.Current.Brand));
        }
        else if (remaining <= 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, "  [{0}](Press Enter to continue)[/]", dim));
        }
    }
}
