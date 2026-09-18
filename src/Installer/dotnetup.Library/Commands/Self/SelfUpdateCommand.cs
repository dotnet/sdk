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
    private readonly string _channel = result.GetValue(SelfCommandParser.ChannelOption)!;
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
        var downloader = _createDownloader();
        var rid = DotnetupUtilities.GetRuntimeIdentifier(InstallerUtilities.GetDefaultInstallArchitecture());
        var workflow = new SelfUpdateWorkflow(invocation.Paths, invocation.LoadedIdentity,
            () => downloader.ResolveDotnetupDownload(_channel, rid),
            (release, destination) =>
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Warning(Microsoft.Dotnet.Installation.Strings.UnsignedBlobFeedWarning.EscapeMarkup()));
                SelfUpdateDownloadProgress.Run(_noProgress,
                    progress => downloader.DownloadWithVerification(release, destination, progress));
            });
        var version = workflow.Execute(invocation.Retain);
        AnsiConsole.WriteLine(version is null
            ? Strings.SelfUpdateAlreadyUpToDate
            : string.Format(CultureInfo.InvariantCulture, Strings.SelfUpdateSucceeded, version));
    }
}