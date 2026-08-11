// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// An enumeration describing the requests the install client can
    /// send to the elevated server. Each requests describes an operaiton that
    /// requires elevation.
    /// </summary>
    public enum InstallRequestType
    {
        /// <summary>
        /// Requests the server to shutdown.
        /// </summary>
        Shutdown = 0,

        /// <summary>
        /// Request an MSI payload to be cached.
        /// </summary>
        CachePayload = 100,

        /// <summary>
        /// Add a dependent to the provider key of an MSI.
        /// </summary>
        AddDependent = 200,

        /// <summary>
        /// Remove a dependent from an MSI's provider key;
        /// </summary>
        RemoveDependent,

        /// <summary>
        /// Install an MSI.
        /// </summary>
        InstallMsi = 300,

        /// <summary>
        /// Uninstall an MSI.
        /// </summary>
        UninstallMsi,

        /// <summary>
        /// Repair an MSI.
        /// </summary>
        RepairMsi,

        /// <summary>
        /// Create a workload installation record.
        /// </summary>
        WriteWorkloadInstallationRecord = 400,

        /// <summary>
        /// Remove a workload installation record.
        /// </summary>
        DeleteWorkloadInstallationRecord,

        /// <summary>
        /// Adds manifests to the install state file.
        /// </summary>
        SaveInstallStateManifestVersions,

        /// <summary>
        /// Removes manifests from an install state file.
        /// </summary>
        RemoveManifestsFromInstallStateFile,

        /// <summary>
        /// Changes the workload mode
        /// </summary>
        AdjustWorkloadMode,

        /// <summary>
        /// Changes the workload set version
        /// </summary>
        AdjustWorkloadSetVersion,

        /// <summary>
        /// Record a workload set referenced by global.json in workload roots file
        /// </summary>
        RecordWorkloadSetInGlobalJson,

        /// <summary>
        /// Remove outdated entries from the global.json workload roots file and then return its contents
        /// </summary>
        GetGlobalJsonWorkloadSetVersions,
    }
}
