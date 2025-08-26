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

    private ScopedMutex directoryToMutex(string directory) => new ScopedMutex("Global\\" + directory.GetHashCode());

    private ScopedMutex finalizeLock() => new ScopedMutex("Global\\Finalize");

    public void Install(DotnetInstallRequest installRequest)
    {
        // Map InstallRequest to DotnetInstallObject by converting channel to fully specified version

        // Grab the mutex on the directory to operate on from installRequest
        // Check if the install already exists, if so, return
        // If not, release the mutex and begin the installer.prepare
        // prepare will download the correct archive to a random user protected folder
        // it will then verify the downloaded archive signature / hash.
        //

        // Once prepare is over, grab the finalize lock, then grab the directory lock
        // Check once again if the install exists, if so, return.
        // Run installer.finalize which will extract to the directory to install to.
        // validate the install, then write to the dnup shared manifest
        // Release

        // Clean up the temp folder
    }

    // Add a doc string mentioning you must hold a mutex over the directory
    private IEnumerable<DotnetInstall> GetExistingInstalls(string directory)
    {
        using (var lockScope = directoryToMutex(directory))
        {
            if (lockScope.HasHandle)
            {
                // TODO: Implement logic to get existing installs
                return Enumerable.Empty<DotnetInstall>();
            }
            return Enumerable.Empty<DotnetInstall>();
        }
    }

    private bool InstallAlreadyExists(string directory)
    {
        using (var lockScope = directoryToMutex(directory))
        {
            if (lockScope.HasHandle)
            {
                // TODO: Implement logic to check if install already exists
                return false;
            }
            return false;
        }
    }
}
