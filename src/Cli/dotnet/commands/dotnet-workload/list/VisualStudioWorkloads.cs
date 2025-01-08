// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.VisualStudio.Setup.Configuration;

namespace Microsoft.DotNet.Workloads.Workload
{
    /// <summary>
    /// Provides functionality to query the status of .NET workloads in Visual Studio.    
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    internal static class VisualStudioWorkloads
    {
        private static readonly object s_guard = new();

        private const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);

        /// <summary>
        /// Visual Studio product ID filters. We dont' want to query SKUs such as Server, TeamExplorer, TestAgent
        /// TestController and BuildTools.
        /// </summary>
        private static readonly string[] s_visualStudioProducts = new string[]
        {
            "Microsoft.VisualStudio.Product.Community",
            "Microsoft.VisualStudio.Product.Professional",
            "Microsoft.VisualStudio.Product.Enterprise",
        };

        /// <summary>
        /// Default prefix to use for Visual Studio component and component group IDs.
        /// </summary>
        private static readonly string s_visualStudioComponentPrefix = "Microsoft.NET.Component";

        /// <summary>
        /// Well-known prefixes used by some workloads that can be replaced when generating component IDs.
        /// </summary>
        private static readonly string[] s_wellKnownWorkloadPrefixes = { "Microsoft.NET.", "Microsoft." };

        /// <summary>
        /// The SWIX package ID wrapping the SDK installer in Visual Studio. The ID should contain
        /// the SDK version as a suffix, e.g., "Microsoft.NetCore.Toolset.5.0.403".
        /// </summary>
        private static readonly string s_visualStudioSdkPackageIdPrefix = "Microsoft.NetCore.Toolset.";

        /// <summary>
        /// Gets a dictionary of mapping possible Visual Studio component IDs to .NET workload IDs in the current SDK.
        /// </summary>
        /// <param name="workloadResolver">The workload resolver used to obtain available workloads.</param>
        /// <returns>A dictionary of Visual Studio component IDs corresponding to workload IDs.</returns>
        internal static Dictionary<string, string> GetAvailableVisualStudioWorkloads(IWorkloadResolver workloadResolver)
        {
            Dictionary<string, string> visualStudioComponentWorkloads = new(StringComparer.OrdinalIgnoreCase);

            // Iterate through all the available workload IDs and generate potential Visual Studio
            // component IDs that map back to the original workload ID. This ensures that we
            // can do reverse lookups for special cases where a workload ID contains a prefix
            // corresponding with the full VS component ID prefix. For example,
            // Microsoft.NET.Component.runtime.android would be a valid component ID for both
            // microsoft-net-runtime-android and runtime-android.
            foreach (var workload in workloadResolver.GetAvailableWorkloads())
            {
                string workloadId = workload.Id.ToString();
                // Old style VS components simply replaced '-' with '.' in the workload ID.
                string componentId = workload.Id.ToString().Replace('-', '.');

                visualStudioComponentWorkloads.Add(componentId, workloadId);

                // Starting in .NET 9.0 and VS 17.12, workload components will follow the VS naming convention.
                foreach (string wellKnownPrefix in s_wellKnownWorkloadPrefixes)
                {
                    if (componentId.StartsWith(wellKnownPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        componentId = componentId.Substring(wellKnownPrefix.Length);
                        break;
                    }
                }

                componentId = s_visualStudioComponentPrefix + "." + componentId;
                visualStudioComponentWorkloads.Add(componentId, workloadId);
            }

            return visualStudioComponentWorkloads;
        }

        /// <summary>
        /// Finds all workloads installed by all Visual Studio instances given that the
        /// SDK installed by an instance matches the feature band of the currently executing SDK.
        /// </summary>
        /// <param name="workloadResolver">The workload resolver used to obtain available workloads.</param>
        /// <param name="installedWorkloads">The collection of installed workloads to update.</param>
        /// <param name="sdkFeatureBand">The feature band of the executing SDK.
        /// If null, then workloads from all feature bands in VS will be returned.
        /// </param>
        internal static void GetInstalledWorkloads(IWorkloadResolver workloadResolver,
            InstalledWorkloadsCollection installedWorkloads, SdkFeatureBand? sdkFeatureBand = null)
        {
            Dictionary<string, string> visualStudioWorkloadIds = GetAvailableVisualStudioWorkloads(workloadResolver);
            HashSet<string> installedWorkloadComponents = new();

            // Visual Studio instances contain a large set of packages and we have to perform a linear
            // search to determine whether a matching SDK was installed and look for each installable
            // workload from the SDK. The search is optimized to only scan each set of packages once.
            foreach (ISetupInstance2 instance in GetVisualStudioInstances())
            {
                ISetupPackageReference[] packages = instance.GetPackages();
                bool hasMatchingSdk = false;
                installedWorkloadComponents.Clear();

                for (int i = 0; i < packages.Length; i++)
                {
                    string packageId = packages[i].GetId();

                    if (string.IsNullOrWhiteSpace(packageId))
                    {
                        // Visual Studio already verifies the setup catalog at build time. If the package ID is empty
                        // the catalog is likely corrupted.
                        continue;
                    }

                    if (packageId.StartsWith(s_visualStudioSdkPackageIdPrefix)) // Check if the package owning SDK is installed via VS. Note: if a user checks to add a workload in VS but does not install the SDK, this will cause those workloads to be ignored.
                    {
                        // After trimming the package prefix we should be left with a valid semantic version. If we can't
                        // parse the version we'll skip this instance.
                        if (!ReleaseVersion.TryParse(packageId.Substring(s_visualStudioSdkPackageIdPrefix.Length),
                            out ReleaseVersion visualStudioSdkVersion))
                        {
                            break;
                        }

                        SdkFeatureBand visualStudioSdkFeatureBand = new(visualStudioSdkVersion);

                        // The feature band of the SDK in VS must match that of the SDK on which we're running.
                        if (sdkFeatureBand != null && !visualStudioSdkFeatureBand.Equals(sdkFeatureBand))
                        {
                            break;
                        }

                        hasMatchingSdk = true;

                        continue;
                    }

                    if (visualStudioWorkloadIds.TryGetValue(packageId, out string workloadId))
                    {
                        installedWorkloadComponents.Add(workloadId);
                    }
                }

                if (hasMatchingSdk)
                {
                    foreach (string id in installedWorkloadComponents)
                    {
                        installedWorkloads.Add(id, $"VS {instance.GetInstallationVersion()}");
                    }
                }
            }
        }

        /// <summary>
        /// Writes install records for VS Workloads so we later install the packs via the CLI for workloads managed by VS.
        /// This is to fix a bug where updating the manifests in the CLI will cause VS to also be told to use these newer workloads via the workload resolver.
        /// ...  but these workloads don't have their corresponding packs installed as VS doesn't update its workloads as the CLI does.
        /// </summary>
        /// <returns>Updated list of workloads including any that may have had new install records written</returns>
        internal static IEnumerable<WorkloadId> WriteSDKInstallRecordsForVSWorkloads(IInstaller workloadInstaller, IWorkloadResolver workloadResolver,
            IEnumerable<WorkloadId> workloadsWithExistingInstallRecords, IReporter reporter)
        {
            // Do this check to avoid adding an unused & unnecessary method to FileBasedInstallers
            if (OperatingSystem.IsWindows() && workloadInstaller is NetSdkMsiInstallerClient)
            {
                InstalledWorkloadsCollection vsWorkloads = new();
                GetInstalledWorkloads(workloadResolver, vsWorkloads);

                // Remove VS workloads with an SDK installation record, as we've already created the records for them, and don't need to again.
                var vsWorkloadsAsWorkloadIds = vsWorkloads.AsEnumerable().Select(w => new WorkloadId(w.Key));
                var workloadsToWriteRecordsFor = vsWorkloadsAsWorkloadIds.Except(workloadsWithExistingInstallRecords);

                if (workloadsToWriteRecordsFor.Any())
                {
                    reporter.WriteLine(
                        string.Format(LocalizableStrings.WriteCLIRecordForVisualStudioWorkloadMessage,
                        string.Join(", ", workloadsToWriteRecordsFor.Select(w => w.ToString()).ToArray()))
                    );

                    ((NetSdkMsiInstallerClient)workloadInstaller).WriteWorkloadInstallRecords(workloadsToWriteRecordsFor);

                    return workloadsWithExistingInstallRecords.Concat(workloadsToWriteRecordsFor).ToList();
                }
            }

            return workloadsWithExistingInstallRecords;

        }

        /// <summary>
        /// Gets a list of all Visual Studio instances.
        /// </summary>
        /// <returns>A list of Visual Studio instances.</returns>
        private static List<ISetupInstance> GetVisualStudioInstances()
        {
            // The underlying COM API has a bug where-by it's not safe for concurrent calls. Until their
            // bug fix is rolled out use a lock to ensure we don't concurrently access this API.
            // https://dev.azure.com/devdiv/DevDiv/_workitems/edit/2241752/
            lock (s_guard)
            {
                List<ISetupInstance> vsInstances = new();

                try
                {
                    SetupConfiguration setupConfiguration = new();
                    ISetupConfiguration2 setupConfiguration2 = setupConfiguration;
                    IEnumSetupInstances setupInstances = setupConfiguration2.EnumInstances();
                    ISetupInstance[] instances = new ISetupInstance[1];
                    int fetched = 0;

                    do
                    {
                        setupInstances.Next(1, instances, out fetched);

                        if (fetched > 0)
                        {
                            ISetupInstance2 instance = (ISetupInstance2)instances[0];

                            // .NET Workloads only shipped in 17.0 and later and we should only look at IDE based SKUs
                            // such as community, professional, and enterprise.
                            if (Version.TryParse(instance.GetInstallationVersion(), out Version version) &&
                                version.Major >= 17 &&
                                s_visualStudioProducts.Contains(instance.GetProduct().GetId()))
                            {
                                vsInstances.Add(instances[0]);
                            }
                        }
                    }
                    while (fetched > 0);

                }
                catch (COMException e) when (e.ErrorCode == REGDB_E_CLASSNOTREG)
                {
                    // Query API not registered, good indication there are no VS installations of 15.0 or later.
                    // Other exceptions are passed through since that likely points to a real error.
                }

                return vsInstances;
            }
        }
    }
}
