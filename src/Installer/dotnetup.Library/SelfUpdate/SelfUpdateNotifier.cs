// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Tells interactive users when a newer dotnetup is available without delaying their commands.
/// </summary>
/// <remarks>
/// A command starts a background refresh at most once per <see cref="RefreshInterval"/>. The refresh
/// reads the channel's version from its redirect target without downloading dotnetup. Timestamped
/// marker files throttle the best-effort check without coordinating concurrent processes.
/// </remarks>
internal sealed class SelfUpdateNotifier
{
    private const string MarkerExtension = ".dnupc";

    internal static TimeSpan RefreshInterval { get; } = TimeSpan.FromHours(24);

    private readonly ReleaseVersion _loadedVersion;
    private readonly string _channel;
    private readonly string _markerDirectory;
    private readonly Func<string, ReleaseVersion?> _resolveLatest;
    private readonly TimeProvider _timeProvider;

    /// <param name="loadedVersion">The version of the running dotnetup.</param>
    /// <param name="channel">The release channel whose latest version is checked.</param>
    /// <param name="markerDirectory">The directory containing update-check marker files.</param>
    /// <param name="resolveLatest">
    /// Returns the channel's latest version, or <c>null</c> when the channel has no build. Network
    /// failures should throw so the check is retried by the next command.
    /// </param>
    /// <param name="timeProvider">The clock used to decide whether the latest marker is fresh.</param>
    internal SelfUpdateNotifier(ReleaseVersion loadedVersion, string channel, string markerDirectory,
        Func<string, ReleaseVersion?> resolveLatest, TimeProvider timeProvider)
    {
        _loadedVersion = loadedVersion;
        _channel = channel;
        _markerDirectory = markerDirectory;
        _resolveLatest = resolveLatest;
        _timeProvider = timeProvider;
    }

    /// <summary>The background refresh started by this command, if the latest marker was stale.</summary>
    internal Task? RefreshTask { get; private set; }

    /// <summary>
    /// Returns a notifier for the running dotnetup, starting a background refresh when needed, or
    /// <c>null</c> when notifications do not apply to this invocation. Never throws: a best-effort
    /// notice must not fail the command.
    /// </summary>
    public static SelfUpdateNotifier? Start(bool interactive)
    {
        // Redirected output is read by a program, even when --interactive true allows prompts.
        if (!interactive ||
            Console.IsOutputRedirected ||
            SelfUpdateInvocation.Current is not { } invocation)
        {
            return null;
        }

        try
        {
            // Only suggest 'self update' where it can succeed: notifications are enabled, unsigned
            // downloads are allowed, and the executable has the canonical name in a supported location.
            if (!DotnetupConfig.ReadUpdateNotificationsEnabled() || UnsignedSourcePolicy.IsUnsignedDownloadBlocked())
            {
                return null;
            }

            invocation.Paths.ValidateLocation();

            var rid = DotnetupUtilities.GetRuntimeIdentifier(InstallerUtilities.GetDefaultInstallArchitecture());
            var notifier = new SelfUpdateNotifier(
                ReleaseVersion.Parse(invocation.LoadedVersion),
                SelfUpdateDefaultChannel.FromLoadedVersion(invocation.LoadedVersion),
                invocation.Paths.DirectoryPath,
                channel => ResolveLatestFromFeed(channel, rid),
                TimeProvider.System);
            notifier.StartRefreshIfStale();
            return notifier;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal void StartRefreshIfStale()
    {
        if (!WasCheckedRecently())
        {
            RefreshTask = Task.Run(Refresh);
        }
    }

    /// <summary>
    /// Returns the newer version on the running build's semantic channel, if any. A build on a
    /// different prerelease label (such as a local development build) is never reported.
    /// </summary>
    internal ReleaseVersion? GetAvailableUpdate(ReleaseVersion? latest)
    {
        if (latest is null)
        {
            return null;
        }

        return SelfUpdateWorkflow.HasSameSemanticChannel(_loadedVersion, latest) &&
            latest.ComparePrecedenceTo(_loadedVersion) > 0
                ? latest
                : null;
    }

    internal void Refresh()
    {
        try
        {
            Directory.CreateDirectory(_markerDirectory);
            if (WasCheckedRecently())
            {
                return;
            }

            var latest = _resolveLatest(_channel);
            var markerPath = TryCreateMarker();
            if (markerPath is null)
            {
                return;
            }

            DeleteOldMarkers(markerPath);
            if (GetAvailableUpdate(latest) is not null)
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Notice(Strings.SelfUpdateAvailableNotice.EscapeMarkup()));
            }
        }
        catch (Exception)
        {
            // Best effort: a failed check must never affect the command. The next command retries.
        }
    }

    private bool WasCheckedRecently()
    {
        long latestCheckSeconds = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(_markerDirectory, $"*{MarkerExtension}"))
            {
                if (TryGetMarkerSeconds(path, out var seconds))
                {
                    latestCheckSeconds = Math.Max(latestCheckSeconds, seconds);
                }
            }
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        long nowSeconds = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        // Markers intentionally omit channel and version. Missing a notice for up to a day after
        // either changes is acceptable for this best-effort check.
        return latestCheckSeconds <= nowSeconds &&
            nowSeconds - latestCheckSeconds < (long)RefreshInterval.TotalSeconds;
    }

    private string? TryCreateMarker()
    {
        var path = Path.Combine(
            _markerDirectory,
            $"{_timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}{MarkerExtension}");
        try
        {
            using var marker = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void DeleteOldMarkers(string currentMarkerPath)
    {
        if (!TryGetMarkerSeconds(currentMarkerPath, out var currentMarkerSeconds))
        {
            return;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(_markerDirectory, $"*{MarkerExtension}"))
            {
                if (!TryGetMarkerSeconds(path, out var markerSeconds) || markerSeconds >= currentMarkerSeconds)
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Cleanup is best effort.
                }
            }
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            // Cleanup is best effort.
        }
    }

    private static bool TryGetMarkerSeconds(string path, out long seconds)
    {
        var timestamp = Path.GetFileNameWithoutExtension(path);
        return long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out seconds) &&
            seconds > 0;
    }

    private static ReleaseVersion? ResolveLatestFromFeed(string channel, string rid)
    {
        try
        {
            using var resolver = new DailyChannelResolver(ReleaseManifest.Default, DefaultHttpClient.Instance);
            return resolver.ResolveDotnetupVersion(channel, rid);
        }
        catch (DotnetInstallException ex) when (ex.ErrorCode != DotnetInstallErrorCode.NetworkError)
        {
            // The channel has no usable build (for example, stable before its first release, or a
            // redirect that fails validation). Record that so it is not re-queried on every command;
            // network errors propagate so the next command retries.
            return null;
        }
    }
}
