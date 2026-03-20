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




        return 0;
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

    private InstallWorkflow.InstallWorkflowResult RunInstallWorkflow(string channel, PathPreference pathPreference, bool? replaceSystemConfig)
    {
        var workflow = new InstallWorkflow(_dotnetInstaller, _channelVersionResolver);
        var options = new InstallWorkflow.InstallWorkflowOptions(
            VersionOrChannel: channel,
            InstallPath: _installPath,
            ReplaceSystemConfig: replaceSystemConfig,
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
