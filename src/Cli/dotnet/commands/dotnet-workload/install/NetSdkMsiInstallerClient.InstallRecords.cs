// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;
using Microsoft.Win32.Msi;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal partial class NetSdkMsiInstallerClient
    {
        /// <summary>
        /// Detect installed workload pack records. Only the default registry hive is searched. Finding a workload pack
        /// record does not necessarily guarantee that the MSI is installed.
        /// </summary>
        protected List<WorkloadPackRecord> GetWorkloadPackRecords()
        {
            Log?.LogMessage($"Detecting installed workload packs for {HostArchitecture}.");
            List<WorkloadPackRecord> workloadPackRecords = new();
            using RegistryKey installedPacksKey = Registry.LocalMachine.OpenSubKey(@$"SOFTWARE\Microsoft\dotnet\InstalledPacks\{HostArchitecture}");

            static void SetRecordMsiProperties(WorkloadPackRecord record, RegistryKey key)
            {
                record.ProviderKeyName = (string)key.GetValue("DependencyProviderKey");
                record.ProductCode = (string)key.GetValue("ProductCode");
                record.ProductVersion = new Version((string)key.GetValue("ProductVersion"));
                record.UpgradeCode = (string)key.GetValue("UpgradeCode");
            }

            if (installedPacksKey != null)
            {
                foreach (string packId in installedPacksKey.GetSubKeyNames())
                {
                    using RegistryKey packKey = installedPacksKey.OpenSubKey(packId);

                    foreach (string packVersion in packKey.GetSubKeyNames())
                    {
                        using RegistryKey packVersionKey = packKey.OpenSubKey(packVersion);

                        WorkloadPackRecord record = new WorkloadPackRecord
                        {
                            MsiId = packId,
                            MsiNuGetVersion = packVersion,
                        };

                        SetRecordMsiProperties(record, packVersionKey);

                        record.InstalledPacks.Add((new WorkloadPackId(packId), new NuGetVersion(packVersion)));

                        Log?.LogMessage($"Found workload pack record, Id: {packId}, version: {packVersion}, ProductCode: {record.ProductCode}, provider key: {record.ProviderKeyName}");

                        workloadPackRecords.Add(record);
                    }
                }
            }

            //  Workload pack group installation records are in a similar format as the pack installation records.  They use the "InstalledPackGroups" key,
            //  and under the key for each pack group/version are keys for the workload pack IDs and versions that are in the pack gorup.
            using RegistryKey installedPackGroupsKey = Registry.LocalMachine.OpenSubKey(@$"SOFTWARE\Microsoft\dotnet\InstalledPackGroups\{HostArchitecture}");
            if (installedPackGroupsKey != null)
            {
                foreach (string packGroupId in installedPackGroupsKey.GetSubKeyNames())
                {
                    using RegistryKey packGroupKey = installedPackGroupsKey.OpenSubKey(packGroupId);
                    foreach (string packGroupVersion in packGroupKey.GetSubKeyNames())
                    {
                        using RegistryKey packGroupVersionKey = packGroupKey.OpenSubKey(packGroupVersion);

                        WorkloadPackRecord record = new WorkloadPackRecord
                        {
                            MsiId = packGroupId,
                            MsiNuGetVersion = packGroupVersion
                        };

                        SetRecordMsiProperties(record, packGroupVersionKey);

                        Log?.LogMessage($"Found workload pack group record, Id: {packGroupId}, version: {packGroupVersion}, ProductCode: {record.ProductCode}, provider key: {record.ProviderKeyName}");

                        foreach (string packId in packGroupVersionKey.GetSubKeyNames())
                        {
                            using RegistryKey packIdKey = packGroupVersionKey.OpenSubKey(packId);
                            foreach (string packVersion in packIdKey.GetSubKeyNames())
                            {
                                record.InstalledPacks.Add((new WorkloadPackId(packId), new NuGetVersion(packVersion)));
                                Log?.LogMessage($"Found workload pack in group, Id: {packId}, version: {packVersion}");
                            }
                        }

                        workloadPackRecords.Add(record);
                    }
                }
            }

            return workloadPackRecords;
        }
    }
}
