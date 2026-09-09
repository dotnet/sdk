// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.Versioning;
#if CLI_AOT
using System.Runtime.InteropServices;
#else
using Microsoft.DotNet.Cli.Installer.Windows;
#endif
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;

/// <summary>
/// Provides support for reading and writing workload installation records in the registry
/// for MSI based workloads.
/// </summary>
/// <remarks>
/// Under NativeAOT (CLI_AOT) only the read members are compiled: writing/deleting records requires
/// elevation and MSI-IPC machinery (<see cref="InstallerBase"/>) that is not AOT-safe, so those members
/// (and the base class) are excluded. The read path is shared verbatim with the managed build.
/// </remarks>
#if NETCOREAPP
[SupportedOSPlatform("windows")]
#endif
internal class RegistryWorkloadInstallationRecordRepository :
#if !CLI_AOT
    InstallerBase,
#endif
    IWorkloadInstallationRecordRepository
{
#if CLI_AOT
    /// <summary>
    /// A lower invariant string representation of the processor architecture of the running .NET host.
    /// In the managed build this is provided by <c>InstallerBase</c>.
    /// </summary>
    private static readonly string HostArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
#endif

    /// <summary>
    /// The base path of workload installation records in the registry.
    /// </summary>
    internal readonly string BasePath = @$"SOFTWARE\Microsoft\dotnet\InstalledWorkloads\Standalone\{HostArchitecture}";

    /// <summary>
    /// The base key to use when reading/writing records.
    /// </summary>
    private readonly RegistryKey _baseKey = Registry.LocalMachine;

#if CLI_AOT
    /// <summary>
    /// Read-only constructor used for in-process workload detection under NativeAOT.
    /// </summary>
    internal RegistryWorkloadInstallationRecordRepository()
    {
    }

    /// <summary>
    /// Read-only constructor for testing purposes to allow changing the base key from HKLM.
    /// </summary>
    internal RegistryWorkloadInstallationRecordRepository(RegistryKey baseKey, string basePath)
    {
        _baseKey = baseKey;
        BasePath = basePath;
    }
#else
    internal RegistryWorkloadInstallationRecordRepository(InstallElevationContextBase elevationContext, ISetupLogger logger, bool verifySignatures)
        : base(elevationContext, logger, verifySignatures)
    {

    }

    /// <summary>
    /// Constructor for testing purposes to allow changing the base key from HKLM.
    /// </summary>
    /// <param name="baseKey">The base key to use, e.g. <see cref="Registry.CurrentUser"/>.</param>
    internal RegistryWorkloadInstallationRecordRepository(InstallElevationContextBase elevationContext, ISetupLogger logger,
        RegistryKey baseKey, string basePath)
        : this(elevationContext, logger, verifySignatures: false)
    {
        _baseKey = baseKey;
        BasePath = basePath;
    }
#endif

#if !CLI_AOT
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
#endif

    public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
    {
        using RegistryKey key = _baseKey.OpenSubKey(BasePath);

        // ToList() is needed to ensure deferred execution does not reference closed registry keys.
        return key is null
            ? Enumerable.Empty<SdkFeatureBand>()
            : [.. (from string name in key.GetSubKeyNames()
               let subkey = key.OpenSubKey(name)
               where subkey.GetSubKeyNames().Length > 0
               select new SdkFeatureBand(name))];
    }

    public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand)
    {
        using RegistryKey wrk = _baseKey.OpenSubKey(Path.Combine(BasePath, $"{sdkFeatureBand}"));

        return GetWorkloadInstallationRecordsFromRegistry(wrk);
    }

    private static IEnumerable<WorkloadId> GetWorkloadInstallationRecordsFromRegistry(RegistryKey sdkFeatureBandWorkloadRegistry)
    {
        // ToList() is needed to ensure deferred execution does not reference closed registry keys.
        return sdkFeatureBandWorkloadRegistry?.GetSubKeyNames().Select(id => new WorkloadId(id)).ToList() ?? Enumerable.Empty<WorkloadId>();
    }

#if !CLI_AOT
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
#else
    // Writing/deleting MSI workload records requires elevation and the MSI-IPC dispatcher, neither of
    // which is available in the NativeAOT build. The AOT path only ever reads records; these throw to
    // satisfy IWorkloadInstallationRecordRepository.
    public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        => throw new NotSupportedException();

    public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        => throw new NotSupportedException();
#endif
}
