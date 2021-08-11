﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.Win32;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Install.InstallRecord
{
    /// <summary>
    /// Provides support for reading and writing workload installation records in the registry
    /// for MSI based workloads.
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    internal class RegistryWorkloadInstallationRecordRepository : InstallerBase, IWorkloadInstallationRecordRepository
    {
        /// <summary>
        /// The base path of workload installation records in the registry.
        /// </summary>
        internal readonly string BasePath = @"SOFTWARE\Microsoft\dotnet\InstalledWorkloads\Standalone";

        /// <summary>
        /// The base key to use when reading/writing records.
        /// </summary>
        private RegistryKey _baseKey = Registry.LocalMachine;


        internal RegistryWorkloadInstallationRecordRepository(InstallElevationContextBase elevationContext, ISetupLogger logger)
            : base(elevationContext, logger)
        {

        }

        /// <summary>
        /// Constructor for testing purposes to allow changing the base key from HKLM.
        /// </summary>
        /// <param name="baseKey">The base key to use, e.g. <see cref="Registry.CurrentUser"/>.</param>
        internal RegistryWorkloadInstallationRecordRepository(InstallElevationContextBase elevationContext, ISetupLogger logger,
            RegistryKey baseKey, string basePath) 
            : this(elevationContext, logger)
        {
            _baseKey = baseKey;
            BasePath = basePath;
        }

        public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            Elevate();

            if (IsElevated)
            {
                string workloadInstallationKeyName = Path.Combine(BasePath, $"{sdkFeatureBand}", $"{workloadId}");
                Log?.LogMessage($"Deleting {workloadInstallationKeyName}");
                _baseKey.DeleteSubKeyTree(workloadInstallationKeyName, throwOnMissingSubKey: false);
            }
            else if (IsClient)
            {
                InstallResponseMessage response = Dispatcher.SendWorkloadRecordRequest(InstallRequestType.DeleteWorkloadInstallationRecord,
                    workloadId, sdkFeatureBand);
                ExitOnFailure(response, "Failed to delete workload record key.");
            }
        }

        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
        {
            using RegistryKey key = _baseKey.OpenSubKey(BasePath);

            // ToList() is needed to ensure deferred execution does not reference closed registry keys.
            return (from string name in key.GetSubKeyNames()
                    let subkey = key.OpenSubKey(name)
                    where subkey.GetSubKeyNames().Length > 0
                    select new SdkFeatureBand(name)).ToList();
        }

        public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand)
        {
            using RegistryKey wrk = _baseKey.OpenSubKey(Path.Combine(BasePath, $"{sdkFeatureBand}"));

            // ToList() is needed to ensure deferred execution does not reference closed registry keys.
            return wrk?.GetSubKeyNames().Select(id => new WorkloadId(id)).ToList() ?? Enumerable.Empty<WorkloadId>();
        }

        public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            Elevate();

            if (IsElevated)
            {
                string subkeyName = Path.Combine(BasePath, $"{sdkFeatureBand}", $"{workloadId}");
                Log?.LogMessage($"Creating {subkeyName}");
                using RegistryKey workloadRecordKey = _baseKey.CreateSubKey(subkeyName);

                if (workloadRecordKey == null)
                {
                    Log?.LogMessage($"Failed to create {subkeyName}");
                }
            }
            else if (IsClient)
            {
                InstallResponseMessage response = Dispatcher.SendWorkloadRecordRequest(InstallRequestType.WriteWorkloadInstallationRecord,
                    workloadId, sdkFeatureBand);

                ExitOnFailure(response, "Failed to write workload record key.");
            }
        }
    }
}
