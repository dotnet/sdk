// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Self;

/// <summary>Updates the canonical dotnetup executable from the requested release channel.</summary>
internal sealed class SelfUpdateCommand(ParseResult result) : CommandBase(result, "self/update")
{
    private readonly string? _channel = result.GetValue(SelfCommandParser.ChannelOption);
    private readonly bool _force = result.GetValue(SelfCommandParser.ForceOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly Func<DotnetDownloader> _createDownloader = static () => new DotnetDownloader();

    internal SelfUpdateCommand(ParseResult result, Func<DotnetDownloader> createDownloader) : this(result)
    {
        ArgumentNullException.ThrowIfNull(createDownloader);
        _createDownloader = createDownloader;
    }

    protected override bool SafeDuringSelfUpdate => true;

    protected override void ExecuteCore()
    {
        var invocation = SelfUpdateInvocation.Current ?? throw new DotnetInstallException(
            DotnetInstallErrorCode.ContextResolutionFailed, Strings.SelfUpdateUnsupportedHost);
        var channel = _channel ?? SelfUpdateDefaultChannel.FromLoadedVersion(invocation.LoadedVersion);
        var downloader = _createDownloader();
        var rid = DotnetupUtilities.GetRuntimeIdentifier(InstallerUtilities.GetDefaultInstallArchitecture());
        var workflow = new SelfUpdateWorkflow(invocation.Paths, invocation.LoadedVersion,
            () => downloader.ResolveDotnetupDownload(channel, rid),
            (release, destination) =>
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Warning(Microsoft.Dotnet.Installation.Strings.UnsignedBlobFeedWarning.EscapeMarkup()));
                SelfUpdateDownloadProgress.Run(_noProgress,
                    progress => downloader.DownloadWithVerification(release, destination, progress));
            })
        {
            Force = _force,
            OnForcedUpdate = (installed, available) => AnsiConsole.MarkupLine(DotnetupTheme.Warning(string.Format(
                CultureInfo.CurrentCulture, Strings.SelfUpdateForcedWarning, installed, available).EscapeMarkup())),
        };
        var result = workflow.ExecuteWithResult(invocation.Retain);
        if (result.WasUpdated)
        {
            AnsiConsole.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.SelfUpdateSucceeded, result.AvailableVersion));
            return;
        }

        bool currentIsNewer = result.InstalledVersion.ComparePrecedenceTo(result.AvailableVersion) > 0;
        string message = currentIsNewer
            ? Strings.SelfUpdateCurrentVersionNewer
            : Strings.SelfUpdateAlreadyUpToDate;
        Console.Error.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            message,
            result.InstalledVersion,
            result.AvailableVersion));

        // Official daily builds share the preview label, so a daily build newer than the preview
        // channel defaults to that channel; point it at --channel daily instead.
        if (currentIsNewer && _channel is null && channel == SelfUpdateDefaultChannel.Preview)
        {
            Console.Error.WriteLine(Strings.SelfUpdateMaybeDailyBuild);
        }
    }
}