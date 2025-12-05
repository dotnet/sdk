// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal interface IWorkloadManifestUpdater
{
    Task UpdateAdvertisingManifestsAsync(bool includePreviews, bool useWorkloadSets = false, DirectoryPath? offlineCache = null);

    Task BackgroundUpdateAdvertisingManifestsWhenRequiredAsync();

    IEnumerable<ManifestUpdateWithWorkloads> CalculateManifestUpdates();

    string GetAdvertisedWorkloadSetVersion();
    IEnumerable<ManifestVersionUpdate> CalculateManifestRollbacks(string rollbackDefinitionFilePath, WorkloadHistoryRecorder recorder = null);
    IEnumerable<ManifestVersionUpdate> CalculateManifestUpdatesFromHistory(WorkloadHistoryState state);
    IEnumerable<ManifestVersionUpdate> CalculateManifestUpdatesForWorkloadSet(WorkloadSet workloadSet);

    Task<IEnumerable<WorkloadDownload>> GetManifestPackageDownloadsAsync(bool includePreviews, SdkFeatureBand providedSdkFeatureBand, SdkFeatureBand installedSdkFeatureBand);

    IEnumerable<WorkloadId> GetUpdatableWorkloadsToAdvertise(IEnumerable<WorkloadId> installedWorkloads);

    void DeleteUpdatableWorkloadsFile();
}

internal record ManifestUpdateWithWorkloads(ManifestVersionUpdate ManifestUpdate, WorkloadCollection Workloads);
