// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using System.Threading.Channels;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

/// <summary>
/// Runs the interactive walkthrough that installs the .NET SDK with defaults
/// and records the user's path replacement preference to <c>dotnetup.config.json</c>.
/// </summary>
internal class WalkthroughWorkflows()
{
    private sealed record ChannelExample(string Channel, string Description, string? ResolvedVersion);

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

    // Walkthrough Wrappers
    // These functions orchestrate the overall flow of the walkthrough, calling into the shared InstallWorkflow and InstallWalkthrough functions as needed.

    // call this from walkthrough command / initial program.cs walkthrough setup
    public FullIntroductionWalkthrough()
    {
        ShowBanner();

        // Step 0: Explain channels and let the user pick one
        string selectedChannel = PromptChannel();

        // ask install workflow to create a resolved install request based on the channel without duplicating code
        // install workflow should handle the global.json parsing, and path resolution

        BaseConfigurationWalkthrough();
    }

    // call this from install commands if and only if we arent in interactive mode or full path is specified
    public BaseConfigurationWalkthrough(resolvedInstallRequest, installfunction)
    {
        _ = InstallerOrchestratorSingleton.PredownloadToCacheAsync(
            channel, InstallComponent.SDK, installRoot);

        var pathPreference = PromptPathPreference();

        if (pathPreference == PathPreference.FullPathReplacement && !OperatingSystem.IsWindows())
        {
            SpectreAnsiConsole.MarkupLine(DotnetupTheme.Error(Strings.PathReplacementModeUnixError));
            return 1;
        }

        // Both FullPathReplacement and ShellProfile shadow the system PATH, so
        // dotnetup needs to be the default install for both modes.
        // DotnetupDotnet (isolation) doesn't touch PATH at all.
        bool replaceSystemConfig = InstallWalkthrough.ShouldReplaceSystemConfiguration(pathPreference);

        // Step 2: Prompt about admin installs before setting up the environment.
        // Both ShellProfile and FullPathReplacement shadow admin installs, so offer migration for both.
        // Track the migration decision separately — accepting migration should copy installs
        // but only FullPathReplacement should trigger system PATH changes (elevation).
        List<DotnetInstall> toMigrate = PromptInstallsToMigrateIfDesired(_dotnetInstaller, pathPreference);

        SpectreAnsiConsole.MarkupLine("Setting up your environment.");
        DisplayInstallLocation(globalJson);

        // Step 2: Run the install workflow, which will download and set up the SDK
        // Install SDK — validate the install path early so the user sees any conflict
        // before the download output, not after.
        var workflowResult = installfunction(channel);

        // Step 3: Save config — show guidance and "Setup complete!" before migrating admin installs
        SaveConfigAndDisplayResult(pathPreference);


        SpectreAnsiConsole.MarkupLine(DotnetupTheme.Dim(
            "You may now use dotnetup. In the meantime, we'll install your remaining components."));
        // make sure we get rid of the one we already installed first
        InstallExecutor.ExecuteAdditionalInstalls(
            toMigrate,
            workflowResult.InstallRoot,
            workflowResult.ManifestPath,
            workflowResult.NoProgress,
            workflowResult.RequireMuxerUpdate);
    }

    // Prompt Functions (Interactive Engagements)

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
    /// Prompts the user to choose how they want to access the dotnetup-managed dotnet
    /// using an interactive selector that shows all options with descriptions and tooltips.
    /// </summary>
    internal static PathPreference PromptPathPreference()
    {
        DotnetupConfig.GetExistingPathPreference();
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
            new("t", Strings.PathPreferenceShellProfile,   isWindows ? Strings.PathDescriptionShellProfile : Strings.PathDescriptionShellProfileBase,   terminalTooltip),
        };

        if (isWindows)
        {
            options.Add(new("r", Strings.PathPreferenceFullReplacement, Strings.PathDescriptionFullReplacement, Strings.PathTooltipFullReplacement));
        }

        int selected = InteractiveOptionSelector.Show("How would you like to use dotnetup?", options, defaultIndex: 1);

        var selectedPreference = selected switch
        {
            0 => PathPreference.DotnetupDotnet,
            1 => PathPreference.ShellProfile,
            _ => PathPreference.FullPathReplacement,
        };
    }

    /// <summary>
    /// Prompts the user about copying admin-managed installs into the dotnetup-managed directory.
    /// </summary>
    /// <returns>A list of installs to migrate if the user agrees, or an empty list if they decline or no system installs exist.</returns>
    internal static List<DotnetInstall> PromptInstallsToMigrateIfDesired(IDotnetInstallManager dotnetInstaller, PathPreference pathPreference)
    {
        if (!InstallWalkthrough.ShouldPromptToConvertSystemInstalls(pathPreference))
        {
            return [];
        }

        var systemInstalls = dotnetInstaller.GetExistingSystemInstalls();
        if (systemInstalls.Count == 0)
        {
            // Nothing to migrate — don't override the caller's initial choice.
            return [];
        }

        // Find the system install path for display purposes
        var currentInstall = dotnetInstaller.GetCurrentPathConfiguration();
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

        // Separate out spacing for the next prompt
        SpectreAnsiConsole.WriteLine();

        return userAcceptedMigration ? systemInstalls : [];
    }

    // Save Settings


    /// <summary>
    /// Determines whether global.json should be updated based on channel mismatch.
    /// </summary>
    /// <param name="channelFromGlobalJson">The channel from global.json.</param>
    /// <returns>True if global.json should be updated, false otherwise, or null if not determined.</returns>
    public bool? ShouldUpdateGlobalJsonFile(string? channelFromGlobalJson)
    {
        if (channelFromGlobalJson is not null && _options.VersionOrChannel is not null &&
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

    private void ApplyPostInstallConfiguration(WorkflowContext context, InstallExecutor.ResolvedInstallRequest resolved)
    {
        if (context.ReplaceSystemConfig)
        {
            _dotnetInstaller.ConfigureInstallType(InstallType.User, context.InstallPath);
        }

        if (context.UpdateGlobalJson == true && context.GlobalJson?.GlobalJsonPath is not null)
        {
            _dotnetInstaller.UpdateGlobalJson(
                context.GlobalJson.GlobalJsonPath,
                resolved.ResolvedVersion!.ToString());
        }
    }

    // Display Functions:

    private static void ShowBanner()
    {
        SpectreAnsiConsole.Write(DotnetBotBanner.BuildPanel());
        SpectreAnsiConsole.WriteLine();
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

        ApplyPostInstallConfiguration(context, resolved);

        SpectreAnsiConsole.MarkupLine(DotnetupTheme.Brand("Setup complete!"));
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
}
