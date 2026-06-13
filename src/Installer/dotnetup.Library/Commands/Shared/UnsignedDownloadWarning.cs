// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Shows the "downloads from the unsigned blob feed" warning at most once per process.
/// The warning is shown up-front when a request's channel makes the blob feed predictable, and
/// — as a backstop — after an install when the downloader actually fell back to the blob feed for
/// a case the up-front prediction could not detect (e.g. a roll-forward band channel that resolves
/// to a blob-feed-only preview, as happens when migrating a system preview install). The download
/// path itself cannot print (it has no console and runs during the progress display), so it only
/// records that a fallback occurred via <see cref="UnsignedSourcePolicy.MarkUnsignedFallbackUsed"/>.
/// </summary>
internal static class UnsignedDownloadWarning
{
    /// <summary>
    /// Shows the warning once if any of the given requests is predicted to use the blob feed.
    /// </summary>
    public static void WarnIfPredicted(IEnumerable<DotnetInstallRequest> requests)
    {
        if (requests.Any(UnsignedSourcePolicy.MayDownloadUnsigned) && UnsignedSourcePolicy.TryClaimUnsignedWarning())
        {
            Emit();
        }
    }

    /// <summary>
    /// Shows the warning once if the given request is predicted to use the blob feed.
    /// </summary>
    public static void WarnIfPredicted(DotnetInstallRequest request)
    {
        if (UnsignedSourcePolicy.MayDownloadUnsigned(request) && UnsignedSourcePolicy.TryClaimUnsignedWarning())
        {
            Emit();
        }
    }

    /// <summary>
    /// Shows the warning once if the downloader fell back to the blob feed and the warning was not
    /// already shown up-front. Call after an install/update completes.
    /// </summary>
    public static void WarnIfFallbackUsed()
    {
        if (UnsignedSourcePolicy.UnsignedFallbackUsed && UnsignedSourcePolicy.TryClaimUnsignedWarning())
        {
            Emit();
        }
    }

    private static void Emit()
        => SpectreAnsiConsole.MarkupLine(DotnetupTheme.Warning(Microsoft.Dotnet.Installation.Strings.UnsignedBlobFeedWarning.EscapeMarkup()));
}
