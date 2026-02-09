// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Result of an installation operation.
/// </summary>
/// <param name="Install">The DotnetInstall, or null if installation failed.</param>
/// <param name="WasAlreadyInstalled">True if the SDK was already installed and no work was done.</param>
internal sealed record InstallResult(DotnetInstall? Install, bool WasAlreadyInstalled);

internal class InstallerOrchestratorSingleton
{
    private static readonly InstallerOrchestratorSingleton _instance = new();

    private InstallerOrchestratorSingleton()
    {
    }

    public static InstallerOrchestratorSingleton Instance => _instance;

    private ScopedMutex modifyInstallStateMutex() => new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

    // Returns InstallResult with Install=null on failure, or Install=DotnetInstall on success
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
        ReleaseVersion? versionToInstall = new ChannelVersionResolver(releaseManifest).Resolve(installRequest);

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
        string componentDescription = installRequest.Component.GetDisplayName();

        // Check if the install already exists and we don't need to do anything
        // read write mutex only for manifest?
        using (var finalizeLock = modifyInstallStateMutex())
        {
            if (!finalizeLock.HasHandle)
            {
                // Log for telemetry but don't block - we may risk clobber but prefer UX over safety here
                // See: https://github.com/dotnet/sdk/issues/52789 for tracking
                Activity.Current?.SetTag("install.mutex_lock_failed", true);
                Activity.Current?.SetTag("install.mutex_lock_phase", "pre_check");
                Console.Error.WriteLine("Warning: Could not acquire installation lock. Another dotnetup process may be running. Proceeding anyway.");
            }
            if (InstallAlreadyExists(install, customManifestPath))
            {
                Console.WriteLine($"\n{componentDescription} {versionToInstall} is already installed, skipping installation.");
                return new InstallResult(install, WasAlreadyInstalled: true);
            }

            // Also check if the component files already exist on disk (e.g., runtime files from SDK install)
            // If so, just add to manifest without downloading
            if (ArchiveInstallationValidator.ComponentFilesExist(install))
            {
                Console.WriteLine($"\n{componentDescription} {versionToInstall} files already exist, adding to manifest.");
                DotnetupSharedManifest manifestManager = new(customManifestPath);
                manifestManager.AddInstalledVersion(install);
                return new InstallResult(install, WasAlreadyInstalled: true);
            }
        }

        IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();

        using DotnetArchiveExtractor installer = new(installRequest, versionToInstall, releaseManifest, progressTarget);
        installer.Prepare();

        // Extract and commit the install to the directory
        using (var finalizeLock = modifyInstallStateMutex())
        {
            if (!finalizeLock.HasHandle)
            {
                // Log for telemetry but don't block - we may risk clobber but prefer UX over safety here
                // See: https://github.com/dotnet/sdk/issues/52789 for tracking
                Activity.Current?.SetTag("install.mutex_lock_failed", true);
                Activity.Current?.SetTag("install.mutex_lock_phase", "commit");
                Console.Error.WriteLine("Warning: Could not acquire installation lock. Another dotnetup process may be running. Proceeding anyway.");
            }
            if (InstallAlreadyExists(install, customManifestPath))
            {
                return new InstallResult(install, WasAlreadyInstalled: true);
            }

            installer.Commit();

            ArchiveInstallationValidator validator = new();
            if (validator.Validate(install))
            {
                DotnetupSharedManifest manifestManager = new(customManifestPath);
                manifestManager.AddInstalledVersion(install);
            }
            else
            {
                return new InstallResult(null, WasAlreadyInstalled: false);
            }
        }

        return new InstallResult(install, WasAlreadyInstalled: false);
    }

    /// <summary>
    /// Gets the existing installs from the manifest. Must hold a mutex over the directory.
    /// </summary>
    private IEnumerable<DotnetInstall> GetExistingInstalls(DotnetInstallRoot installRoot, string? customManifestPath = null)
    {
        var manifestManager = new DotnetupSharedManifest(customManifestPath);
        // Use the overload that filters by muxer directory
        return manifestManager.GetInstalledVersions(installRoot);
    }

    /// <summary>
    /// Checks if the installation already exists. Must hold a mutex over the directory.
    /// </summary>
    private bool InstallAlreadyExists(DotnetInstall install, string? customManifestPath = null)
    {
        var existingInstalls = GetExistingInstalls(install.InstallRoot, customManifestPath);

        // Check if there's any existing installation that matches the version we're trying to install
        return existingInstalls.Any(existing =>
            existing.Version.Equals(install.Version) &&
            existing.Component == install.Component);
    }
}
