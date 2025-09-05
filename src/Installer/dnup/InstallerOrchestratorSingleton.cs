// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        DotnetInstall install = new ManifestChannelVersionResolver().Resolve(installRequest);

        // Check if the install already exists and we don't need to do anything
        // read write mutex only for manifest?
        using (var finalizeLock = modifyInstallStateMutex())
        {
            if (InstallAlreadyExists(installRequest.ResolvedDirectory, install))
            {
                Console.WriteLine($"\n.NET SDK {install.FullySpecifiedVersion.Value} is already installed, skipping installation.");
                return install;
            }
        }

        ArchiveDotnetInstaller installer = new(installRequest, install);
        installer.Prepare();

        // Extract and commit the install to the directory
        using (var finalizeLock = modifyInstallStateMutex())
        {
            if (InstallAlreadyExists(installRequest.ResolvedDirectory, install))
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
    private IEnumerable<DotnetInstall> GetExistingInstalls(string directory)
    {
        var manifestManager = new DnupSharedManifest();
        // Use the overload that filters by muxer directory
        return manifestManager.GetInstalledVersions(directory);
    }

    /// <summary>
    /// Checks if the installation already exists. Must hold a mutex over the directory.
    /// </summary>
    private bool InstallAlreadyExists(string directory, DotnetInstall install)
    {
        var existingInstalls = GetExistingInstalls(directory);

        // Check if there's any existing installation that matches the version we're trying to install
        return existingInstalls.Any(existing =>
            existing.FullySpecifiedVersion.Value == install.FullySpecifiedVersion.Value &&
            existing.Type == install.Type &&
            existing.Architecture == install.Architecture);
    }
}
