// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;

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
    public DotnetInstall? Install(DotnetInstallRequest installRequest)
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

        // Check if the install already exists and we don't need to do anything
        // read write mutex only for manifest?
        using (var finalizeLock = modifyInstallStateMutex())
        {
            if (InstallAlreadyExists(install))
            {
                Console.WriteLine($"\n.NET SDK {versionToInstall} is already installed, skipping installation.");
                return install;
            }
        }

        using ArchiveDotnetInstaller installer = new(installRequest, versionToInstall);
        installer.Prepare();

        // Extract and commit the install to the directory
        using (var finalizeLock = modifyInstallStateMutex())
        {
            if (InstallAlreadyExists(install))
            {
                return install;
            }

            installer.Commit();

            ArchiveInstallationValidator validator = new();
            if (validator.Validate(install))
            {
                DnupSharedManifest manifestManager = new();
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
    private IEnumerable<DotnetInstall> GetExistingInstalls(DotnetInstallRoot installRoot)
    {
        var manifestManager = new DnupSharedManifest();
        // Use the overload that filters by muxer directory
        return manifestManager.GetInstalledVersions(installRoot);
    }

    /// <summary>
    /// Checks if the installation already exists. Must hold a mutex over the directory.
    /// </summary>
    private bool InstallAlreadyExists(DotnetInstall install)
    {
        var existingInstalls = GetExistingInstalls(install.InstallRoot);

        // Check if there's any existing installation that matches the version we're trying to install
        return existingInstalls.Any(existing =>
            existing.Version.Equals(install.Version) &&
            existing.Component == install.Component);
    }
}
