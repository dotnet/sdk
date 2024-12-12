// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Search.Tests
{
    public class MockWorkloadResolver : IWorkloadResolver
    {
        private readonly IEnumerable<WorkloadResolver.WorkloadInfo> _availableWorkloads;
        private readonly IEnumerable<WorkloadManifestInfo> _installedManifests;
        private readonly Func<WorkloadId, IEnumerable<WorkloadPackId>> _getPacksInWorkload;
        private readonly Func<WorkloadPackId, WorkloadResolver.PackInfo> _getPackInfo;

        public MockWorkloadResolver(
            IEnumerable<WorkloadResolver.WorkloadInfo> availableWorkloads,
            IEnumerable<WorkloadManifestInfo> installedManifests = null,
            Func<WorkloadId, IEnumerable<WorkloadPackId>> getPacks = null,
            Func<WorkloadPackId, WorkloadResolver.PackInfo> getPackInfo = null)
        {
            _availableWorkloads = availableWorkloads;
            _installedManifests = installedManifests;
            _getPacksInWorkload = getPacks;
            _getPackInfo = getPackInfo;
        }

        public IEnumerable<WorkloadResolver.WorkloadInfo> GetAvailableWorkloads() => _availableWorkloads;

        public IEnumerable<WorkloadResolver.PackInfo> GetInstalledWorkloadPacksOfKind(WorkloadPackKind kind) => throw new NotImplementedException();
        public IEnumerable<WorkloadPackId> GetPacksInWorkload(WorkloadId workloadId) => _getPacksInWorkload?.Invoke(workloadId) ?? throw new NotImplementedException();
        public IEnumerable<WorkloadResolver.WorkloadInfo> GetExtendedWorkloads(IEnumerable<WorkloadId> workloadIds) => throw new NotImplementedException();

        public ISet<WorkloadResolver.WorkloadInfo> GetWorkloadSuggestionForMissingPacks(IList<WorkloadPackId> packId, out ISet<WorkloadPackId> unsatisfiablePacks) => throw new NotImplementedException();
        public void RefreshWorkloadManifests() { /* noop */ }
        public WorkloadResolver.PackInfo TryGetPackInfo(WorkloadPackId packId) => _getPackInfo?.Invoke(packId) ?? throw new NotImplementedException();
        public bool IsPlatformIncompatibleWorkload(WorkloadId workloadId) => throw new NotImplementedException();
        public string GetManifestVersion(string manifestId) => throw new NotImplementedException();
        public string GetManifestFeatureBand(string manifestId) => throw new NotImplementedException();
        public IEnumerable<WorkloadManifestInfo> GetInstalledManifests() => _installedManifests ?? throw new NotImplementedException();
        public IWorkloadResolver CreateOverlayResolver(IWorkloadManifestProvider overlayManifestProvider) => throw new NotImplementedException();
        public string GetSdkFeatureBand() => "8.0.100";
        public IWorkloadManifestProvider.WorkloadVersionInfo GetWorkloadVersion() => new IWorkloadManifestProvider.WorkloadVersionInfo("8.0.100.2");
        public IEnumerable<WorkloadId> GetUpdatedWorkloads(WorkloadResolver advertisingManifestResolver, IEnumerable<WorkloadId> installedWorkloads) => throw new NotImplementedException();
        WorkloadResolver IWorkloadResolver.CreateOverlayResolver(IWorkloadManifestProvider overlayManifestProvider) => throw new NotImplementedException();
        WorkloadManifest IWorkloadResolver.GetManifestFromWorkload(WorkloadId workloadId) => throw new NotImplementedException();
        public IWorkloadManifestProvider GetWorkloadManifestProvider() => throw new NotImplementedException();
    }
}
