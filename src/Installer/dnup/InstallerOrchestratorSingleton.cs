// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class InstallerOrchestratorSingleton
{
    private static readonly InstallerOrchestratorSingleton _instance = new();

    private InstallerOrchestratorSingleton()
    {
    }

    public static InstallerOrchestratorSingleton Instance => _instance;

    private ScopedMutex modifyInstallStateMutex() => new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

    // Returns null on failure, DotnetInstall on success
    public DotnetInstall? Install(DotnetInstallRequest installRequest, bool noProgress = false)
    {
        // Map InstallRequest to DotnetInstallObject by converting channel to fully specified version
        ReleaseVersion? versionToInstall = new ManifestChannelVersionResolver().Resolve(installRequest);

        if (versionToInstall == null)
        {
            Console.WriteLine($"\nCould not resolve version for channel '{installRequest.Channel.Name}'.");
            return null;
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
            if (InstallAlreadyExists(install, customManifestPath))
            {
                Console.WriteLine($"\n.NET SDK {versionToInstall} is already installed, skipping installation.");
                return install;
            }
        }

        //  TODO: Change noProgress logic so that output should still be printed, just not updates
        IProgressTarget progressTarget = noProgress ? new NullProgressTarget() : new SpectreProgressTarget();

        using ArchiveDotnetExtractor installer = new(installRequest, versionToInstall, progressTarget);
        installer.Prepare();

        // Extract and commit the install to the directory
        using (var finalizeLock = modifyInstallStateMutex())
        {
            if (InstallAlreadyExists(install, customManifestPath))
            {
                return install;
            }

            installer.Commit();

            ArchiveInstallationValidator validator = new();
            if (validator.Validate(install))
            {
                DnupSharedManifest manifestManager = new(customManifestPath);
                manifestManager.AddInstalledVersion(install);
            }
            else
            {
                return null;
            }
        }

        return install;
    }

    /// <summary>
    /// Gets the existing installs from the manifest. Must hold a mutex over the directory.
    /// </summary>
    private IEnumerable<DotnetInstall> GetExistingInstalls(DotnetInstallRoot installRoot, string? customManifestPath = null)
    {
        var manifestManager = new DnupSharedManifest(customManifestPath);
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
