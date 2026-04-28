// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class InstallerOrchestratorSingleton
{
    public static InstallerOrchestratorSingleton Instance { get; } = new();

    private InstallerOrchestratorSingleton()
    {
    }

    private static ScopedMutex ModifyInstallStateMutex() => new(Constants.MutexNames.ModifyInstallationStates);

    /// <summary>
    /// Downloads the archive for an already-resolved install request into the download cache
    /// without installing. This allows a background task to warm the cache while the user
    /// interacts with init prompts, so the subsequent <see cref="Install"/> call
    /// finds the archive already cached and skips the download.
    /// </summary>
    /// <remarks>
    /// This method is safe to call concurrently with <see cref="Install"/>. The download
    /// cache handles deduplication, and no install-state mutex is acquired.
    /// Exceptions are intentionally swallowed — a failed predownload simply means the
    /// real install will download normally.
    /// </remarks>
    public static async Task PredownloadToCacheAsync(ResolvedInstallRequest resolvedRequest)
    {
        try
        {
            await Task.Run(() =>
            {
                // Reuse PrepareInstall which checks if already installed and populates the download cache.
                // The PreparedInstall is disposed immediately — only the cache side effect matters.
                // Use NullProgressTarget to avoid any console output from the background predownload.
                using var reporter = new LazyProgressReporter(new NullProgressTarget());
                using var prepared = Instance.PrepareInstall(resolvedRequest, reporter, out _);
            }).ConfigureAwait(false);
        }
        catch
        {
            // Predownload is best-effort — failures are silently ignored.
            // The real install will download normally.
        }
    }

    // Throws DotnetInstallException on failure, returns InstallResult on success
#pragma warning disable CA1822 // Intentionally an instance method on a singleton
    public InstallResult Install(ResolvedInstallRequest resolvedRequest, bool noProgress = false)
    {
        IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();
        using var reporter = new LazyProgressReporter(progressTarget);
        using var prepared = PrepareInstall(resolvedRequest, reporter, out var alreadyInstalledResult);

        if (alreadyInstalledResult is not null)
        {
            return alreadyInstalledResult;
        }

        return CommitPreparedInstall(prepared!);
    }
#pragma warning restore CA1822

    /// <summary>
    /// Prepares and commits all install requests concurrently where possible: downloads run in parallel,
    /// and commits serialize through the install-state mutex.
    /// Individual <see cref="DotnetInstallException"/> failures are captured and returned
    /// so that other installs in the batch can continue.
    /// </summary>
    /// <returns>A batch result containing both successes and per-request failures.</returns>
    public InstallBatchResult InstallMany(IReadOnlyList<ResolvedInstallRequest> requests, IProgressReporter sharedReporter)
    {

        var results = new List<InstallResult>();
        var failures = new List<InstallFailure>();
        var fatalExceptions = new List<Exception>();
        var installResultCollectionLock = new object();

        // Use a BlockingCollection so commits can start as soon as downloads finish,
        // overlapping extraction/commit with still-running downloads.
        using var readyQueue = new System.Collections.Concurrent.BlockingCollection<PreparedInstall>();

        var downloadTask = Task.Run(() => PrepareConcurrent(requests, sharedReporter, readyQueue, results, failures, fatalExceptions, installResultCollectionLock));
        ConsumeConcurrentPreparedInstallsAndCommit(readyQueue, results, failures, fatalExceptions, installResultCollectionLock);
        downloadTask.Wait();

        // Fatal (non-DotnetInstallException) errors still abort the batch entirely.
        if (fatalExceptions.Count > 0)
        {
            throw new AggregateException(fatalExceptions);
        }

        return new InstallBatchResult(results, failures);
    }

    /// <summary>
    /// Downloads archives concurrently (max 3) and enqueues PreparedInstalls for commit.
    /// <see cref="DotnetInstallException"/> failures are captured per-request; other exceptions are fatal.
    /// </summary>
    private void PrepareConcurrent(
        IReadOnlyList<ResolvedInstallRequest> requests,
        IProgressReporter sharedReporter,
        System.Collections.Concurrent.BlockingCollection<PreparedInstall> readyQueue,
        List<InstallResult> results,
        List<InstallFailure> failures,
        List<Exception> fatalExceptions,
        object installResultCollectionLock)
    {
        const int maxConcurrentDownloads = 3;

        try
        {
            Parallel.ForEach(requests, new ParallelOptions { MaxDegreeOfParallelism = maxConcurrentDownloads }, request =>
            {
                try
                {
                    var prepared = PrepareInstall(request, sharedReporter, out var existingResult);
                    lock (installResultCollectionLock)
                    {
                        if (prepared is not null)
                        {
                            readyQueue.Add(prepared);
                        }
                        else if (existingResult is not null)
                        {
                            results.Add(existingResult);
                        }
                    }
                }
                catch (DotnetInstallException ex)
                {
                    lock (installResultCollectionLock) { failures.Add(new InstallFailure(request, ex)); }
                }
                catch (Exception ex)
                {
                    lock (installResultCollectionLock) { fatalExceptions.Add(ex); }
                }
            });
        }
        finally
        {
            readyQueue.CompleteAdding();
        }
    }

    /// <summary>
    /// Consumes the ready queue and commits (extracts + records) each install as it arrives.
    /// CommitPreparedInstall acquires the install-state mutex, so commits are serialized.
    /// <see cref="DotnetInstallException"/> failures are captured per-request; other exceptions are fatal.
    /// </summary>
    private void ConsumeConcurrentPreparedInstallsAndCommit(
        System.Collections.Concurrent.BlockingCollection<PreparedInstall> readyQueue,
        List<InstallResult> results,
        List<InstallFailure> failures,
        List<Exception> fatalExceptions,
        object installResultCollectionLock)
    {
        var committedInstalls = new List<PreparedInstall>();
        try
        {
            foreach (var prepared in readyQueue.GetConsumingEnumerable())
            {
                committedInstalls.Add(prepared);

                // Skip committing if a fatal error has already occurred on another thread.
                // Read under the lock because the download threads write fatalExceptions under the same lock.
                bool facedFatalError;
                lock (installResultCollectionLock) { facedFatalError = fatalExceptions.Count > 0; }
                if (facedFatalError)
                {
                    continue;
                }

                try
                {
                    results.Add(CommitPreparedInstall(prepared));
                }
                catch (DotnetInstallException ex)
                {
                    lock (installResultCollectionLock) { failures.Add(new InstallFailure(prepared.ResolvedRequest, ex)); }
                }
                catch (Exception ex)
                {
                    lock (installResultCollectionLock) { fatalExceptions.Add(ex); }
                }
            }
        }
        finally
        {
            foreach (var p in committedInstalls) { p.Dispose(); }
        }
    }

    /// <summary>
    /// Represents a prepared (downloaded but not yet committed) installation.
    /// </summary>
    internal sealed class PreparedInstall : IDisposable
    {
        public ResolvedInstallRequest ResolvedRequest { get; }
        public DotnetInstallRequest Request => ResolvedRequest.Request;
        public ReleaseVersion Version => ResolvedRequest.ResolvedVersion;
        public DotnetInstall Install { get; }
        public DotnetArchiveExtractor Extractor { get; }

        public PreparedInstall(ResolvedInstallRequest resolvedRequest, DotnetInstall install,
            DotnetArchiveExtractor extractor)
        {
            ResolvedRequest = resolvedRequest;
            Install = install;
            Extractor = extractor;
        }

        public void Dispose() => Extractor.Dispose();
    }

    /// <summary>
    /// Validates and resolves version for an install request, checks if already installed,
    /// and downloads the archive — but does not commit (extract) it.
    /// Returns null if the install was already present (the result is returned via the out parameter).
    /// Used for concurrent multi-install scenarios where downloads happen in parallel.
    /// </summary>
    /// <param name="resolvedRequest">The resolved installation request with a concrete version.</param>
    /// <param name="sharedReporter">A shared progress reporter for displaying download progress.</param>
    /// <param name="alreadyInstalledResult">Set when the install is already present; null otherwise.</param>
    /// <returns>A PreparedInstall that can be committed later, or null if already installed.</returns>
#pragma warning disable CA1822
    public PreparedInstall? PrepareInstall(
        ResolvedInstallRequest resolvedRequest,
        IProgressReporter sharedReporter,
        out InstallResult? alreadyInstalledResult)
    {
        alreadyInstalledResult = null;
        var installRequest = resolvedRequest.Request;
        var versionToInstall = resolvedRequest.ResolvedVersion;
        var install = new DotnetInstall(installRequest.InstallRoot, versionToInstall, installRequest.Component);
        ReleaseManifest releaseManifest = new();
        var manifest = installRequest.Options.Untracked ? null : new DotnetupSharedManifest(installRequest.Options.ManifestPath);

        using (var finalizeLock = ModifyInstallStateMutex())
        {
            if (manifest?.InstallAlreadyExists(install) == true)
            {
                manifest.RecordInstallSpec(installRequest);
                alreadyInstalledResult = new InstallResult(install, WasAlreadyInstalled: true);
                return null;
            }

            if (!installRequest.Options.Untracked
                && !manifest!.IsRootTracked(installRequest.InstallRoot)
                && DotnetupSharedManifest.HasDotnetArtifacts(installRequest.InstallRoot.Path))
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

        DotnetArchiveExtractor extractor = new(installRequest, versionToInstall, releaseManifest, sharedReporter, cacheDirectory: DotnetupPaths.DownloadCacheDirectory);
        extractor.Prepare();

        return new PreparedInstall(resolvedRequest, install, extractor);
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
        var manifest = prepared.Request.Options.Untracked ? null : new DotnetupSharedManifest(prepared.Request.Options.ManifestPath);

        using (var finalizeLock = ModifyInstallStateMutex())
        {
            if (manifest?.InstallAlreadyExists(prepared.Install) == true)
            {
                return new InstallResult(prepared.Install, WasAlreadyInstalled: true);
            }

            prepared.Extractor.Commit();

            ArchiveInstallationValidator validator = new();
            if (validator.Validate(prepared.Install, out string? validationFailure))
            {
                manifest?.RecordInstallSpec(prepared.Request);

                manifest?.AddInstallation(prepared.Request.InstallRoot, new Installation
                {
                    Component = prepared.Request.Component,
                    Version = prepared.Version.ToString(),
                    Subcomponents = [.. prepared.Extractor.ExtractedSubcomponents],
                });
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
}
