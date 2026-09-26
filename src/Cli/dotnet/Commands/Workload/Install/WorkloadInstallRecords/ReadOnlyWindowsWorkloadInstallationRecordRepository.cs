// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;

#if NETCOREAPP
[SupportedOSPlatform("windows")]
#endif
internal sealed class ReadOnlyWindowsWorkloadInstallationRecordRepository : IWorkloadInstallationRecordRepository
{
    private static readonly string s_hostArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
    private readonly RegistryKey _baseKey;
    private readonly string _basePath;

    internal ReadOnlyWindowsWorkloadInstallationRecordRepository()
        : this(Registry.LocalMachine, @$"SOFTWARE\Microsoft\dotnet\InstalledWorkloads\Standalone\{s_hostArchitecture}")
    {
    }

    internal ReadOnlyWindowsWorkloadInstallationRecordRepository(RegistryKey baseKey, string basePath)
    {
        _baseKey = baseKey;
        _basePath = basePath;
    }

    public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
        => WindowsWorkloadInstallationRecordReader.GetFeatureBandsWithInstallationRecords(_baseKey, _basePath);

    public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand)
        => WindowsWorkloadInstallationRecordReader.GetInstalledWorkloads(_baseKey, _basePath, sdkFeatureBand);

    public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        => throw new NotSupportedException();

    public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        => throw new NotSupportedException();
}
