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
/// marker files store the result and throttle the best-effort check without coordinating concurrent
/// processes.
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
            if (!DotnetupConfig.ReadUpdateNotificationsEnabled() || UnsignedSourcePolicy.IsUnsignedDownloadBlocked())
            {
                return null;
            }

            // Do not validate the executable location: renamed executables should still mention
            // updates even though self-update requires restoring the canonical name first.
            var rid = DotnetupUtilities.GetRuntimeIdentifier(InstallerUtilities.GetDefaultInstallArchitecture());
            var notifier = new SelfUpdateNotifier(
                ReleaseVersion.Parse(invocation.LoadedVersion),
                SelfUpdateDefaultChannel.FromLoadedVersion(invocation.LoadedVersion),
                DotnetupPaths.UpdateCheckDirectory,
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
    /// Writes the update notice when the latest marker records a newer build. Never throws, because
    /// the command it follows has already succeeded.
    /// </summary>
    public void ShowIfUpdateAvailable()
    {
        try
        {
            var latest = ReadLatestMarker();
            if (GetAvailableUpdate(latest.Version) is not null)
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Notice(Strings.SelfUpdateAvailableNotice.EscapeMarkup()));
                MarkUpdateAsShown(latest.Path, latest.UnixSeconds);
            }
        }
        catch (IOException)
        {
            // The terminal went away after the command finished; there is nothing left to report to.
        }
    }

    /// <summary>
    /// Returns the newer version from the latest marker when it is on the running build's semantic
    /// channel. A different prerelease label (such as a local development build) is never reported.
    /// </summary>
    internal ReleaseVersion? GetAvailableUpdate()
    {
        return GetAvailableUpdate(ReadLatestMarker().Version);
    }

    private ReleaseVersion? GetAvailableUpdate(ReleaseVersion? latest)
    {
        return latest is not null &&
            SelfUpdateWorkflow.HasSameSemanticChannel(_loadedVersion, latest) &&
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
            var markerPath = TryCreateMarker(latest);
            if (markerPath is null)
            {
                return;
            }

            DeleteOldMarkers(markerPath);
        }
        catch (Exception)
        {
            // Best effort: a failed check must never affect the command. The next command retries.
        }
    }

    private bool WasCheckedRecently()
    {
        long latestCheckSeconds = ReadLatestMarker().UnixSeconds;
        long nowSeconds = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        // Markers intentionally omit channel and installed version. Missing a notice for up to a day
        // after either changes is acceptable for this best-effort check.
        return latestCheckSeconds <= nowSeconds &&
            nowSeconds - latestCheckSeconds < (long)RefreshInterval.TotalSeconds;
    }

    private string? TryCreateMarker(ReleaseVersion? latest)
    {
        var versionSuffix = latest is null ? "" : $"_{latest}";
        var path = Path.Combine(
            _markerDirectory,
            $"{_timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}{versionSuffix}{MarkerExtension}");
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
        if (!TryParseMarker(currentMarkerPath, out var currentMarker))
        {
            return;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(_markerDirectory, $"*{MarkerExtension}"))
            {
                if (!TryParseMarker(path, out var marker) || marker.UnixSeconds >= currentMarker.UnixSeconds)
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

    private void MarkUpdateAsShown(string? markerPath, long unixSeconds)
    {
        if (markerPath is null)
        {
            return;
        }

        var shownMarkerPath = Path.Combine(
            _markerDirectory,
            $"{unixSeconds.ToString(CultureInfo.InvariantCulture)}{MarkerExtension}");
        try
        {
            File.Move(markerPath, shownMarkerPath);
        }
        catch (IOException) when (File.Exists(shownMarkerPath))
        {
            try
            {
                File.Delete(markerPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: failure can only cause another notice before the next refresh.
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort: failure can only cause another notice before the next refresh.
        }
    }

    private (string? Path, long UnixSeconds, ReleaseVersion? Version) ReadLatestMarker()
    {
        (string? Path, long UnixSeconds, ReleaseVersion? Version) latest = default;
        try
        {
            foreach (var path in Directory.EnumerateFiles(_markerDirectory, $"*{MarkerExtension}"))
            {
                if (TryParseMarker(path, out var marker) &&
                    IsNewerMarker(marker, (latest.UnixSeconds, latest.Version)))
                {
                    latest = (path, marker.UnixSeconds, marker.Version);
                }
            }
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            // A missing or unreadable marker directory has no usable cached result.
        }

        return latest;
    }

    private static bool IsNewerMarker(
        (long UnixSeconds, ReleaseVersion? Version) candidate,
        (long UnixSeconds, ReleaseVersion? Version) current)
    {
        if (candidate.UnixSeconds != current.UnixSeconds)
        {
            return candidate.UnixSeconds > current.UnixSeconds;
        }

        if (candidate.Version is null)
        {
            return false;
        }

        return current.Version is null ||
            candidate.Version.ComparePrecedenceTo(current.Version) > 0;
    }

    private static bool TryParseMarker(
        string path,
        out (long UnixSeconds, ReleaseVersion? Version) marker)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var separator = name.IndexOf('_');
        var timestamp = separator < 0 ? name : name[..separator];
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds <= 0)
        {
            marker = default;
            return false;
        }

        ReleaseVersion? version = null;
        if (separator >= 0 && !ReleaseVersion.TryParse(name[(separator + 1)..], out version))
        {
            marker = default;
            return false;
        }

        marker = (seconds, version);
        return true;
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
