// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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

    // Add a doc string mentioning you must hold a mutex over the directory
    private IEnumerable<DotnetInstall> GetExistingInstalls(string directory)
    {
        // assert we have the finalize lock
        return Enumerable.Empty<DotnetInstall>();
    }

    private bool InstallAlreadyExists(string directory, DotnetInstall install)
    {
        // assert we have the finalize lock
        return false;
    }
}
