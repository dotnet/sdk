// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using Microsoft.DotNet.Workloads.Workload;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockPackWorkloadInstaller : IInstaller
    {
        public IList<PackInfo> InstalledPacks;
        public List<PackInfo> RolledBackPacks = new();
        public IList<(ManifestVersionUpdate manifestUpdate, DirectoryPath? offlineCache)> InstalledManifests =
            new List<(ManifestVersionUpdate manifestUpdate, DirectoryPath?)>();
        public string CachePath;
        public bool GarbageCollectionCalled = false;
        public MockInstallationRecordRepository InstallationRecordRepository;
        public bool FailingRollback;
        public bool FailingGarbageCollection;
        private readonly string FailingPack;
        private readonly string _dotnetDir;

        public IWorkloadResolver WorkloadResolver { get; set; }

        public int ExitCode => 0;

        public MockPackWorkloadInstaller(string dotnetDir, string failingWorkload = null, string failingPack = null, bool failingRollback = false, IList<WorkloadId> installedWorkloads = null,
            IList<PackInfo> installedPacks = null, bool failingGarbageCollection = false)
        {
            InstallationRecordRepository = new MockInstallationRecordRepository(failingWorkload, installedWorkloads);
            FailingRollback = failingRollback;
            InstalledPacks = installedPacks ?? new List<PackInfo>();
            FailingPack = failingPack;
            FailingGarbageCollection = failingGarbageCollection;
            _dotnetDir = dotnetDir;
        }

        IEnumerable<PackInfo> GetPacksForWorkloads(IEnumerable<WorkloadId> workloadIds)
        {
            if (WorkloadResolver == null)
            {
                return Enumerable.Empty<PackInfo>();
            }
            else
            {
                return workloadIds
                        .SelectMany(workloadId => WorkloadResolver.GetPacksInWorkload(workloadId))
                        .Distinct()
                        .Select(packId => WorkloadResolver.TryGetPackInfo(packId))
                        .Where(pack => pack != null).ToList();
            }
        }

        public void UpdateInstallMode(SdkFeatureBand sdkFeatureBand, bool newMode)
        {
            throw new NotImplementedException();
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, ITransactionContext transactionContext, DirectoryPath? offlineCache = null)
        {
            List<PackInfo> packs = new();

            transactionContext.Run(action: () =>
            {
                CachePath = offlineCache?.Value;

                packs = GetPacksForWorkloads(workloadIds).ToList();

                foreach (var packInfo in packs)
                {
                    InstalledPacks = InstalledPacks.Append(packInfo).ToList();
                    if (packInfo.Id.ToString().Equals(FailingPack))
                    {
                        throw new Exception($"Failing pack: {packInfo.Id}");
                    }
                }
            },
            rollback: () =>
            {
                if (FailingRollback)
                {
                    throw new Exception("Rollback failure");
                }

                RolledBackPacks.AddRange(packs);
            });
        }

        public void RepairWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null) => throw new NotImplementedException();

        public void GarbageCollect(Func<string, IWorkloadResolver> getResolverForWorkloadSet, DirectoryPath? offlineCache = null, bool cleanAllPacks = false)
        {
            if (FailingGarbageCollection)
            {
                throw new Exception("Failing garbage collection");
            }
            GarbageCollectionCalled = true;
        }

        public IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository()
        {
            return InstallationRecordRepository;
        }

        public void InstallWorkloadManifest(ManifestVersionUpdate manifestUpdate, ITransactionContext transactionContext, DirectoryPath? offlineCache = null, bool isRollback = false)
        {
            InstalledManifests.Add((manifestUpdate, offlineCache));
        }

        public IEnumerable<WorkloadDownload> GetDownloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, bool includeInstalledItems)
        {
            var packs = GetPacksForWorkloads(workloadIds);

            if (!includeInstalledItems)
            {
                packs = packs.Where(p => !InstalledPacks.Any(installed => installed.ResolvedPackageId == p.ResolvedPackageId && installed.Version == p.Version));
            }

            return packs.Select(p => new WorkloadDownload(p.ResolvedPackageId, p.ResolvedPackageId, p.Version));
        }

        public void Shutdown()
        {

        }

        public PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand)
        {
            return new PackageId($"{manifestId}.Manifest-{featureBand}");
        }

        public List<(string nupkgPath, string targetPath)> ExtractCallParams = new();

        public Task ExtractManifestAsync(string nupkgPath, string targetPath)
        {
            ExtractCallParams.Add((nupkgPath, targetPath));

            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }
            Directory.CreateDirectory(targetPath);

            string manifestContents = $@"{{
  ""version"": ""1.0.42"",
  ""workloads"": {{
    }}
  }},
  ""packs"": {{
  }}
}}";

            File.WriteAllText(Path.Combine(targetPath, "WorkloadManifest.json"), manifestContents);

            return Task.CompletedTask;
        }

        public void ReplaceWorkloadResolver(IWorkloadResolver workloadResolver)
        {
            WorkloadResolver = workloadResolver;
        }

        public void RemoveManifestsFromInstallState(SdkFeatureBand sdkFeatureBand)
        {
            string path = Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, _dotnetDir), "default.json");
            if (File.Exists(path))
            {
                var installStateContents = File.Exists(path) ? InstallStateContents.FromString(File.ReadAllText(path)) : new InstallStateContents();
                installStateContents.Manifests = null;
                File.WriteAllText(path, installStateContents.ToString());
            }
        }

        public void SaveInstallStateManifestVersions(SdkFeatureBand sdkFeatureBand, Dictionary<string, string> manifestContents)
        {
            string path = Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, _dotnetDir), "default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var installStateContents = File.Exists(path) ? InstallStateContents.FromString(File.ReadAllText(path)) : new InstallStateContents();
            installStateContents.Manifests = manifestContents;
            File.WriteAllText(path, installStateContents.ToString());
        }
    }

    internal class MockInstallationRecordRepository : IWorkloadInstallationRecordRepository
    {
        public IList<WorkloadId> WorkloadInstallRecord = new List<WorkloadId>();
        private readonly string FailingWorkload;
        public IList<WorkloadId> InstalledWorkloads;

        public MockInstallationRecordRepository(string failingWorkload = null, IList<WorkloadId> installedWorkloads = null)
        {
            FailingWorkload = failingWorkload;
            InstalledWorkloads = installedWorkloads ?? new List<WorkloadId>();
        }

        public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            WorkloadInstallRecord.Add(workloadId);
            if (workloadId.ToString().Equals(FailingWorkload))
            {
                throw new Exception($"Failing workload: {workloadId}");
            }
        }

        public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            WorkloadInstallRecord.Remove(workloadId);
        }
        public IEnumerable<WorkloadId> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand)
        {
            return InstalledWorkloads;
        }

        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords()
        {
            return Enumerable.Empty<SdkFeatureBand>();
        }
    }
}
