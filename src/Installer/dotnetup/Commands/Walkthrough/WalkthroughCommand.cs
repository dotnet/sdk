// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
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
        SpectreAnsiConsole.MarkupLine("[bold]Welcome to dotnetup![/]");
        SpectreAnsiConsole.WriteLine(Strings.RootCommandDescription);
        SpectreAnsiConsole.WriteLine();

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
        SpectreAnsiConsole.MarkupLine("[bold]Step 1:[/] How would you like to access .NET?");
        SpectreAnsiConsole.WriteLine();

        var pathPreference = PromptPathPreference();
        bool setDefaultInstall = pathPreference == PathPreference.FullPathReplacement;

        // Step 2: Install SDK — wait for predownload to finish (cache is warm)
        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine("[bold]Step 2:[/] Install the .NET SDK");

        // Await predownload so the cache is populated before the real install begins.
        // Exceptions are already swallowed inside PredownloadToCacheAsync.
        predownloadTask.GetAwaiter().GetResult();

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

        // Step 3: Save config
        var config = new DotnetupConfigData
        {
            PathPreference = pathPreference,
        };
        DotnetupConfig.Write(config);

        SpectreAnsiConsole.WriteLine();
        SpectreAnsiConsole.MarkupLine("[green]Setup complete![/]");

        DisplayPathGuidance(pathPreference);

        return 0;
    }

    /// <summary>
    /// Prompts the user to choose how they want to access the dotnetup-managed dotnet
    /// using an interactive selector that shows all options with descriptions.
    /// </summary>
    private static PathPreference PromptPathPreference()
    {
        var options = new List<SelectableOption>
        {
            new("dotnetup dotnet", Strings.PathPreferenceDotnetupDotnet, Strings.PathDescriptionDotnetupDotnet),
            new("shell profile", Strings.PathPreferenceShellProfile, Strings.PathDescriptionShellProfile),
            new("system default", Strings.PathPreferenceFullReplacement, Strings.PathDescriptionFullReplacement),
        };

        int selected = InteractiveOptionSelector.Show("Choose how to configure your PATH:", options, defaultIndex: 2);

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
