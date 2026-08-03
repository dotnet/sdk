// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;

#if NETCOREAPP
[SupportedOSPlatform("windows")]
#endif
internal static class WindowsWorkloadInstallationRecordReader
{
    internal static IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords(RegistryKey baseKey, string basePath)
    {
        using RegistryKey? key = baseKey.OpenSubKey(basePath);
        if (key is null)
        {
            return [];
        }

        List<SdkFeatureBand> featureBands = [];
        foreach (string name in key.GetSubKeyNames())
        {
            using RegistryKey? subkey = key.OpenSubKey(name);
            if (subkey is not null && subkey.GetSubKeyNames().Length > 0)
            {
                featureBands.Add(new SdkFeatureBand(name));
            }
        }

        return featureBands;
    }

    internal static IEnumerable<WorkloadId> GetInstalledWorkloads(
        RegistryKey baseKey,
        string basePath,
        SdkFeatureBand sdkFeatureBand)
    {
        using RegistryKey? workloadKey = baseKey.OpenSubKey(Path.Combine(basePath, $"{sdkFeatureBand}"));
        return workloadKey?.GetSubKeyNames().Select(id => new WorkloadId(id)).ToList() ?? [];
    }
}
