// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.NativeWrapper;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    /// <summary>
    /// Handles garbage collection of workload sets, workload manifests, and workload packs for a feature band.
    ///
    /// This class does not actually do the garbage collection (ie delete / uninstall) the items.  It calculates which
    /// items should be kept installed.  The IInstaller implementation will be responsible for actually uninstalling
    /// the items which are not needed.  The IInstaller implementation should keep "reference counts" for workload packs
    /// and manifests for each feature band.  When it runs garbage collection, it should remove those reference counts
    /// for the current feature band for items not specified in ManifestsToKeep and PacksToKeep.  Then, if an item
    /// has no reference counts left, it can actually be deleted / uninstalled.
    /// </summary>
    internal class WorkloadGarbageCollector
    {
        SdkFeatureBand _sdkFeatureBand;
        string _dotnetDir;
        IEnumerable<WorkloadId> _installedWorkloads;
        Func<string, IWorkloadResolver> _getResolverForWorkloadSet;

        public HashSet<string> WorkloadSetsToKeep = new();
        public HashSet<(ManifestId id, ManifestVersion version, SdkFeatureBand featureBand)> ManifestsToKeep = new();
        public HashSet<(WorkloadPackId id, string version)> PacksToKeep = new();

        public WorkloadGarbageCollector(string dotnetDir, SdkFeatureBand sdkFeatureBand, IEnumerable<WorkloadId> installedWorkloads, Func<string, IWorkloadResolver> getResolverForWorkloadSet)
        {
            _dotnetDir = dotnetDir;
            _sdkFeatureBand = sdkFeatureBand;
            _installedWorkloads = installedWorkloads;
            _getResolverForWorkloadSet = getResolverForWorkloadSet;
        }

        public void Collect()
        {
            GarbageCollectWorkloadSets();
            GarbageCollectWorkloadManifestsAndPacks();
        }

        IWorkloadResolver GetResolver(string workloadSetVersion = null)
        {
            return _getResolverForWorkloadSet(workloadSetVersion);
        }

        void GarbageCollectWorkloadSets()
        {
            //  Determine which workload sets should not be garbage collected.  IInstaller implementation will be responsible for actually uninstalling the other ones (if not referenced by another feature band)
            //  Keep the following, garbage collect all others:
            //  - Workload set if specified in rollback state file, otherwise latest installed workload set
            //  - Workload sets from global.json GC roots (after scanning to see if GC root data is up-to-date)
            //  - Baseline workload sets

            //  What happens if there's a later SDK installed?  Could a workload set from a previous band be pinned to a later SDK?

            var resolver = GetResolver();

            var installedWorkloadSets = resolver.GetWorkloadManifestProvider().GetAvailableWorkloadSets();
            
            var installStateFilePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(_sdkFeatureBand, _dotnetDir), "default.json");
            if (File.Exists(installStateFilePath))
            {
                //  If there is a rollback state file (default.json) in the workload install state folder, don't garbage collect the workload set it specifies.
                var installState = SdkDirectoryWorkloadManifestProvider.InstallStateReader.ReadInstallState(installStateFilePath);
                if (!string.IsNullOrEmpty(installState.WorkloadSetVersion))
                {
                    WorkloadSetsToKeep.Add(installState.WorkloadSetVersion);
                }
            }
            else
            {
                //  If there isn't a rollback state file, don't garbage collect the latest workload set installed for the feature band
                if (installedWorkloadSets.Any())
                {
                    var latestWorkloadSetVersion = installedWorkloadSets.Keys.MaxBy(k => new ReleaseVersion(k));
                    WorkloadSetsToKeep.Add(latestWorkloadSetVersion);
                }
            }

            //  Add baseline workload set versions for installed SDKs to list that shouldn't be collected.  They should stay installed until the SDK is uninstalled
            foreach (var workloadSet in installedWorkloadSets.Values)
            {
                if (workloadSet.IsBaselineWorkloadSet)
                {
                    WorkloadSetsToKeep.Add(workloadSet.Version);
                }
            }

            //  TODO:
            //  Scan workload set GC roots, which correspond to global.json files that pinned to a workload set.  For each one, check to see if it's up-to-date.  If the global.json file
            //  doesn't exist anymore (or doesn't specify a workload set version), delete the GC root.  If the workload set version in global.json has changed, update the GC root.
            //  After updating GC roots, add workload sets listed in GC roots to list of workload sets to keep

        }

        void GarbageCollectWorkloadManifestsAndPacks()
        {
            //  Determine which workload manifests and packs should not be garbage collected
            //  This will be within the scope of a feature band.
            //  The IInstaller implementation will be responsible for actually uninstalling the workload manifests, and it will use feature-band based ref counts to do this.
            //  So if this method determines that a workload manifest should be garbage collected, but another feature band still depends on it, then the installer would remove the ref count for the current
            //  feature band, but not actually uninstall the manifest.

            //  Get default resolver for this feature band and add all manifests it resolves to a list to keep.  This will cover:
            //  - Any manifests listed in the rollback state file (default.json)
            //  - The latest version of each manifest, if a workload set is not installed and there is no rollback state file

            List<IWorkloadResolver> resolvers = new List<IWorkloadResolver>();
            resolvers.Add(GetResolver());


            //  Iterate through all installed workload sets for this SDK feature band that have not been marked for garbage collection
            //  For each manifest version listed in a workload set, add it to a list to keep
            foreach (var workloadSet in WorkloadSetsToKeep)
            {
                resolvers.Add(GetResolver(workloadSet));
            }

            foreach (var resolver in resolvers)
            {
                foreach (var manifest in resolver.GetInstalledManifests())
                {
                    ManifestsToKeep.Add((new ManifestId(manifest.Id), new ManifestVersion(manifest.Version), new SdkFeatureBand(manifest.ManifestFeatureBand)));
                }

                foreach (var pack in _installedWorkloads.SelectMany(workloadId => resolver.GetPacksInWorkload(workloadId))
                    .Select(packId => resolver.TryGetPackInfo(packId))
                    .Where(pack => pack != null))
                {
                    PacksToKeep.Add((pack.Id, pack.Version));
                }
            }

            //  NOTE: We should not collect baseline workload manifests. When we have a corresponding baseline manifest, this will happen, as we have logic
            //  to avoid collecting baseline manifests. Until then, it will be possible for the baseline manifests to be collected.
        }
    }
}
