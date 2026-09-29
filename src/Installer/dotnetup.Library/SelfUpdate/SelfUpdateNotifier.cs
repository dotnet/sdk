// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Tells interactive users when a newer dotnetup is available without delaying their commands.
/// </summary>
/// <remarks>
/// A command starts a background refresh at most once per <see cref="RefreshInterval"/>. The refresh
/// reads the channel's version from its redirect target, without downloading dotnetup, and atomically
/// replaces a small cache file. The end-of-command check reads only that cache, so it never waits on
/// the network; a refresh cut short by process exit is retried by a later command. No self-update
/// lock is needed because the check never touches the executable.
/// </remarks>
internal sealed class SelfUpdateNotifier
{
    internal static TimeSpan RefreshInterval { get; } = TimeSpan.FromHours(24);

    private readonly ReleaseVersion _loadedVersion;
    private readonly string _channel;
    private readonly string _statePath;
    private readonly Func<string, ReleaseVersion?> _resolveLatest;
    private readonly TimeProvider _timeProvider;

    /// <param name="loadedVersion">The version of the running dotnetup.</param>
    /// <param name="channel">The release channel whose latest version is checked.</param>
    /// <param name="statePath">The cache file holding the latest check result.</param>
    /// <param name="resolveLatest">
    /// Returns the channel's latest version, or <c>null</c> when the channel has no build. Network
    /// failures should throw so the check is retried by the next command.
    /// </param>
    /// <param name="timeProvider">The clock used to decide whether the cache is fresh.</param>
    internal SelfUpdateNotifier(ReleaseVersion loadedVersion, string channel, string statePath,
        Func<string, ReleaseVersion?> resolveLatest, TimeProvider timeProvider)
    {
        _loadedVersion = loadedVersion;
        _channel = channel;
        _statePath = statePath;
        _resolveLatest = resolveLatest;
        _timeProvider = timeProvider;
    }

    /// <summary>The background refresh started by this command, if the cache was stale.</summary>
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
            SelfUpdateInvocation.Current is not { } invocation ||
            !ReleaseVersion.TryParse(invocation.LoadedVersion, out var loadedVersion))
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
                loadedVersion,
                SelfUpdateDefaultChannel.FromLoadedVersion(invocation.LoadedVersion),
                DotnetupPaths.UpdateCheckPath,
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
        if (!IsFresh(ReadState()))
        {
            RefreshTask = Task.Run(Refresh);
        }
    }

    /// <summary>
    /// Writes the gold update notice when the cached check found a newer build. Never throws, because
    /// the command it follows has already succeeded.
    /// </summary>
    public void ShowIfUpdateAvailable()
    {
        try
        {
            if (GetAvailableUpdate() is not null)
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Notice(Strings.SelfUpdateAvailableNotice.EscapeMarkup()));
            }
        }
        catch (IOException)
        {
            // The terminal went away after the command finished; there is nothing left to report to.
        }
    }

    /// <summary>
    /// Returns the cached newer version on the running build's semantic channel, if any. A build on a
    /// different prerelease label (such as a local development build) is never told to update.
    /// </summary>
    internal ReleaseVersion? GetAvailableUpdate()
    {
        var state = ReadState();
        if (state is null ||
            !string.Equals(state.Channel, _channel, StringComparison.Ordinal) ||
            state.LatestVersion is null ||
            !ReleaseVersion.TryParse(state.LatestVersion, out var latest))
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
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);

            // Another dotnetup already refreshing holds this exclusively; skip rather than wait. The OS
            // releases the handle, and deletes the file, as soon as this process exits.
            using var refreshLock = TryAcquireRefreshLock();
            if (refreshLock is null || IsFresh(ReadState()))
            {
                return;
            }

            var latest = _resolveLatest(_channel);
            WriteState(new UpdateCheckState
            {
                Channel = _channel,
                LatestVersion = latest?.ToString(),
                CheckedUtc = _timeProvider.GetUtcNow(),
            });
        }
        catch (Exception)
        {
            // Best effort: a failed check must never affect the command. The next command retries.
        }
    }

    private bool IsFresh(UpdateCheckState? state)
    {
        if (state is null || !string.Equals(state.Channel, _channel, StringComparison.Ordinal))
        {
            return false;
        }

        // A future timestamp (for example, after the clock moved back) is treated as stale.
        var age = _timeProvider.GetUtcNow() - state.CheckedUtc;
        return age >= TimeSpan.Zero && age < RefreshInterval;
    }

    private UpdateCheckState? ReadState()
    {
        try
        {
            return File.Exists(_statePath)
                ? JsonSerializer.Deserialize(File.ReadAllText(_statePath), UpdateCheckJsonContext.Default.UpdateCheckState)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void WriteState(UpdateCheckState state)
    {
        // Replace atomically so a concurrent reader never observes a partial file. A unique temporary
        // name keeps writers apart even if two refreshes race past the advisory lock.
        var tempPath = $"{_statePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, UpdateCheckJsonContext.Default.UpdateCheckState));
            File.Move(tempPath, _statePath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private FileStream? TryAcquireRefreshLock()
    {
        try
        {
            return new FileStream(_statePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 1, FileOptions.DeleteOnClose);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
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

/// <summary>The cached result of the most recent dotnetup update check.</summary>
internal sealed class UpdateCheckState
{
    public string? Channel { get; set; }

    /// <summary>The channel's latest version, or <c>null</c> when the channel had no build.</summary>
    public string? LatestVersion { get; set; }

    public DateTimeOffset CheckedUtc { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UpdateCheckState))]
internal partial class UpdateCheckJsonContext : JsonSerializerContext { }
