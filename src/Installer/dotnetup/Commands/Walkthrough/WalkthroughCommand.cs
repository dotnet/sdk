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
        // This warms the download cache so the real install can benefit from a cache hit.
        // Intentionally fire-and-forget: the install will show real download progress if
        // the predownload hasn't finished yet, or skip the download via cache hit if it has.
        var installRoot = new DotnetInstallRoot(
            _installPath ?? _dotnetInstaller.GetDefaultDotnetInstallPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());

        _ = InstallerOrchestratorSingleton.PredownloadToCacheAsync(
            channel, InstallComponent.SDK, installRoot);

        // Step 1: Choose how to access .NET
        var pathPreference = PromptPathPreference();

        if (pathPreference == PathPreference.FullPathReplacement && !OperatingSystem.IsWindows())
        {
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Error(Strings.PathReplacementModeUnixError));
            return 1;
        }

        // Both FullPathReplacement and ShellProfile shadow the system PATH, so
        // dotnetup needs to be the default install for both modes.
        // DotnetupDotnet (isolation) doesn't touch PATH at all.
        bool? setDefaultInstall = pathPreference != PathPreference.DotnetupDotnet;

        // Step 2: Prompt about admin installs before setting up the environment.
        // Both ShellProfile and FullPathReplacement shadow admin installs, so offer migration for both.
        if (pathPreference != PathPreference.DotnetupDotnet && OperatingSystem.IsWindows())
        {
            setDefaultInstall = InstallWalkthrough.PromptAdminMigration(_dotnetInstaller);
        }

        // Install SDK — validate the install path early so the user sees any conflict
        // before the download output, not after.
        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine("Setting up your environment.");
        ValidateInstallPathOrThrow(installRoot, _manifestPath);
        DisplayInstallLocation(globalJson);

        // Don't await the predownload — let the install show real download progress.
        // If the predownload already finished and warmed the cache, the install
        // benefits automatically via a cache hit. Otherwise, the install downloads
        // normally with a visible progress bar.

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

    private void DisplayInstallLocation(GlobalJsonInfo? globalJson)
    {
        if (globalJson?.SdkPath is not null)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]Installing to [{1}]{2}[/] as controlled by global.json file [{1}]{3}[/].[/]",
                DotnetupTheme.Current.Dim,
                DotnetupTheme.Current.Accent,
                globalJson.SdkPath.EscapeMarkup(),
                globalJson.GlobalJsonPath!.EscapeMarkup()));
        }
        else
        {
            string resolvedInstallPath = _installPath ?? _dotnetInstaller.GetDefaultDotnetInstallPath();
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]You can find dotnetup managed installs at [{1}]{2}[/].[/]",
                DotnetupTheme.Current.Dim,
                DotnetupTheme.Current.Accent,
                resolvedInstallPath.EscapeMarkup()));
        }
    }

    private static void SaveConfigAndDisplayResult(PathPreference pathPreference)
    {
        var config = new DotnetupConfigData
        {
            PathPreference = pathPreference,
        };
        DotnetupConfig.Write(config);
        DisplayPathGuidance(pathPreference);
        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine(DotnetupTheme.Brand("Setup complete!"));
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
    /// Shows guidance based on the chosen path preference.
    /// </summary>
    private static void DisplayPathGuidance(PathPreference preference)
    {
        SpectreAnsiConsole.WriteLine();
        string? guidance = preference switch
        {
            PathPreference.DotnetupDotnet => Strings.PathGuidanceDotnetupDotnet,
            PathPreference.ShellProfile => Strings.PathGuidanceShellProfile,
            PathPreference.FullPathReplacement => Strings.PathGuidanceFullReplacement,
            _ => null,
        };

        if (guidance is not null)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]{1}[/]",
                DotnetupTheme.Current.Dim,
                guidance.EscapeMarkup()));
        }
    }

    /// <summary>
    /// Checks whether the target install path already contains .NET artifacts that are not
    /// tracked by dotnetup. Mirrors the conflict check in
    /// <see cref="InstallerOrchestratorSingleton.PrepareInstall"/> so we can surface the
    /// error before any download work begins.
    /// </summary>
    private static void ValidateInstallPathOrThrow(DotnetInstallRoot installRoot, string? manifestPath)
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifestData = new DotnetupSharedManifest(manifestPath).ReadManifest();
        if (!InstallerOrchestratorSingleton.IsRootInManifest(manifestData, installRoot)
            && InstallerOrchestratorSingleton.HasDotnetArtifacts(installRoot.Path))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.Unknown,
                $"The install path '{installRoot.Path}' already contains a .NET installation that is not tracked by dotnetup. " +
                "To avoid conflicts, use a different install path or remove the existing installation first.");
        }
    }
}
