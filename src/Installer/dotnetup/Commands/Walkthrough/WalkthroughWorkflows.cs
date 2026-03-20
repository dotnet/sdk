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

    // call this from walkthrough command / initial program.cs walkthrough setup
    public FullIntroductionWalkthrough()
    {
        ShowBanner();

        // Step 0: Explain channels and let the user pick one
        string selectedChannel = PromptChannel();

        var (channel, globalJson, installRoot) = ResolveChannelAndStartPredownload(selectedChannel);

        BaseConfigurationWalkthrough();
    }

    // call this from install commands if and only if we arent in interactive mode or full path is specified
    public BaseConfigurationWalkthrough(channel, installfunction)
    {
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
        bool? replaceSystemConfig = InstallWalkthrough.ShouldReplaceSystemConfiguration(pathPreference);

        // Step 2: Prompt about admin installs before setting up the environment.
        // Both ShellProfile and FullPathReplacement shadow admin installs, so offer migration for both.
        // Track the migration decision separately — accepting migration should copy installs
        // but only FullPathReplacement should trigger system PATH changes (elevation).
        List<DotnetInstall> toMigrate = InstallWalkthrough.GetInstallsToMigrateIfDesired(_dotnetInstaller, pathPreference);

        SpectreAnsiConsole.MarkupLine("Setting up your environment.");
        ValidateInstallPathOrThrow(installRoot, _manifestPath);
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
            new("t", Strings.PathPreferenceShellProfile,   isWindows ? Strings.PathDescriptionShellProfile : Strings.PathDescriptionShellProfileBase,   terminalTooltip),
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
}
