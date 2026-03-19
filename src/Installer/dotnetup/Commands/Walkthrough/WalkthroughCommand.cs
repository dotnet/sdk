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
    private readonly Verbosity _verbosity = result.GetValue(CommonOptions.VerbosityOption);
    private readonly bool _requireMuxerUpdate = result.GetValue(CommonOptions.RequireMuxerUpdateOption);

    private readonly DotnetInstallManager _dotnetInstaller = new();
    private readonly ChannelVersionResolver _channelVersionResolver = new();

    protected override string GetCommandName() => "walkthrough";

    protected override int ExecuteCore()
    {
        ShowBanner();

        // Step 0: Explain channels and let the user pick one
        string selectedChannel = PromptChannel();

        var (channel, globalJson, installRoot) = ResolveChannelAndStartPredownload(selectedChannel);

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
        bool? setDefaultInstall = InstallWorkflow.ShouldReplaceSystemConfiguration(pathPreference);

        // Step 2: Prompt about admin installs before setting up the environment.
        // Both ShellProfile and FullPathReplacement shadow admin installs, so offer migration for both.
        if (InstallWorkflow.ShouldConvertSystemInstalls(pathPreference) && OperatingSystem.IsWindows())
        {
            setDefaultInstall = InstallWalkthrough.PromptAdminMigration(_dotnetInstaller);
        }

        // Install SDK — validate the install path early so the user sees any conflict
        // before the download output, not after.
        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine("Setting up your environment.");
        ValidateInstallPathOrThrow(installRoot, _manifestPath);
        DisplayInstallLocation(globalJson);

        var workflowResult = RunInstallWorkflow(channel, pathPreference, setDefaultInstall);

        // Step 3: Save config — show guidance and "Setup complete!" before migrating admin installs
        SaveConfigAndDisplayResult(pathPreference);

        // Step 4: Migrate admin installs after setup is complete so the user knows they can start working
        RunDeferredAdminInstalls(workflowResult, pathPreference, setDefaultInstall);

        return 0;
    }

    private static void ShowBanner()
    {
        SpectreAnsiConsole.Write(DotnetBotBanner.BuildPanel());
        SpectreAnsiConsole.WriteLine();
    }

    /// <summary>
    /// Explains how dotnetup channels work and lets the user pick a channel.
    /// Builds example channels dynamically from the release manifest and shows
    /// what each one currently resolves to.
    /// </summary>
    private string PromptChannel()
    {
        string brand = DotnetupTheme.Current.Brand;
        string dim = DotnetupTheme.Current.Dim;

        SpectreAnsiConsole.MarkupLine($"Welcome to [{brand} bold]dotnetup[/]!");
        SpectreAnsiConsole.WriteLine();

        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "dotnetup updates and groups installations using [{0} bold]dotnetup channels[/].",
            brand));
        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]Channels may be implied from your global.json.[/]",
            dim));
        SpectreAnsiConsole.WriteLine();

        var examples = BuildChannelExamples();

        var prompt = new SelectionPrompt<ChannelExample>()
            .Title("[bold]Select an example channel to get started:[/]")
            .PageSize(examples.Count - 1)
            .HighlightStyle(Style.Parse(brand))
            .MoreChoicesText(string.Format(CultureInfo.InvariantCulture, "[{0}](use {1}{2} arrows)[/]", dim, Constants.Symbols.UpArrow, Constants.Symbols.DownArrow))
            .UseConverter(ex =>
            {
                string resolved = ex.ResolvedVersion is not null
                    ? string.Format(CultureInfo.InvariantCulture, "[{0}] {1} {2}[/]", dim, Constants.Symbols.RightArrow, ex.ResolvedVersion)
                    : string.Format(CultureInfo.InvariantCulture, "[{0}] (no version available)[/]", dim);
                string suggested = ex.Channel == ChannelVersionResolver.LatestChannel
                    ? " [white](suggested)[/]"
                    : "";
                return string.Format(CultureInfo.InvariantCulture, "[bold {0}]{1}[/]{2}  [{3}]{4}[/] {5}",
                    brand,
                    ex.Channel.EscapeMarkup().PadRight(12),
                    suggested,
                    dim,
                    ex.Description.EscapeMarkup(),
                    resolved);
            });

        prompt.AddChoices(examples);

        if (Console.IsInputRedirected)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]Using default channel: [{1}]latest[/][/]",
                dim, brand));
            return ChannelVersionResolver.LatestChannel;
        }

        var selected = SpectreAnsiConsole.Prompt(prompt);
        SpectreAnsiConsole.WriteLine();
        return selected.Channel;
    }

    /// <summary>
    /// Builds a list of example channels with descriptions and resolved versions.
    /// Uses the release manifest to find the latest major version dynamically.
    /// </summary>
    private List<ChannelExample> BuildChannelExamples()
    {
        var resolver = _channelVersionResolver;

        var latestResolved = resolver.GetLatestVersionForChannel(
            new UpdateChannel(ChannelVersionResolver.LatestChannel), InstallComponent.SDK);
        string? ltsVersion = resolver.GetLatestVersionForChannel(
            new UpdateChannel(ChannelVersionResolver.LtsChannel), InstallComponent.SDK)?.ToString();
        string? previewVersion = resolver.GetLatestVersionForChannel(
            new UpdateChannel(ChannelVersionResolver.PreviewChannel), InstallComponent.SDK)?.ToString();

        var examples = new List<ChannelExample>
        {
            new(ChannelVersionResolver.LatestChannel, "Latest stable release", latestResolved?.ToString()),
            new(ChannelVersionResolver.LtsChannel, "Long Term Support", ltsVersion),
            new(ChannelVersionResolver.PreviewChannel, "Latest preview", previewVersion),
        };

        if (latestResolved is not null)
        {
            string latestVersion = latestResolved.ToString();
            string majorMinor = FormattableString.Invariant($"{latestResolved.Major}.{latestResolved.Minor}");
            string featureBand = FormattableString.Invariant($"{latestResolved.Major}.{latestResolved.Minor}.{latestResolved.SdkFeatureBand / 100}xx");

            examples.Add(new(majorMinor, "Major.Minor channel", latestVersion));
            examples.Add(new(featureBand, "SDK feature band", latestVersion));
            examples.Add(new(latestVersion, "Explicit version", latestVersion));
        }

        return examples;
    }

    private sealed record ChannelExample(string Channel, string Description, string? ResolvedVersion);

    /// <summary>
    /// Resolves the install channel from global.json (or the user's selection) and fires off a
    /// background predownload to warm the cache while the user answers prompts.
    /// </summary>
    private (string Channel, GlobalJsonInfo? GlobalJson, DotnetInstallRoot InstallRoot) ResolveChannelAndStartPredownload(string selectedChannel)
    {
        var globalJson = _dotnetInstaller.GetGlobalJsonInfo(Environment.CurrentDirectory);
        string? channelFromGlobalJson = globalJson?.GlobalJsonPath is not null
            ? GlobalJsonChannelResolver.ResolveChannel(globalJson.GlobalJsonPath)
            : null;
        string channel = channelFromGlobalJson ?? selectedChannel;

        var installRoot = new DotnetInstallRoot(
            _installPath ?? _dotnetInstaller.GetDefaultDotnetInstallPath(),
            InstallerUtilities.GetDefaultInstallArchitecture());

        _ = InstallerOrchestratorSingleton.PredownloadToCacheAsync(
            channel, InstallComponent.SDK, installRoot);

        return (channel, globalJson, installRoot);
    }

    private InstallWorkflow.InstallWorkflowResult RunInstallWorkflow(string channel, PathPreference pathPreference, bool? setDefaultInstall)
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
            ComponentDescription: InstallComponent.SDK.GetDisplayName(),
            UpdateGlobalJson: null,
            ResolveChannelFromGlobalJson: GlobalJsonChannelResolver.ResolveChannel,
            RequireMuxerUpdate: _requireMuxerUpdate,
            PathPreference: pathPreference,
            Verbosity: _verbosity);
        return workflow.Execute(options);
    }

    /// <summary>
    /// Gets admin installs that should be migrated and installs them after the primary setup is complete.
    /// </summary>
    private void RunDeferredAdminInstalls(
        InstallWorkflow.InstallWorkflowResult workflowResult,
        PathPreference pathPreference,
        bool? setDefaultInstall)
    {
        if (!InstallWorkflow.ShouldConvertSystemInstalls(pathPreference) || setDefaultInstall != true)
        {
            return;
        }

        var adminInstalls = _dotnetInstaller.GetExistingSystemInstalls();
        if (adminInstalls.Count == 0 || workflowResult.InstallRoot is null)
        {
            return;
        }

        // Exclude the version we just installed
        var seen = new HashSet<(InstallComponent, string)>();
        if (workflowResult.ResolvedVersion is not null)
        {
            seen.Add((InstallComponent.SDK, workflowResult.ResolvedVersion.ToString()));
        }

        var toMigrate = adminInstalls.Where(i => seen.Add((i.Component, i.Version.ToString()))).ToList();
        if (toMigrate.Count == 0)
        {
            return;
        }

        SpectreAnsiConsole.MarkupLine(DotnetupTheme.Dim(
            "You may now use dotnetup. In the meantime, we'll install your remaining components."));
        InstallExecutor.ExecuteAdditionalInstalls(
            toMigrate,
            workflowResult.InstallRoot,
            workflowResult.ManifestPath,
            workflowResult.NoProgress,
            workflowResult.RequireMuxerUpdate);
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
