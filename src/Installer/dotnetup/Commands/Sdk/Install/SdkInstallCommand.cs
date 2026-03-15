// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal class SdkInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string[] _channels = result.GetValue(SdkInstallCommandParser.ChannelArguments) ?? [];
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(CommonOptions.SetDefaultInstallOption);
    private readonly bool? _updateGlobalJson = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly bool _interactive = result.GetValue(CommonOptions.InteractiveOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly bool _requireMuxerUpdate = result.GetValue(CommonOptions.RequireMuxerUpdateOption);
    private readonly bool _untracked = result.GetValue(CommonOptions.UntrackedOption);

    private readonly DotnetInstallManager _dotnetInstaller = new();
    private readonly ChannelVersionResolver _channelVersionResolver = new();

    protected override string GetCommandName() => "sdk/install";

    protected override int ExecuteCore()
    {
        // Single channel (or none): use existing InstallWorkflow for full walkthrough support
        if (_channels.Length <= 1)
        {
            string? singleChannel = _channels.Length == 1 ? _channels[0] : null;
            return ExecuteSingleInstall(singleChannel);
        }

        // Multiple channels: validate all upfront, then download concurrently and commit sequentially
        return ExecuteMultipleInstalls(_channels);
    }

    private int ExecuteSingleInstall(string? versionOrChannel)
    {
        var pathPreference = DotnetupConfig.EnsurePathPreference(_interactive);
        bool? setDefault = _setDefaultInstall ?? (pathPreference == PathPreference.FullPathReplacement ? true : null);

        var workflow = new InstallWorkflow(_dotnetInstaller, _channelVersionResolver);

        var options = new InstallWorkflow.InstallWorkflowOptions(
            versionOrChannel,
            _installPath,
            setDefault,
            _manifestPath,
            _interactive,
            _noProgress,
            InstallComponent.SDK,
            ".NET SDK",
            _updateGlobalJson,
            GlobalJsonChannelResolver.ResolveChannel,
            _requireMuxerUpdate,
            _untracked,
            pathPreference);

        workflow.Execute(options);
        return 0;
    }

    private int ExecuteMultipleInstalls(string[] channels)
    {
        string installPath = _installPath ?? _dotnetInstaller.GetDefaultDotnetInstallPath();
        var installRoot = new DotnetInstallRoot(installPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var requests = channels.Select(channel => new DotnetInstallRequest(
            installRoot,
            new UpdateChannel(channel),
            InstallComponent.SDK,
            new InstallRequestOptions
            {
                ManifestPath = _manifestPath,
                RequireMuxerUpdate = _requireMuxerUpdate,
                Untracked = _untracked
            })).ToList();

        IProgressTarget progressTarget = _noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();
        using var sharedReporter = progressTarget.CreateProgressReporter();

        var results = InstallerOrchestratorSingleton.Instance.InstallMany(requests, sharedReporter);

        DisplayMultiInstallResults(results);

        if (_setDefaultInstall == true)
        {
            _dotnetInstaller.ConfigureInstallType(InstallType.User, installPath);
        }

        return 0;
    }

    private static void DisplayMultiInstallResults(IReadOnlyList<InstallResult> results)
    {
        var installed = new List<string>();
        var alreadyInstalled = new List<string>();
        string? sharedPath = null;

        string accent = DotnetupTheme.Current.Accent;

        foreach (var installResult in results)
        {
            string description = string.Format(CultureInfo.InvariantCulture, ".NET SDK [{0}]{1}[/]", accent, installResult.Install.Version.ToString().EscapeMarkup());
            sharedPath ??= installResult.Install.InstallRoot.Path;
            if (installResult.WasAlreadyInstalled)
            {
                alreadyInstalled.Add(description);
            }
            else
            {
                installed.Add(description);
            }
        }

        string escapedPath = sharedPath?.EscapeMarkup() ?? string.Empty;

        if (installed.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "Installed {0} at [{1}]{2}[/]", string.Join(", ", installed), accent, escapedPath));
        }

        if (alreadyInstalled.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "{0} already installed at [{1}]{2}[/]", string.Join(", ", alreadyInstalled), accent, escapedPath));
        }
    }
}
