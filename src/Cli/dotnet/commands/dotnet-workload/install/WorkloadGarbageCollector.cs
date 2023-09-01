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
    internal class WorkloadGarbageCollector
    {

        string _sdkVersion;
        string _dotnetDir;
        List<string> installedSdkVersions;

        public HashSet<string> WorkloadSetsToKeep = new();

        public WorkloadGarbageCollector(string dotnetDir, string sdkVersion)
        {
            _sdkVersion = sdkVersion;
            _dotnetDir = dotnetDir;

            installedSdkVersions = NETCoreSdkResolverNativeWrapper.GetAvailableSdks(_dotnetDir)
                .Select(path => new DirectoryInfo(path).Name)
                .OrderByDescending(version => new ReleaseVersion(version))
                .ToList();

        }

        IWorkloadResolver GetResolver(string sdkVersion, SdkDirectoryWorkloadManifestProvider.InstallState installState = null)
        {
            throw new NotImplementedException();
        }

        void GarbageCollectWorkloadSets()
        {
            //  Determine which workload sets to garbage collect.  IInstaller implementation will be responsible for actually uninstalling them
            //  Keep the following, garbage collect all others:
            //  - Workload set if specified in rollback state file, otherwise latest installed workload set
            //  - Workload sets from global.json GC roots (after scanning to see if GC root data is up-to-date)
            //  - Baseline workload sets

            //  What happens if there's a later SDK installed?  Could a workload set from a previous banned be pinned to a later SDK?

            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            var resolver = GetResolver(_sdkVersion);


            var installedWorkloadSets = resolver.GetWorkloadManifestProvider().GetAvailableWorkloadSets();

            
            var installStateFilePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, _dotnetDir), "default.json");
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

            //  TODO:  Add baseline workload set versions for installed SDKs to list that shouldn't be collected.  They should stay installed until the SDK is uninstalled
            //  Alternatively just don't collect workload sets that have a file named baseline.workloadset.json

            //  TODO:
            //  Scan workload set GC roots, which correspond to global.json files that pinned to a workload set.  For each one, check to see if it's up-to-date.  If the global.json file
            //  doesn't exist anymore (or doesn't specify a workload set version), delete the GC root.  If the workload set version in global.json has changed, update the GC root.
            //  After updating GC roots, add workload sets listed in GC roots to list of workload sets to keep



            ////  Garbage collect all other workload sets.
            //foreach (var workloadSet in installedWorkloadSets.Keys)
            //{
            //    if (!workloadSetsToKeep.Contains(workloadSet))
            //    {
            //        WorkloadSetsToGarbageCollect.Add(workloadSet);
            //    }
            //}


            //var manifestInstallDirForFeatureBand = GetManifestInstallDirForFeatureBand(_sdkFeatureBand.ToString());
            //string workloadSetsDirectory = Path.Combine(manifestInstallDirForFeatureBand, SdkDirectoryWorkloadManifestProvider.WorkloadSetsFolderName);

            //foreach ((string workloadSetVersion, _) in installedWorkloadSets)
            //{
            //    if (workloadSetsToKeep.Contains(workloadSetVersion))
            //    {
            //        //  Don't uninstall this workload set
            //        continue;
            //    }

            //    string workloadSetDirectory = Path.Combine(workloadSetsDirectory, workloadSetVersion);
            //    if (Directory.Exists(workloadSetDirectory))
            //    {
            //        //  If the directory doesn't exist, the workload set is probably from a directory specified via the DOTNETSDK_WORKLOAD_MANIFEST_ROOTS environment variable
            //        //  In that case just ignore it, as the CLI doesn't manage that install
            //        Directory.Delete(workloadSetDirectory, true);
            //    }
            //}

        }

        void GarbageCollectWorkloadManifests()
        {
            //  Determine which workload manifests to garbage collect
            //  This will be within the scope of a feature band.
            //  The IInstaller implementation will be responsible for actually uninstalling the workload manifests, and it will use feature-band based ref counts to do this, the same way it does for workload packs
            //  So if this method determines that a workload manifest should be garbage collected, but another feature band still depends on it, then the installer would remove the ref count for the current
            //  feature band, but not actually uninstall the manifest.

            //  Iterate through all installed workload sets for this SDK feature band that have not been marked for garbage collection
            //  For each manifest version listed in a workload set, add it to a list to keep
            //  Also look in any rollback state file (default.json) in workload install state folder, and if it lists any manifests add them to the list of manifests to keep
            //  If the current SDK feature band doesn't have a workload set installed or a rollback state file, add the latest installed version of each manifest to the list of manifests to keep
            //  Baseline workload sets should not be garbage collected, so this should also prevent baseline manifest versions from being garbage collected (once we start including the baseline workload sets)

            //  IInstaller implementation should uninstall all manifests that are not in the list to keep (possibly only if they are using the side-by-side folder layout)



        }
    }
}
