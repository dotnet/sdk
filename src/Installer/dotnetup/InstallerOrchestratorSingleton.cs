// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

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
        // Map InstallRequest to DotnetInstallObject by converting channel to fully specified version
        ReleaseManifest releaseManifest = new();
        ReleaseVersion? versionToInstall = new ChannelVersionResolver(releaseManifest).Resolve(installRequest);

        if (versionToInstall == null)
        {
            Console.WriteLine($"\nCould not resolve version for channel '{installRequest.Channel.Name}'.");
            return new InstallResult(null, WasAlreadyInstalled: false);
        }

        DotnetInstall install = new(
            installRequest.InstallRoot,
            versionToInstall,
            installRequest.Component);

        string? customManifestPath = installRequest.Options.ManifestPath;

        // Check if the install already exists and we don't need to do anything
        // read write mutex only for manifest?
        using (var finalizeLock = modifyInstallStateMutex())
        {
            if (!finalizeLock.HasHandle)
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.InstallationLocked,
                    $"Could not acquire installation lock. Another dotnetup or installation process may be running.");
            }
            if (InstallAlreadyExists(install, customManifestPath))
            {
                Console.WriteLine($"\n.NET SDK {versionToInstall} is already installed, skipping installation.");
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
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.InstallationLocked,
                    $"Could not acquire installation lock. Another dotnetup or installation process may be running.");
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
