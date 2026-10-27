// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;

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
                using var prepared = Instance.PrepareInstall(resolvedRequest, new InstallBatchContext(reporter, resolvedRequest.ResolvedVersion.ToString().Length), out _);
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
        using var prepared = PrepareInstall(resolvedRequest, new InstallBatchContext(reporter, resolvedRequest.ResolvedVersion.ToString().Length), out var alreadyInstalledResult);

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
        // Compute a shared version-display width so all rows in this batch align even
        // when versions differ in length (e.g. stable "10.0.201" + daily "11.0.100-preview.3.26207.106").
        int versionDisplayWidth = requests.Count == 0
            ? 0
            : requests.Max(r => r.ResolvedVersion.ToString().Length);
        var batchContext = new InstallBatchContext(sharedReporter, versionDisplayWidth);

        var results = new List<InstallResult>();
        var failures = new List<InstallFailure>();
        var fatalExceptions = new List<Exception>();
        var installResultCollectionLock = new object();

        // Use a BlockingCollection so commits can start as soon as downloads finish,
        // overlapping extraction/commit with still-running downloads.
        using var readyQueue = new System.Collections.Concurrent.BlockingCollection<PreparedInstall>();

        var downloadTask = Task.Run(() => PrepareConcurrent(requests, batchContext, readyQueue, results, failures, fatalExceptions, installResultCollectionLock));
        ConsumeConcurrentPreparedInstallsAndCommit(readyQueue, batchContext, prepared => CommitPreparedInstall(prepared), results, failures, fatalExceptions, installResultCollectionLock);
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
        InstallBatchContext batchContext,
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
                    var prepared = PrepareInstall(request, batchContext, out var existingResult);
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
    /// Once the user has asked to cancel the batch, remaining installs are recorded as cancelled instead of committed.
    /// <c>commit</c> commits one prepared install; production code passes <see cref="CommitPreparedInstall"/>.
    /// </summary>
    internal static void ConsumeConcurrentPreparedInstallsAndCommit(
        System.Collections.Concurrent.BlockingCollection<PreparedInstall> readyQueue,
        InstallBatchContext batchContext,
        Func<PreparedInstall, InstallResult> commit,
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

                if (batchContext.IsCancellationRequested)
                {
                    lock (installResultCollectionLock) { failures.Add(new InstallFailure(prepared.ResolvedRequest, CreateCancelledBeforeStartException(prepared))); }
                    continue;
                }

                try
                {
                    results.Add(commit(prepared));
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

    private static DotnetInstallException CreateCancelledBeforeStartException(PreparedInstall prepared) => new(
        DotnetInstallErrorCode.OperationCancelled,
        string.Format(CultureInfo.CurrentCulture, Strings.InstallCancelledBeforeStart, prepared.Request.Component.GetDisplayName(), prepared.Version),
        version: prepared.Version.ToString(),
        component: prepared.Request.Component.ToString());

    /// <summary>
    /// Represents a prepared (downloaded but not yet committed) installation.
    /// </summary>
    internal sealed class PreparedInstall : IDisposable
    {
        public ResolvedInstallRequest ResolvedRequest { get; }
        public DotnetInstallRequest Request => ResolvedRequest.Request;
        public ReleaseVersion Version => ResolvedRequest.ResolvedVersion;
        public DotnetInstall Install { get; }
        public IInstallArtifact Artifact { get; }

        public PreparedInstall(ResolvedInstallRequest resolvedRequest, DotnetInstall install,
            IInstallArtifact artifact)
        {
            ResolvedRequest = resolvedRequest;
            Install = install;
            Artifact = artifact;
        }

        public void Dispose() => Artifact.Dispose();
    }

    /// <summary>
    /// Validates and resolves version for an install request, checks if already installed,
    /// and downloads the install artifact — but does not commit (install) it.
    /// Returns null if the install was already present (the result is returned via the out parameter).
    /// Used for concurrent multi-install scenarios where downloads happen in parallel.
    /// </summary>
    /// <param name="resolvedRequest">The resolved installation request with a concrete version.</param>
    /// <param name="batchContext">Shared per-batch progress reporter and version-column width. For single-install scenarios, pass a context whose width matches the request's version length.</param>
    /// <param name="alreadyInstalledResult">Set when the install is already present; null otherwise.</param>
    /// <param name="installationValidator">Optional validator override used to determine whether an untracked installation already exists on disk.</param>
    /// <returns>A PreparedInstall that can be committed later, or null if already installed.</returns>
#pragma warning disable CA1822
    public PreparedInstall? PrepareInstall(
        ResolvedInstallRequest resolvedRequest,
        InstallBatchContext batchContext,
        out InstallResult? alreadyInstalledResult,
        IInstallationValidator? installationValidator = null)
    {
        var installRequest = resolvedRequest.Request;
        var install = new DotnetInstall(installRequest.InstallRoot, resolvedRequest.ResolvedVersion, installRequest.Component);
        installationValidator ??= CreateInstallationValidator(installRequest.Options.InstallMechanism);

        alreadyInstalledResult = CheckExistingInstallState(installRequest, install, installationValidator);
        if (alreadyInstalledResult is not null)
        {
            return null;
        }

        IInstallArtifact artifact = CreateInstallArtifact(resolvedRequest, ReleaseManifest.Default, batchContext);
        try
        {
            artifact.Prepare();
        }
        catch
        {
            artifact.Dispose();
            throw;
        }

        return new PreparedInstall(resolvedRequest, install, artifact);
    }
#pragma warning restore CA1822

    /// <summary>
    /// Under the install-state mutex, returns an already-installed result when the install exists,
    /// otherwise validates that the install root can be installed into and returns null.
    /// </summary>
    private static InstallResult? CheckExistingInstallState(
        DotnetInstallRequest installRequest,
        DotnetInstall install,
        IInstallationValidator installationValidator)
    {
        var manifest = installRequest.Options.Untracked ? null : new DotnetupSharedManifest(installRequest.Options.ManifestPath);

        using var finalizeLock = ModifyInstallStateMutex();

        if (manifest?.InstallAlreadyExists(install) == true)
        {
            manifest.RecordInstallSpec(installRequest);
            return new InstallResult(install, WasAlreadyInstalled: true);
        }

        // For --untracked installs the manifest is null, so the check above is
        // skipped. Fall back to a filesystem check using the same validation
        // that CommitPreparedInstall uses post-install. This runs inside the
        // mutex so another dotnetup process can't be mid-extraction when we probe.
        if (installRequest.Options.Untracked && installationValidator.Validate(install))
        {
            return new InstallResult(install, WasAlreadyInstalled: true);
        }

        if (!installRequest.Options.Untracked
            && !manifest!.IsRootTracked(installRequest.InstallRoot)
            && DotnetupSharedManifest.HasDotnetArtifacts(installRequest.InstallRoot.Path))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InstallPathHasUntrackedArtifacts,
                $"The install path '{installRequest.InstallRoot.Path}' already contains a .NET installation that is not tracked by dotnetup. " +
                "To avoid conflicts, use a different install path, remove the existing installation first, " +
                "or use the --untracked option to install without tracking.",
                version: install.Version.ToString(),
                component: installRequest.Component.ToString());
        }

        // The installer bundle replaces the muxer itself, so --require-muxer-update only applies to archives.
        if (installRequest.Options.RequireMuxerUpdate
            && installRequest.Options.InstallMechanism == InstallMechanism.Archive
            && installRequest.InstallRoot.Path is not null)
        {
            MuxerHandler.EnsureMuxerIsWritable(installRequest.InstallRoot.Path);
        }

        return null;
    }

    /// <summary>Creates the artifact that downloads and installs the request using its install mechanism.</summary>
    internal static IInstallArtifact CreateInstallArtifact(
        ResolvedInstallRequest resolvedRequest,
        ReleaseManifest releaseManifest,
        InstallBatchContext batchContext)
    {
        var request = resolvedRequest.Request;
        var version = resolvedRequest.ResolvedVersion;

        return request.Options.InstallMechanism switch
        {
            InstallMechanism.WindowsInstallerBundle => new WindowsBundleArtifact(request, version, batchContext, releaseManifest: releaseManifest),
            _ => new DotnetArchiveExtractor(request, version, releaseManifest, batchContext,
                archiveDownloader: new DotnetDownloader(releaseManifest, cacheDirectory: DotnetupPaths.DownloadCacheDirectory,
                    requireSignature: AccessModeResolver.Resolve().IsAdmin)),
        };
    }

    /// <summary>
    /// Creates the validator that checks whether an install exists. Archive installs validate the layout
    /// dotnetup extracted; installer bundle installs ask hostfxr, since the bundle owns the layout.
    /// </summary>
    internal static IInstallationValidator CreateInstallationValidator(InstallMechanism mechanism) => new InstallationValidator(mechanism);

    /// <summary>
    /// Commits a previously prepared installation: installs the artifact into the target directory
    /// and records it in the manifest. Must be called sequentially (not concurrently).
    /// </summary>
    /// <returns>The installation result.</returns>
#pragma warning disable CA1822
    public InstallResult CommitPreparedInstall(PreparedInstall prepared, IInstallationValidator? installationValidator = null)
    {
        var manifest = prepared.Request.Options.Untracked ? null : new DotnetupSharedManifest(prepared.Request.Options.ManifestPath);

        using (var finalizeLock = ModifyInstallStateMutex())
        {
            IInstallationValidator validator = installationValidator ?? CreateInstallationValidator(prepared.Request.Options.InstallMechanism);
            if (manifest?.InstallAlreadyExists(prepared.Install) == true
                || (prepared.Request.Options.InstallMechanism == InstallMechanism.WindowsInstallerBundle
                    && validator.Validate(prepared.Install)))
            {
                return new InstallResult(prepared.Install, WasAlreadyInstalled: true);
            }

            prepared.Artifact.Commit();

            if (prepared.Artifact.RequiresPostInstallValidation
                && !validator.Validate(prepared.Install, out string? validationFailure))
            {
                throw CreatePostInstallValidationException(prepared, validationFailure);
            }

            manifest?.RecordInstallSpec(prepared.Request);

            manifest?.AddInstallation(prepared.Request.InstallRoot, new Installation
            {
                Component = prepared.Request.Component,
                Version = prepared.Version.ToString(),
                Subcomponents = [.. prepared.Artifact.ExtractedSubcomponents],
            });
        }

        return new InstallResult(prepared.Install, WasAlreadyInstalled: false);
    }
#pragma warning restore CA1822

    private static DotnetInstallException CreatePostInstallValidationException(PreparedInstall prepared, string? validationFailure)
    {
        bool isBundle = prepared.Request.Options.InstallMechanism == InstallMechanism.WindowsInstallerBundle;
        return new DotnetInstallException(
            isBundle ? DotnetInstallErrorCode.InstallerPostInstallVerificationFailed : DotnetInstallErrorCode.InstallFailed,
            string.Format(
                CultureInfo.CurrentCulture,
                isBundle ? Strings.WindowsInstallerPostInstallVerificationFailed : Strings.InstallValidationFailed,
                validationFailure),
            version: prepared.Version.ToString(),
            component: prepared.Request.Component.ToString());
    }
}
