// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.History;
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
        private IWorkloadResolver _resolver;
        private string _workloadSetVersion;

        public MockWorkloadManifestUpdater(IEnumerable<ManifestUpdateWithWorkloads> manifestUpdates = null, IWorkloadResolver resolver = null, bool fromWorkloadSet = false, string workloadSetVersion = null)
        {
            _manifestUpdates = manifestUpdates ?? new List<ManifestUpdateWithWorkloads>();
            _fromWorkloadSet = fromWorkloadSet;
            _workloadSetVersion = workloadSetVersion;
            _resolver = resolver;
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

        public IEnumerable<ManifestVersionUpdate> CalculateManifestUpdatesFromHistory(WorkloadHistoryRecord record)
        {
            var currentManifests = _resolver?.GetInstalledManifests() ??
                _manifestUpdates?.Select(mu => new WorkloadManifestInfo(
                    mu.ManifestUpdate.ManifestId.ToString(),
                    mu.ManifestUpdate.NewVersion.ToString(),
                    "manifestDir",
                    mu.ManifestUpdate.NewFeatureBand));

            foreach (var manifest in record.StateAfterCommand.ManifestVersions)
            {
                var featureBandAndVersion = manifest.Value.Split('/');
                yield return new ManifestVersionUpdate(new ManifestId(manifest.Key), new ManifestVersion(featureBandAndVersion[0]), featureBandAndVersion[1]);
            }
        }

        public ManifestVersionWithBand GetInstalledManifestVersion(ManifestId manifestId)
        {
            if (_resolver is not null)
            {
                var manifest = _resolver.GetInstalledManifests().FirstOrDefault(m => m.Id.Equals(manifestId.ToString()));
                if (manifest is not null)
                {
                    return new ManifestVersionWithBand(new ManifestVersion(manifest.Version), new SdkFeatureBand(manifest.ManifestFeatureBand));
                }
            }

            var update = _manifestUpdates?.FirstOrDefault(u => u.ManifestUpdate.ManifestId.Equals(manifestId))?.ManifestUpdate;
            if (update is null)
            {
                return null;
            }

            return new ManifestVersionWithBand(update.NewVersion, new SdkFeatureBand(update.NewFeatureBand));
        }

        public Task<IEnumerable<WorkloadDownload>> GetManifestPackageDownloadsAsync(bool includePreviews, SdkFeatureBand providedSdkFeatureBand, SdkFeatureBand installedSdkFeatureBand)
        {
            GetManifestPackageDownloadsCallCount++;
            return Task.FromResult<IEnumerable<WorkloadDownload>>(new List<WorkloadDownload>()
            {
                new WorkloadDownload("mock-manifest", "mock-manifest-package", "1.0.5")
            });
        }

        public IEnumerable<ManifestVersionUpdate> CalculateManifestRollbacks(string rollbackDefinitionFilePath, WorkloadHistoryRecorder recorder = null)
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
        public IEnumerable<ManifestVersionUpdate> ParseRollbackDefinitionFiles(IEnumerable<string> files, WorkloadHistoryRecorder recorder = null) => _manifestUpdates.Select(t => t.ManifestUpdate);

        public IEnumerable<ManifestVersionUpdate> CalculateManifestUpdatesForWorkloadSet(WorkloadSet workloadSet) => _manifestUpdates.Select(t => t.ManifestUpdate);

        public string GetAdvertisedWorkloadSetVersion() => _workloadSetVersion;
        
    }
}
