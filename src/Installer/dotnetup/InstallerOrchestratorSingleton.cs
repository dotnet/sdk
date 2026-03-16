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

    // Throws DotnetInstallException on failure, returns InstallResult on success
#pragma warning disable CA1822 // Intentionally an instance method on a singleton
    public InstallResult Install(DotnetInstallRequest installRequest, bool noProgress = false)
    {
        // Validate channel format before attempting resolution
        if (!ChannelVersionResolver.IsValidChannelFormat(installRequest.Channel.Name))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InvalidChannel,
                $"'{installRequest.Channel.Name}' is not a valid .NET version or channel. " +
                $"Use a version like '9.0', '9.0.100', or a channel keyword: {string.Join(", ", ChannelVersionResolver.KnownChannelKeywords)}.",
                version: null, // Don't include user input in telemetry
                component: installRequest.Component.ToString());
        }

        // Map InstallRequest to DotnetInstallObject by converting channel to fully specified version
        ReleaseManifest releaseManifest = new();
        ReleaseVersion? versionToInstall = installRequest.ResolvedVersion
            ?? new ChannelVersionResolver(releaseManifest).Resolve(installRequest);

        if (versionToInstall == null)
        {
            // Channel format was valid, but the version doesn't exist
            throw new DotnetInstallException(
                DotnetInstallErrorCode.VersionNotFound,
                $"Could not find .NET version '{installRequest.Channel.Name}'. The version may not exist or may not be supported.",
                version: null, // Don't include user input in telemetry
                component: installRequest.Component.ToString());
        }

        DotnetInstall install = new(
            installRequest.InstallRoot,
            versionToInstall,
            installRequest.Component);

        string? customManifestPath = installRequest.Options.ManifestPath;

        // Check if the install already exists and we don't need to do anything
        using (var finalizeLock = ModifyInstallStateMutex())
        {
            // Untracked installs don't interact with the manifest, so skip reading it
            // entirely. This avoids errors when the manifest uses a legacy format.
            var manifestData = installRequest.Options.Untracked
                ? new DotnetupManifestData()
                : new DotnetupSharedManifest(customManifestPath).ReadManifest();

            if (InstallAlreadyExists(manifestData, install))
            {
                // Still record the install spec so the user's requested channel is tracked,
                // even though the version is already installed (possibly via a different channel).
                RecordInstallSpec(installRequest, customManifestPath);
                return new InstallResult(install, WasAlreadyInstalled: true);
            }

            // Guard: error if the target directory contains .NET artifacts but isn't tracked in the manifest.
            // This prevents silently mixing managed and unmanaged installations.
            // Skip this guard for untracked installs.
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

            // Fail fast: if the muxer must be updated and it is currently locked,
            // throw before the expensive download.  The check is inside the mutex
            // so it does not race with other dotnetup processes.
            if (installRequest.Options.RequireMuxerUpdate && installRequest.InstallRoot.Path is not null)
            {
                MuxerHandler.EnsureMuxerIsWritable(installRequest.InstallRoot.Path);
            }
        }

        IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();

        using DotnetArchiveExtractor installer = new(installRequest, versionToInstall, releaseManifest, progressTarget);
        installer.Prepare();

        // Extract and commit the install to the directory
        using (var finalizeLock = ModifyInstallStateMutex())
        {
            // Untracked installs skip manifest entirely to avoid legacy format errors.
            var manifestData = installRequest.Options.Untracked
                ? new DotnetupManifestData()
                : new DotnetupSharedManifest(customManifestPath).ReadManifest();

            if (InstallAlreadyExists(manifestData, install))
            {
                return new InstallResult(install, WasAlreadyInstalled: true);
            }

            installer.Commit();

            ArchiveInstallationValidator validator = new();
            if (validator.Validate(install, out string? validationFailure))
            {
                RecordInstallSpec(installRequest, customManifestPath);

                // Record the installation with its resolved version
                if (!installRequest.Options.Untracked)
                {
                    var manifestManager = new DotnetupSharedManifest(customManifestPath);
                    manifestManager.AddInstallation(installRequest.InstallRoot, new Installation
                    {
                        Component = installRequest.Component,
                        Version = versionToInstall.ToString(),
                        Subcomponents = [.. installer.ExtractedSubcomponents]
                    });
                }
            }
            else
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.InstallFailed,
                    $"Installation validation failed: {validationFailure}",
                    version: versionToInstall.ToString(),
                    component: installRequest.Component.ToString());
            }
        }

        return new InstallResult(install, WasAlreadyInstalled: false);
    }
#pragma warning restore CA1822

    /// <summary>
    /// Installs multiple components with concurrent downloads and serialized extraction.
    /// All pre-checks run first, then archives download in parallel with separate progress bars,
    /// and finally extraction/commit happens one at a time.
    /// </summary>
#pragma warning disable CA1822 // Intentionally an instance method on a singleton
    public IReadOnlyList<InstallResult> InstallMultiple(IReadOnlyList<DotnetInstallRequest> installRequests, bool noProgress = false)
    {
        if (installRequests.Count == 0)
        {
            return [];
        }

        // For a single request, delegate to the existing path
        if (installRequests.Count == 1)
        {
            return [Install(installRequests[0], noProgress)];
        }

        ReleaseManifest releaseManifest = new();

        // Phase 1: Validate and resolve all requests, check for already-installed
        var prepared = new List<(int OriginalIndex, DotnetInstallRequest Request, ReleaseVersion Version, DotnetInstall Install, string? ManifestPath)>();
        var results = new InstallResult?[installRequests.Count];

        for (int i = 0; i < installRequests.Count; i++)
        {
            var request = installRequests[i];

            if (!ChannelVersionResolver.IsValidChannelFormat(request.Channel.Name))
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.InvalidChannel,
                    $"'{request.Channel.Name}' is not a valid .NET version or channel. " +
                    $"Use a version like '9.0', '9.0.100', or a channel keyword: {string.Join(", ", ChannelVersionResolver.KnownChannelKeywords)}.",
                    version: null,
                    component: request.Component.ToString());
            }

            ReleaseVersion? version = request.ResolvedVersion
                ?? new ChannelVersionResolver(releaseManifest).Resolve(request);

            if (version == null)
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.VersionNotFound,
                    $"Could not find .NET version '{request.Channel.Name}'. The version may not exist or may not be supported.",
                    version: null,
                    component: request.Component.ToString());
            }

            DotnetInstall install = new(request.InstallRoot, version, request.Component);
            string? customManifestPath = request.Options.ManifestPath;

            using (var finalizeLock = ModifyInstallStateMutex())
            {
                var manifestData = request.Options.Untracked
                    ? new DotnetupManifestData()
                    : new DotnetupSharedManifest(customManifestPath).ReadManifest();

                if (InstallAlreadyExists(manifestData, install))
                {
                    RecordInstallSpec(request, customManifestPath);
                    results[i] = new InstallResult(install, WasAlreadyInstalled: true);
                    continue;
                }

                if (!request.Options.Untracked
                    && !IsRootInManifest(manifestData, request.InstallRoot)
                    && HasDotnetArtifacts(request.InstallRoot.Path))
                {
                    throw new DotnetInstallException(
                        DotnetInstallErrorCode.Unknown,
                        $"The install path '{request.InstallRoot.Path}' already contains a .NET installation that is not tracked by dotnetup. " +
                        "To avoid conflicts, use a different install path or remove the existing installation first.",
                        version: version.ToString(),
                        component: request.Component.ToString());
                }

                if (request.Options.RequireMuxerUpdate && request.InstallRoot.Path is not null)
                {
                    MuxerHandler.EnsureMuxerIsWritable(request.InstallRoot.Path);
                }
            }

            prepared.Add((i, request, version, install, customManifestPath));
        }

        // If everything was already installed, return early
        if (prepared.Count == 0)
        {
            return [.. results.Select(r => r!)];
        }

        // Phase 2: Download all archives concurrently with shared progress
        IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();
        using var sharedReporter = progressTarget.CreateProgressReporter();

        var extractors = new DotnetArchiveExtractor[prepared.Count];
        try
        {
            for (int i = 0; i < prepared.Count; i++)
            {
                extractors[i] = new DotnetArchiveExtractor(
                    prepared[i].Request,
                    prepared[i].Version,
                    releaseManifest,
                    progressTarget,
                    sharedReporter: sharedReporter);
            }

            // Download concurrently
            var downloadTasks = extractors.Select(e => Task.Run(() => e.Prepare())).ToArray();
            Task.WaitAll(downloadTasks);

            // Phase 3: Commit sequentially
            for (int i = 0; i < prepared.Count; i++)
            {
                var (originalIndex, request, version, install, customManifestPath) = prepared[i];

                using (var finalizeLock = ModifyInstallStateMutex())
                {
                    var manifestData = request.Options.Untracked
                        ? new DotnetupManifestData()
                        : new DotnetupSharedManifest(customManifestPath).ReadManifest();

                    if (InstallAlreadyExists(manifestData, install))
                    {
                        results[originalIndex] = new InstallResult(install, WasAlreadyInstalled: true);
                        continue;
                    }

                    extractors[i].Commit();

                    ArchiveInstallationValidator validator = new();
                    if (validator.Validate(install, out string? validationFailure))
                    {
                        RecordInstallSpec(request, customManifestPath);

                        if (!request.Options.Untracked)
                        {
                            var manifestManager = new DotnetupSharedManifest(customManifestPath);
                            manifestManager.AddInstallation(request.InstallRoot, new Installation
                            {
                                Component = request.Component,
                                Version = version.ToString(),
                                Subcomponents = [.. extractors[i].ExtractedSubcomponents]
                            });
                        }
                    }
                    else
                    {
                        throw new DotnetInstallException(
                            DotnetInstallErrorCode.InstallFailed,
                            $"Installation validation failed: {validationFailure}",
                            version: version.ToString(),
                            component: request.Component.ToString());
                    }
                }

                results[originalIndex] = new InstallResult(install, WasAlreadyInstalled: false);
            }
        }
        finally
        {
            foreach (var extractor in extractors)
            {
                extractor?.Dispose();
            }
        }

        return [.. results.Select(r => r!)];
    }
#pragma warning restore CA1822

    /// <summary>
    /// Records the install specin the manifest, respecting Untracked and SkipInstallSpecRecording flags.
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
