// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

/// <summary>
/// Runs the interactive walkthrough that installs the .NET SDK with defaults
/// and records the user's path replacement preference to <c>dotnetup.config.json</c>.
/// </summary>
internal class WalkthroughCommand(ParseResult result) : CommandBase(result)
{
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly bool _requireMuxerUpdate = result.GetValue(CommonOptions.RequireMuxerUpdateOption);

    private readonly DotnetInstallManager _dotnetInstaller = new();
    private readonly ChannelVersionResolver _channelVersionResolver = new();

    protected override string GetCommandName() => "walkthrough";

    protected override int ExecuteCore()
    {

        // Resolve the channel early so we can start predownloading.
        // This uses the same logic as InstallWorkflow: check global.json first, fall back to "latest".
        var globalJson = _dotnetInstaller.GetGlobalJsonInfo(Environment.CurrentDirectory);
        string? channelFromGlobalJson = globalJson?.GlobalJsonPath is not null
            ? GlobalJsonChannelResolver.ResolveChannel(globalJson.GlobalJsonPath)
            : null;
        string channel = channelFromGlobalJson ?? "latest";

        // Start predownloading the archive in the background while the user answers prompts.
        // This warms the download cache so the real install skips the download entirely.
        var installRoot = new DotnetInstallRoot(
            _installPath ?? _dotnetInstaller.GetDefaultDotnetInstallPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());
        var predownloadTask = InstallerOrchestratorSingleton.PredownloadToCacheAsync(
            channel, InstallComponent.SDK, installRoot);

        // Step 1: Choose how to access .NET
        var pathPreference = PromptPathPreference();

        if (pathPreference == PathPreference.FullPathReplacement && !OperatingSystem.IsWindows())
        {
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Error(Strings.PathReplacementModeUnixError));
            return 1;
        }

        // For isolation mode, we know we don't need to set the default install.
        // For other modes, pass null so InstallWalkthrough can prompt about admin migration.
        bool? setDefaultInstall = pathPreference == PathPreference.DotnetupDotnet ? false : null;

        // Step 2: Prompt about admin installs before setting up the environment.
        // Always check in walkthrough mode regardless of current configuration,
        // because a previous walkthrough may have configured a user install while
        // admin SDKs still exist in Program Files.
        if (setDefaultInstall is null && OperatingSystem.IsWindows())
        {
            setDefaultInstall = PromptAdminMigration(installRoot.Path);
        }

        // Install SDK — wait for predownload to finish (cache is warm)
        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine("Setting up your environment.");

        // Await predownload so the cache is populated before the real install begins.
        // Exceptions are already swallowed inside PredownloadToCacheAsync.
        predownloadTask.GetAwaiter().GetResult();

        RunInstallWorkflow(channel, pathPreference, setDefaultInstall);

        // Step 3: Save config
        SaveConfigAndDisplayResult(pathPreference);
        return 0;
    }

    private void RunInstallWorkflow(string channel, PathPreference pathPreference, bool? setDefaultInstall)
    {
        var workflow = new InstallWorkflow(_dotnetInstaller, _channelVersionResolver);
        var options = new InstallWorkflow.InstallWorkflowOptions(
            VersionOrChannel: channel,
            InstallPath: _installPath,
            SetDefaultInstall: setDefaultInstall,
            ManifestPath: _manifestPath,
            Interactive: true,
            NoProgress: _noProgress,
            Component: InstallComponent.SDK,
            ComponentDescription: ".NET SDK",
            UpdateGlobalJson: null,
            ResolveChannelFromGlobalJson: GlobalJsonChannelResolver.ResolveChannel,
            RequireMuxerUpdate: _requireMuxerUpdate,
            PathPreference: pathPreference);
        workflow.Execute(options);
    }

    private static void SaveConfigAndDisplayResult(PathPreference pathPreference)
    {
        var config = new DotnetupConfigData
        {
            PathPreference = pathPreference,
        };
        DotnetupConfig.Write(config);
        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine(DotnetupTheme.Success("Setup complete!"));
        DisplayPathGuidance(pathPreference);
    }

    /// <summary>
    /// Prompts the user to choose how they want to access the dotnetup-managed dotnet
    /// using an interactive selector that shows all options with descriptions and tooltips.
    /// </summary>
    internal static PathPreference PromptPathPreference()
    {
        bool isWindows = OperatingSystem.IsWindows();

        string isolationTooltip = string.Format(
            CultureInfo.InvariantCulture,
            Strings.PathTooltipDotnetupDotnet,
            isWindows ? "Program Files" : "/usr/local");

        string terminalTooltip = isWindows
            ? Strings.PathTooltipShellProfile + " " + Strings.PathTooltipShellProfileWindowsNote
            : Strings.PathTooltipShellProfile;

        var options = new List<SelectableOption>
        {
            new("i", Strings.PathPreferenceDotnetupDotnet, Strings.PathDescriptionDotnetupDotnet, isolationTooltip),
            new("t", Strings.PathPreferenceShellProfile,   Strings.PathDescriptionShellProfile,   terminalTooltip),
        };

        if (isWindows)
        {
            options.Add(new("r", Strings.PathPreferenceFullReplacement, Strings.PathDescriptionFullReplacement, Strings.PathTooltipFullReplacement));
        }

        int selected = InteractiveOptionSelector.Show("How would you like to use dotnetup?", options, defaultIndex: 1);

        return selected switch
        {
            0 => PathPreference.DotnetupDotnet,
            1 => PathPreference.ShellProfile,
            _ => PathPreference.FullPathReplacement,
        };
    }

    /// <summary>
    /// Checks for admin .NET SDK installs and prompts the user about migrating them
    /// to a dotnetup-managed user install. Always runs in walkthrough mode regardless
    /// of current configuration.
    /// </summary>
    /// <param name="resolvedInstallPath">The resolved user install path.</param>
    /// <returns>True if the user wants to set the user install as default, false if they decline, null if no admin installs found.</returns>
    private bool? PromptAdminMigration(string resolvedInstallPath)
    {
        var adminSdks = _dotnetInstaller.GetInstalledAdminSdkVersions();
        if (adminSdks.Count == 0)
        {
            return null;
        }

        // Find the admin install path for display purposes
        var currentInstall = _dotnetInstaller.GetConfiguredInstallType();
        string adminPath = currentInstall?.InstallType == InstallType.Admin
            ? currentInstall.Path
            : adminSdks.Count > 0 && OperatingSystem.IsWindows()
                ? WindowsPathHelper.GetProgramFilesDotnetPaths().FirstOrDefault() ?? "Program Files\\dotnet"
                : "the system .NET location";

        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine($"You have an existing admin install of .NET in [{DotnetupTheme.Current.Accent}]{adminPath.EscapeMarkup()}[/].");
        SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]The following installs would be converted to be owned by dotnetup:[/]");
        InstallWalkthrough.RenderScrollableList(adminSdks, visibleCount: 3);

        SpectreAnsiConsole.MarkupLine($"We can configure your system to use the new install of .NET in [{DotnetupTheme.Current.Accent}]{resolvedInstallPath.EscapeMarkup()}[/] instead.");
        SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]This would mean that the admin install of .NET would no longer be accessible from the PATH or from Visual Studio.[/]");
        SpectreAnsiConsole.MarkupLine($"[{DotnetupTheme.Current.Dim}]You can change this later with \"dotnetup defaultinstall\".[/]");

        return SpectreAnsiConsole.Confirm(
            $"Do you want to set the user install path ({resolvedInstallPath}) as the default dotnet install? This will update the PATH and DOTNET_ROOT environment variables.",
            defaultValue: true);
    }

    /// <summary>
    /// Shows guidance based on the chosen path preference.
    /// </summary>
    private static void DisplayPathGuidance(PathPreference preference)
    {
        SpectreAnsiConsole.WriteLine();
        switch (preference)
        {
            case PathPreference.DotnetupDotnet:
                SpectreAnsiConsole.WriteLine(Strings.PathGuidanceDotnetupDotnet);
                break;
            case PathPreference.ShellProfile:
                SpectreAnsiConsole.WriteLine(Strings.PathGuidanceShellProfile);
                break;
            case PathPreference.FullPathReplacement:
                SpectreAnsiConsole.WriteLine(Strings.PathGuidanceFullReplacement);
                break;
        }
    }
}
