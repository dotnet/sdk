// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Packaging;

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
        Dictionary<string, string> _globalJsonWorkloadSetVersions;
        IReporter _verboseReporter;

        public HashSet<string> WorkloadSetsToKeep = new();
        public HashSet<(ManifestId id, ManifestVersion version, SdkFeatureBand featureBand)> ManifestsToKeep = new();
        public HashSet<(WorkloadPackId id, string version)> PacksToKeep = new();

        enum GCAction
        {
            Collect = 0,
            KeepWithoutPacks = 1,
            Keep = 2,
        }

        Dictionary<string, GCAction> _workloadSets = new();
        Dictionary<(ManifestId id, ManifestVersion version, SdkFeatureBand featureBand), GCAction> _manifests = new();

        //  globalJsonWorkloadSetVersions should be the contents of the GC Roots file.  The keys should be paths to global.json files, and the values
        //  should be the workload set version referred to by that file.  Before calling this method, the installer implementation should update the
        //  file by removing any outdated entries in it (where for example the global.json file doesn't exist or no longer specifies the same workload
        //  set version).
        public WorkloadGarbageCollector(string dotnetDir, SdkFeatureBand sdkFeatureBand, IEnumerable<WorkloadId> installedWorkloads, Func<string, IWorkloadResolver> getResolverForWorkloadSet,
            Dictionary<string, string> globalJsonWorkloadSetVersions, IReporter verboseReporter)
        {
            _dotnetDir = dotnetDir;
            _sdkFeatureBand = sdkFeatureBand;
            _installedWorkloads = installedWorkloads;
            _getResolverForWorkloadSet = getResolverForWorkloadSet;
            _globalJsonWorkloadSetVersions = globalJsonWorkloadSetVersions;
            _verboseReporter = verboseReporter ?? Reporter.NullReporter;
        }

        public void Collect()
        {
            _verboseReporter.WriteLine("GC: Beginning workload garbage collection.");
            _verboseReporter.WriteLine($"GC: Installed workloads: {string.Join(", ", _installedWorkloads)}");

            GarbageCollectWorkloadSets();
            GarbageCollectWorkloadManifestsAndPacks();

            WorkloadSetsToKeep.AddRange(_workloadSets.Where(kvp => kvp.Value != GCAction.Collect).Select(kvp => kvp.Key));
            ManifestsToKeep.AddRange(_manifests.Where(kvp => kvp.Value != GCAction.Collect).Select(kvp => kvp.Key));
        }

        IWorkloadResolver GetResolver(string workloadSetVersion = null)
        {
            return _getResolverForWorkloadSet(workloadSetVersion);
        }

        void GarbageCollectWorkloadSets()
        {
            //  Determine which workload sets should not be garbage collected.  IInstaller implementation will be responsible for actually uninstalling the other ones (if not referenced by another feature band)
            //  Keep the following, garbage collect all others:
            //  - Baseline workload sets
            //  - Workload set if specified in rollback state file, otherwise latest installed workload set
            //  - Workload sets from global.json GC roots (after scanning to see if GC root data is up-to-date)
            //  Baseline workload sets and manifests should be kept, but if they aren't active, the packs should be garbage collected.
            //  GCAction.KeepWithoutPacks is for keeping track of this

            var resolver = GetResolver();

            var installedWorkloadSets = resolver.GetWorkloadManifestProvider().GetAvailableWorkloadSets();
            _workloadSets = installedWorkloadSets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.IsBaselineWorkloadSet ? GCAction.KeepWithoutPacks : GCAction.Collect);

            var installStateFilePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(_sdkFeatureBand, _dotnetDir), "default.json");
            var installState = InstallStateContents.FromPath(installStateFilePath);
            //  If there is a rollback state file (default.json) in the workload install state folder, don't garbage collect the workload set it specifies.
            if (!string.IsNullOrEmpty(installState.WorkloadVersion))
            {
                if (installedWorkloadSets.ContainsKey(installState.WorkloadVersion))
                {
                    _workloadSets[installState.WorkloadVersion] = GCAction.Keep;
                    _verboseReporter.WriteLine($"GC: Keeping workload set version {installState.WorkloadVersion} because it is specified in the install state file {installStateFilePath}");
                }
                else
                {
                    _verboseReporter.WriteLine($"GC: Error: Workload set version {installState.WorkloadVersion} which was specified in {installStateFilePath} was not found.  This is likely an invalid state.");
                }
            }
            else
            {
                //  If there isn't a rollback state file, don't garbage collect the latest workload set installed for the feature band
                if (installedWorkloadSets.Any())
                {
                    var latestWorkloadSetVersion = installedWorkloadSets.Keys.MaxBy(k => new ReleaseVersion(k));
                    _workloadSets[latestWorkloadSetVersion] = GCAction.Keep;
                    _verboseReporter.WriteLine($"GC: Keeping latest installed workload set version {latestWorkloadSetVersion}");
                }
            }

            foreach (var (globalJsonPath, workloadSetVersion) in _globalJsonWorkloadSetVersions)
            {
                if (installedWorkloadSets.ContainsKey(workloadSetVersion))
                {
                    _workloadSets[workloadSetVersion] = GCAction.Keep;
                    _verboseReporter.WriteLine($"GC: Keeping workload set version {workloadSetVersion} because it is referenced by {globalJsonPath}.");
                }
            }
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

            List<(IWorkloadResolver, string workloadSet, GCAction gcAction)> resolvers = new();
            resolvers.Add((GetResolver(), "<none>", GCAction.Keep));


            //  Iterate through all installed workload sets for this SDK feature band that have not been marked for garbage collection
            //  For each manifest version listed in a workload set, add it to a list to keep
            foreach (var (workloadSet, gcAction) in _workloadSets)
            {
                if (gcAction != GCAction.Collect)
                {
                    resolvers.Add((GetResolver(workloadSet), workloadSet, gcAction));
                }
            }

            foreach (var (resolver, workloadSet, gcAction) in resolvers)
            {
                foreach (var manifest in resolver.GetInstalledManifests())
                {
                    _verboseReporter.WriteLine($"GC: Keeping manifest {manifest.Id} {manifest.Version}/{manifest.ManifestFeatureBand} as part of workload set {workloadSet}");

                    var manifestKey = (new ManifestId(manifest.Id), new ManifestVersion(manifest.Version), new SdkFeatureBand(manifest.ManifestFeatureBand));
                    GCAction existingAction;
                    if (!_manifests.TryGetValue(manifestKey, out existingAction))
                    {
                        existingAction = GCAction.Collect;
                    }

                    //  We should keep a manifest if it's referenced by any workload set we're planning to keep.  If there are multiple resolvers that end up referencing
                    //  a workload manifest, we should take the "greater" action.  IE if a manifest would be KeepWithoutPacks with one resolver and Keep with another one,
                    //  then it (and its packs) should be kept.
                    //  The scenario where there would be a mismatch is if there's a baseline workload set that's not active referring to the same manifest as an active
                    //  workload set.  The manifest would be marked KeepWithoutPacks via the baseline manifest, and Keep via the active workload set.
                    if (gcAction > existingAction)
                    {
                        _manifests[manifestKey] = gcAction;
                    }
                }

                if (gcAction == GCAction.Keep)
                {
                    foreach (var pack in _installedWorkloads.SelectMany(workloadId => resolver.GetPacksInWorkload(workloadId))
                        .Select(packId => resolver.TryGetPackInfo(packId))
                        .Where(pack => pack != null))
                    {
                        _verboseReporter.WriteLine($"GC: Keeping workload pack {pack.ResolvedPackageId} {pack.Version} as part of workload set {workloadSet}");
                        PacksToKeep.Add((new WorkloadPackId(pack.ResolvedPackageId), pack.Version));
                    }
                }
            }

            //  NOTE: We should not collect baseline workload manifests. When we have a corresponding baseline workload set, this will happen, as we have logic
            //  to avoid collecting baseline manifests. Until then, it will be possible for the baseline manifests to be collected.
        }
    }
}
