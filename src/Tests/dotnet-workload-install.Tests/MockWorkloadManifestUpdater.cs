// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockWorkloadManifestUpdater : IWorkloadManifestUpdater
    {
        public int UpdateAdvertisingManifestsCallCount = 0;
        public int CalculateManifestUpdatesCallCount = 0;
        public int GetManifestPackageDownloadsCallCount = 0;
        private readonly IEnumerable<ManifestUpdateWithWorkloads> _manifestUpdates;
        private bool _fromWorkloadSet;

        public MockWorkloadManifestUpdater(IEnumerable<ManifestUpdateWithWorkloads> manifestUpdates = null, bool fromWorkloadSet = false)
        {
            _manifestUpdates = manifestUpdates ?? new List<ManifestUpdateWithWorkloads>();
            _fromWorkloadSet = fromWorkloadSet;
        }

        public Task UpdateAdvertisingManifestsAsync(bool includePreview, bool useWorkloadSets = false, DirectoryPath? cachePath = null)
        {
            UpdateAdvertisingManifestsCallCount++;
            return Task.CompletedTask;
        }

        public IEnumerable<ManifestUpdateWithWorkloads> CalculateManifestUpdates()
        {
            CalculateManifestUpdatesCallCount++;
            return _manifestUpdates;
        }

        public Task<IEnumerable<WorkloadDownload>> GetManifestPackageDownloadsAsync(bool includePreviews, SdkFeatureBand providedSdkFeatureBand, SdkFeatureBand installedSdkFeatureBand)
        {
            GetManifestPackageDownloadsCallCount++;
            return Task.FromResult<IEnumerable<WorkloadDownload>>(new List<WorkloadDownload>()
            {
                new WorkloadDownload("mock-manifest", "mock-manifest-package", "1.0.5")
            });
        }

        public IEnumerable<ManifestVersionUpdate> CalculateManifestRollbacks(string rollbackDefinitionFilePath)
        {
            if (_fromWorkloadSet && !rollbackDefinitionFilePath.EndsWith("installed.workloadset.json"))
            {
                throw new Exception("Should be updating or installing via workload set.");
            }

            return _manifestUpdates.Select(t => t.ManifestUpdate);
        }

        public Task BackgroundUpdateAdvertisingManifestsWhenRequiredAsync() => throw new NotImplementedException();
        public IEnumerable<WorkloadId> GetUpdatableWorkloadsToAdvertise(IEnumerable<WorkloadId> installedWorkloads) => throw new NotImplementedException();
        public void DeleteUpdatableWorkloadsFile() { }

        public void DownloadWorkloadSet(string version, DirectoryPath? offlineCache) => throw new NotImplementedException();
        public IEnumerable<ManifestVersionUpdate> ParseRollbackDefinitionFiles(IEnumerable<string> files) => _manifestUpdates.Select(t => t.ManifestUpdate);
    }
}
