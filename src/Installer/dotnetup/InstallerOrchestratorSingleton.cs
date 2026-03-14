// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Result of an installation operation.
/// </summary>
/// <param name="Install">The DotnetInstall for the completed installation.</param>
/// <param name="WasAlreadyInstalled">True if the SDK was already installed and no work was done.</param>
internal sealed record InstallResult(DotnetInstall Install, bool WasAlreadyInstalled);

internal class InstallerOrchestratorSingleton
{
    public static InstallerOrchestratorSingleton Instance { get; } = new();

    private InstallerOrchestratorSingleton()
    {
    }

    private static ScopedMutex ModifyInstallStateMutex() => new(Constants.MutexNames.ModifyInstallationStates);

    /// <summary>
    /// Resolves the version for a channel and downloads the archive into the download cache
    /// without installing. This allows a background task to warm the cache while the user
    /// interacts with walkthrough prompts, so the subsequent <see cref="Install"/> call
    /// finds the archive already cached and skips the download.
    /// </summary>
    /// <remarks>
    /// This method is safe to call concurrently with <see cref="Install"/>. The download
    /// cache handles deduplication, and no install-state mutex is acquired.
    /// Exceptions are intentionally swallowed — a failed predownload simply means the
    /// real install will download normally.
    /// </remarks>
    public static async Task PredownloadToCacheAsync(string channel, InstallComponent component, DotnetInstallRoot installRoot)
    {
        try
        {
            if (!ChannelVersionResolver.IsValidChannelFormat(channel))
            {
                return;
            }

            ReleaseManifest releaseManifest = new();
            var resolver = new ChannelVersionResolver(releaseManifest);
            var request = new DotnetInstallRequest(installRoot, new UpdateChannel(channel), component, new InstallRequestOptions());
            var version = resolver.Resolve(request);

            if (version is null)
            {
                return;
            }

            // Download to a temp file, which populates the download cache as a side effect.
            // The temp file is cleaned up afterwards — only the cache entry matters.
            var tempDir = Directory.CreateTempSubdirectory("dotnetup-predownload").FullName;
            try
            {
                var archivePath = Path.Combine(tempDir, $"predownload{DotnetupUtilities.GetArchiveFileExtensionForPlatform()}");
                using var downloader = new DotnetArchiveDownloader(releaseManifest);
                await Task.Run(() => downloader.DownloadArchiveWithVerification(request, version, archivePath)).ConfigureAwait(false);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
            }
        }
        catch
        {
            // Predownload is best-effort — failures are silently ignored.
            // The real install will download normally.
        }
    }

    // Throws DotnetInstallException on failure, returns InstallResult on success
#pragma warning disable CA1822 // Intentionally an instance method on a singleton
    public InstallResult Install(DotnetInstallRequest installRequest, bool noProgress = false)
    {
        IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();
        using var reporter = progressTarget.CreateProgressReporter();
        using var prepared = PrepareInstall(installRequest, reporter, out var alreadyInstalledResult);

        if (alreadyInstalledResult is not null)
        {
            return alreadyInstalledResult;
        }

        return CommitPreparedInstall(prepared!);
    }
#pragma warning restore CA1822

    /// <summary>
    /// Validates the channel format, resolves the version, and constructs the install object.
    /// Throws <see cref="DotnetInstallException"/> if the channel is invalid or the version cannot be found.
    /// </summary>
    private static (ReleaseManifest Manifest, ReleaseVersion Version, DotnetInstall Install) ResolveInstall(DotnetInstallRequest request)
    {
        if (!ChannelVersionResolver.IsValidChannelFormat(request.Channel.Name))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InvalidChannel,
                $"'{request.Channel.Name}' is not a valid .NET version or channel. " +
                $"Use a version like '9.0', '9.0.100', or a channel keyword: {string.Join(", ", ChannelVersionResolver.KnownChannelKeywords)}.",
                version: null,
                component: request.Component.ToString());
        }

        ReleaseManifest manifest = new();
        ReleaseVersion? version = request.ResolvedVersion ?? new ChannelVersionResolver(manifest).Resolve(request);

        if (version is null)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.VersionNotFound,
                $"Could not find .NET version '{request.Channel.Name}'. The version may not exist or may not be supported.",
                version: null,
                component: request.Component.ToString());
        }

        return (manifest, version, new DotnetInstall(request.InstallRoot, version, request.Component));
    }

    /// <summary>
    /// Prepares and commits all install requests concurrently where possible: downloads run in parallel,
    /// and commits serialize through the install-state mutex.
    /// </summary>
    /// <returns>All results, including already-installed entries.</returns>
#pragma warning disable CA1822
    public IReadOnlyList<InstallResult> InstallMany(IReadOnlyList<DotnetInstallRequest> requests, IProgressReporter sharedReporter)
    {
        var preparedInstalls = new List<PreparedInstall>();
        var results = new List<InstallResult>();
        var exceptions = new List<Exception>();
        var lockObj = new object();

        // Phase 1: download all archives concurrently
        var tasks = requests.Select(request => Task.Run(() =>
        {
            try
            {
                var prepared = PrepareInstall(request, sharedReporter, out var existingResult);
                lock (lockObj)
                {
                    if (prepared is not null)
                    {
                        preparedInstalls.Add(prepared);
                    }
                    else if (existingResult is not null)
                    {
                        results.Add(existingResult);
                    }
                }
            }
            catch (Exception ex)
            {
                lock (lockObj) { exceptions.Add(ex); }
            }
        })).ToList();

        Task.WaitAll([.. tasks]);

        if (exceptions.Count > 0)
        {
            foreach (var p in preparedInstalls) { p.Dispose(); }
            if (exceptions[0] is DotnetInstallException) { throw exceptions[0]; }
            throw new AggregateException(exceptions);
        }

        // Phase 2: commit each prepared install; the install-state mutex serializes them
        try
        {
            foreach (var prepared in preparedInstalls)
            {
                results.Add(CommitPreparedInstall(prepared));
            }
        }
        finally
        {
            foreach (var p in preparedInstalls) { p.Dispose(); }
        }

        return results;
    }
#pragma warning restore CA1822

    /// <summary>
    /// Represents a prepared (downloaded but not yet committed) installation.
    /// </summary>
    internal sealed class PreparedInstall : IDisposable
    {
        public DotnetInstallRequest Request { get; }
        public ReleaseVersion Version { get; }
        public DotnetInstall Install { get; }
        public DotnetArchiveExtractor Extractor { get; }
        public ReleaseManifest ReleaseManifest { get; }
        public bool WasAlreadyInstalled { get; init; }

        public PreparedInstall(DotnetInstallRequest request, ReleaseVersion version, DotnetInstall install,
            DotnetArchiveExtractor extractor, ReleaseManifest releaseManifest)
        {
            Request = request;
            Version = version;
            Install = install;
            Extractor = extractor;
            ReleaseManifest = releaseManifest;
        }

        public void Dispose() => Extractor.Dispose();
    }

    /// <summary>
    /// Validates and resolves version for an install request, checks if already installed,
    /// and downloads the archive — but does not commit (extract) it.
    /// Returns null if the install was already present (the result is returned via the out parameter).
    /// Used for concurrent multi-install scenarios where downloads happen in parallel.
    /// </summary>
    /// <param name="installRequest">The installation request.</param>
    /// <param name="sharedReporter">A shared progress reporter for displaying download progress.</param>
    /// <param name="alreadyInstalledResult">Set when the install is already present; null otherwise.</param>
    /// <returns>A PreparedInstall that can be committed later, or null if already installed.</returns>
#pragma warning disable CA1822
    public PreparedInstall? PrepareInstall(
        DotnetInstallRequest installRequest,
        IProgressReporter sharedReporter,
        out InstallResult? alreadyInstalledResult)
    {
        alreadyInstalledResult = null;

        var (releaseManifest, versionToInstall, install) = ResolveInstall(installRequest);
        string? customManifestPath = installRequest.Options.ManifestPath;

        using (var finalizeLock = ModifyInstallStateMutex())
        {
            var manifestData = installRequest.Options.Untracked
                ? new DotnetupManifestData()
                : new DotnetupSharedManifest(customManifestPath).ReadManifest();

            if (InstallAlreadyExists(manifestData, install))
            {
                RecordInstallSpec(installRequest, customManifestPath);
                alreadyInstalledResult = new InstallResult(install, WasAlreadyInstalled: true);
                return null;
            }

            if (!installRequest.Options.Untracked
                && !IsRootInManifest(manifestData, installRequest.InstallRoot)
                && HasDotnetArtifacts(installRequest.InstallRoot.Path))
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.Unknown,
                    $"The install path '{installRequest.InstallRoot.Path}' already contains a .NET installation that is not tracked by dotnetup. " +
                    "To avoid conflicts, use a different install path or remove the existing installation first.",
                    version: versionToInstall.ToString(),
                    component: installRequest.Component.ToString());
            }

            if (installRequest.Options.RequireMuxerUpdate && installRequest.InstallRoot.Path is not null)
            {
                MuxerHandler.EnsureMuxerIsWritable(installRequest.InstallRoot.Path);
            }
        }

        DotnetArchiveExtractor extractor = new(installRequest, versionToInstall, releaseManifest, sharedReporter);
        extractor.Prepare();

        return new PreparedInstall(installRequest, versionToInstall, install, extractor, releaseManifest);
    }
#pragma warning restore CA1822

    /// <summary>
    /// Commits a previously prepared installation: extracts the archive to the target directory
    /// and records it in the manifest. Must be called sequentially (not concurrently).
    /// </summary>
    /// <returns>The installation result.</returns>
#pragma warning disable CA1822
    public InstallResult CommitPreparedInstall(PreparedInstall prepared)
    {
        string? customManifestPath = prepared.Request.Options.ManifestPath;

        using (var finalizeLock = ModifyInstallStateMutex())
        {
            var manifestData = prepared.Request.Options.Untracked
                ? new DotnetupManifestData()
                : new DotnetupSharedManifest(customManifestPath).ReadManifest();

            if (InstallAlreadyExists(manifestData, prepared.Install))
            {
                return new InstallResult(prepared.Install, WasAlreadyInstalled: true);
            }

            prepared.Extractor.Commit();

            ArchiveInstallationValidator validator = new();
            if (validator.Validate(prepared.Install, out string? validationFailure))
            {
                RecordInstallSpec(prepared.Request, customManifestPath);

                if (!prepared.Request.Options.Untracked)
                {
                    var manifestManager = new DotnetupSharedManifest(customManifestPath);
                    manifestManager.AddInstallation(prepared.Request.InstallRoot, new Installation
                    {
                        Component = prepared.Request.Component,
                        Version = prepared.Version.ToString(),
                        Subcomponents = [.. prepared.Extractor.ExtractedSubcomponents]
                    });
                }
            }
            else
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.InstallFailed,
                    $"Installation validation failed: {validationFailure}",
                    version: prepared.Version.ToString(),
                    component: prepared.Request.Component.ToString());
            }
        }

        return new InstallResult(prepared.Install, WasAlreadyInstalled: false);
    }
#pragma warning restore CA1822

    /// <summary>
    /// Records the install spec in the manifest, respecting Untracked and SkipInstallSpecRecording flags.
    /// </summary>
    private static void RecordInstallSpec(DotnetInstallRequest installRequest, string? customManifestPath)
    {
        if (installRequest.Options.Untracked || installRequest.Options.SkipInstallSpecRecording)
        {
            return;
        }

        var manifestManager = new DotnetupSharedManifest(customManifestPath);
        manifestManager.AddInstallSpec(installRequest.InstallRoot, new InstallSpec
        {
            Component = installRequest.Component,
            VersionOrChannel = installRequest.Channel.Name,
            InstallSource = installRequest.Options.InstallSource switch
            {
                InstallRequestSource.GlobalJson => InstallSource.GlobalJson,
                _ => InstallSource.Explicit,
            },
            GlobalJsonPath = installRequest.Options.GlobalJsonPath
        });
    }

    internal static bool InstallAlreadyExists(DotnetupManifestData manifestData, DotnetInstall install)
    {
        var root = manifestData.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(r.Path, install.InstallRoot.Path!));
        return root?.Installations.Any(existing =>
            existing.Version == install.Version.ToString() &&
            existing.Component == install.Component) ?? false;
    }

    internal static bool IsRootInManifest(DotnetupManifestData manifestData, DotnetInstallRoot installRoot)
    {
        return manifestData.DotnetRoots.Any(root =>
            DotnetupUtilities.PathsEqual(root.Path, installRoot.Path));
    }

    internal static bool HasDotnetArtifacts(string? path)
    {
        if (path is null || !Directory.Exists(path))
        {
            return false;
        }

        // Check for common .NET installation markers
        return File.Exists(Path.Combine(path, DotnetupUtilities.GetDotnetExeName()))
            || Directory.Exists(Path.Combine(path, "sdk"))
            || Directory.Exists(Path.Combine(path, "shared"));
    }
}
