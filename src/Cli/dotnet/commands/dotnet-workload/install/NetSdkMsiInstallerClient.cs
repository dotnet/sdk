// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.History;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32.Msi;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    [SupportedOSPlatform("windows")]
    internal partial class NetSdkMsiInstallerClient : MsiInstallerBase, IInstaller
    {
        private INuGetPackageDownloader _nugetPackageDownloader;

        private SdkFeatureBand _sdkFeatureBand;

        private IWorkloadResolver _workloadResolver;

        private bool _shutdown;

        private readonly PackageSourceLocation _packageSourceLocation;

        private readonly string _dependent;

        public int ExitCode => Restart ? unchecked((int)Error.SUCCESS_REBOOT_REQUIRED) : unchecked((int)Error.SUCCESS);

        public NetSdkMsiInstallerClient(InstallElevationContextBase elevationContext,
            ISetupLogger logger,
            bool verifySignatures,
            IWorkloadResolver workloadResolver,
            SdkFeatureBand sdkFeatureBand,
            INuGetPackageDownloader nugetPackageDownloader = null,
            VerbosityOptions verbosity = VerbosityOptions.normal,
            PackageSourceLocation packageSourceLocation = null,
            IReporter reporter = null) : base(elevationContext, logger, verifySignatures, reporter)
        {
            _packageSourceLocation = packageSourceLocation;
            _nugetPackageDownloader = nugetPackageDownloader;
            _sdkFeatureBand = sdkFeatureBand;
            _workloadResolver = workloadResolver;
            _dependent = $"{DependentPrefix},{sdkFeatureBand},{HostArchitecture}";

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            Log?.LogMessage($"Executing: {Microsoft.DotNet.Cli.Utils.Windows.GetProcessCommandLine()}, PID: {CurrentProcess.Id}, PPID: {ParentProcess.Id}");
            Log?.LogMessage($"{nameof(IsElevated)}: {IsElevated}");
            Log?.LogMessage($"{nameof(Is64BitProcess)}: {Is64BitProcess}");
            Log?.LogMessage($"{nameof(RebootPending)}: {RebootPending}");
            Log?.LogMessage($"{nameof(ProcessorArchitecture)}: {ProcessorArchitecture}");
            Log?.LogMessage($"{nameof(HostArchitecture)}: {HostArchitecture}");
            Log?.LogMessage($"{nameof(SdkDirectory)}: {SdkDirectory}");
            Log?.LogMessage($"{nameof(VerifySignatures)}: {VerifySignatures}");
            Log?.LogMessage($"SDK feature band: {_sdkFeatureBand}");

            if (IsElevated)
            {
                // Turn off automatic updates. We don't want MU to potentially patch the SDK
                // and it also reduces the risk of hitting ERROR_INSTALL_ALREADY_RUNNING.
                UpdateAgent.Stop();
            }
        }

        public void ReplaceWorkloadResolver(IWorkloadResolver workloadResolver)
        {
            _workloadResolver = workloadResolver;
        }

        private IEnumerable<(WorkloadPackId Id, string Version)> GetInstalledPacks(SdkFeatureBand sdkFeatureBand)
        {
            string dependent = $"{DependentPrefix},{sdkFeatureBand},{HostArchitecture}";

            return GetWorkloadPackRecords()
                .Where(packRecord => new DependencyProvider(packRecord.ProviderKeyName).Dependents.Contains(dependent))
                .SelectMany(packRecord => packRecord.InstalledPacks)
                .Select(p => (p.id, p.version.ToString()));
        }

        public IEnumerable<WorkloadDownload> GetDownloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, bool includeInstalledItems)
        {
            IEnumerable<WorkloadDownload> msis = GetMsisForWorkloads(workloadIds);
            if (!includeInstalledItems)
            {
                HashSet<(string id, string version)> installedItems = new(GetInstalledPacks(sdkFeatureBand).Select(t => (t.Id.ToString(), t.Version)));
                msis = msis.Where(m => !installedItems.Contains((m.Id, m.NuGetPackageVersion)));
            }

            return msis.ToList(); ;
        }

        //  Wrap the setup logger in an IReporter so it can be passed to the garbage collector
        private class SetupLogReporter : IReporter
        {
            private ISetupLogger _setupLogger;

            public SetupLogReporter(ISetupLogger setupLogger)
            {
                _setupLogger = setupLogger;
            }

            //  SetupLogger doesn't have a way of writing a message that shouldn't include a newline.  So if this method is used a message may be split across multiple lines,
            //  but that's probably better than not writing a message at all or throwing an exception
            public void Write(string message) => _setupLogger.LogMessage(message);
            public void WriteLine(string message) => _setupLogger.LogMessage(message);
            public void WriteLine() => _setupLogger.LogMessage("");
            public void WriteLine(string format, params object[] args) => _setupLogger.LogMessage(string.Format(format, args));
        }

        /// <summary>
        /// Cleans up and removes stale workload packs.
        /// </summary>
        public void GarbageCollect(Func<string, IWorkloadResolver> getResolverForWorkloadSet, DirectoryPath? offlineCache = null, bool cleanAllPacks = false)
        {
            try
            {
                ReportPendingReboot();
                Log?.LogMessage($"Starting garbage collection.");
                Log?.LogMessage($"Garbage Collection Mode: CleanAllPacks={cleanAllPacks}.");

                var globalJsonWorkloadSetVersions = GetGlobalJsonWorkloadSetVersions(_sdkFeatureBand);

                var garbageCollector = new WorkloadGarbageCollector(DotNetHome, _sdkFeatureBand, RecordRepository.GetInstalledWorkloads(_sdkFeatureBand),
                    getResolverForWorkloadSet, globalJsonWorkloadSetVersions, new SetupLogReporter(Log));
                garbageCollector.Collect();

                IEnumerable<SdkFeatureBand> installedFeatureBands = GetInstalledFeatureBands(Log);

                List<WorkloadSetRecord> workloadSetsToRemove = new();
                var installedWorkloadSets = GetWorkloadSetRecords();
                foreach (var workloadSetRecord in installedWorkloadSets)
                {
                    DependencyProvider depProvider = new DependencyProvider(workloadSetRecord.ProviderKeyName);

                    (bool shouldBeInstalled, string reason) ShouldBeInstalled(SdkFeatureBand dependentFeatureBand)
                    {
                        if (!installedFeatureBands.Contains(dependentFeatureBand))
                        {
                            return (false, $"SDK feature band {dependentFeatureBand} does not match any installed feature bands.");
                        }
                        else if (dependentFeatureBand.Equals(_sdkFeatureBand))
                        {
                            if (garbageCollector.WorkloadSetsToKeep.Contains(workloadSetRecord.WorkloadSetVersion))
                            {
                                return (true, $"the workload set is still needed for SDK feature band {dependentFeatureBand}.");
                            }
                            else
                            {
                                return (false, $"the workload set is no longer needed for SDK feature band {dependentFeatureBand}.");
                            }
                        }
                        else
                        {
                            return (true, $"the workload set may still be needed for SDK feature band {dependentFeatureBand}, which is different than the current SDK feature band of {_sdkFeatureBand}");
                        }
                    }

                    Log?.LogMessage($"Evaluating dependents for workload set, dependent: {depProvider}, Version: {workloadSetRecord.WorkloadSetVersion}, Feature Band: {workloadSetRecord.WorkloadSetFeatureBand}");
                    UpdateDependentReferenceCounts(depProvider, ShouldBeInstalled);

                    // Recheck the registry to see if there are any remaining dependents. If not, we can
                    // remove the workload set. We'll add it to the list and remove the installed workload set at the end.
                    IEnumerable<string> remainingDependents = depProvider.Dependents;

                    if (remainingDependents.Any())
                    {
                        Log?.LogMessage($"Workload set {workloadSetRecord.WorkloadSetVersion}/{workloadSetRecord.WorkloadSetFeatureBand} will not be removed because other dependents remain: {string.Join(", ", remainingDependents)}.");
                    }
                    else
                    {
                        workloadSetsToRemove.Add(workloadSetRecord);
                        Log?.LogMessage($"Removing workload set {workloadSetRecord.WorkloadSetVersion}/{workloadSetRecord.WorkloadSetFeatureBand} as no dependents remain.");
                    }
                }

                RemoveWorkloadSets(workloadSetsToRemove, offlineCache);

                List<WorkloadManifestRecord> manifestsToRemove = new();
                var installedWorkloadManifests = GetWorkloadManifestRecords();
                foreach (var manifestRecord in installedWorkloadManifests)
                {
                    DependencyProvider depProvider = new DependencyProvider(manifestRecord.ProviderKeyName);

                    (bool shouldBeInstalled, string reason) ShouldBeInstalled(SdkFeatureBand dependentFeatureBand)
                    {
                        if (!installedFeatureBands.Contains(dependentFeatureBand))
                        {
                            return (false, $"SDK feature band {dependentFeatureBand} does not match any installed feature bands.");
                        }
                        else if (dependentFeatureBand.Equals(_sdkFeatureBand))
                        {
                            if (garbageCollector.ManifestsToKeep.Contains((new ManifestId(manifestRecord.ManifestId), new ManifestVersion(manifestRecord.ManifestVersion), new SdkFeatureBand(manifestRecord.ManifestFeatureBand))))
                            {
                                return (true, $"the manifest is still needed for SDK feature band {dependentFeatureBand}.");
                            }
                            else
                            {
                                return (false, $"the manifest is no longer needed for SDK feature band {dependentFeatureBand}.");
                            }
                        }
                        else
                        {
                            return (true, $"the manifest may still be needed for SDK feature band {dependentFeatureBand}, which is different than the current SDK feature band of {_sdkFeatureBand}");
                        }
                    }

                    Log?.LogMessage($"Evaluating dependents for workload manifest, dependent: {depProvider}, ID: {manifestRecord.ManifestId}, Version: {manifestRecord.ManifestVersion}, Feature Band: {manifestRecord.ManifestFeatureBand}");
                    UpdateDependentReferenceCounts(depProvider, ShouldBeInstalled);

                    // Recheck the registry to see if there are any remaining dependents. If not, we can
                    // remove the workload manifest. We'll add it to the list and remove the packs at the end.
                    IEnumerable<string> remainingDependents = depProvider.Dependents;

                    if (remainingDependents.Any())
                    {
                        Log?.LogMessage($"{manifestRecord.ManifestId} {manifestRecord.ManifestVersion}/{manifestRecord.ManifestFeatureBand} will not be removed because other dependents remain: {string.Join(", ", remainingDependents)}.");
                    }
                    else
                    {
                        manifestsToRemove.Add(manifestRecord);
                        Log?.LogMessage($"Removing {manifestRecord.ManifestId} {manifestRecord.ManifestVersion}/{manifestRecord.ManifestFeatureBand} as no dependents remain.");
                    }

                }

                RemoveWorkloadManifests(manifestsToRemove, offlineCache);

                //  If aliased, the pack records here are the resolved pack from the alias
                IEnumerable<WorkloadPackRecord> installedWorkloadPacks = GetWorkloadPackRecords();

                List<WorkloadPackRecord> packsToRemove = new();

                // We first need to clean up the dependents and then do a pass at removing them. Querying the installed packs
                // is effectively a table scan of the registry to make sure we have accurate information and there's a
                // potential perf hit for both memory and speed when enumerating large sets of registry entries.
                foreach (WorkloadPackRecord packRecord in installedWorkloadPacks)
                {
                    DependencyProvider depProvider = new(packRecord.ProviderKeyName);

                    (bool shouldBeInstalled, string reason) ShouldBeInstalled(SdkFeatureBand dependentFeatureBand)
                    {
                        if (!installedFeatureBands.Contains(dependentFeatureBand))
                        {
                            return (false, $"SDK feature band {dependentFeatureBand} does not match any installed feature bands.");
                        }
                        else if (cleanAllPacks)
                        {
                            return (false, "dotnet has been told to clean everything.");
                        }
                        else if (dependentFeatureBand.Equals(_sdkFeatureBand))
                        {
                            // If the current SDK feature band is listed as a dependent, we can validate
                            // the workload packs against the expected pack IDs and versions to potentially remove it.
                            if (packRecord.InstalledPacks.All(p => !garbageCollector.PacksToKeep.Contains((p.id, p.version.ToString()))))
                            {
                                //  None of the packs installed by this MSI are necessary any longer for this feature band, so we can remove the reference count
                                return (false, "the pack record(s) do not match any expected packs.");
                            }
                            else
                            {
                                return (true, $"the packs are still needed. Mode: {cleanAllPacks} | Dependent band: {dependentFeatureBand} | SDK band: {_sdkFeatureBand}.");
                            }
                        }
                        return (true, $"no conditions for removal were met. Mode: {cleanAllPacks} | Dependent band: {dependentFeatureBand} | SDK band: {_sdkFeatureBand}.");
                    }


                    Log?.LogMessage($"Evaluating dependents for workload pack, dependent: {depProvider}, MSI ID: {packRecord.MsiId}, MSI version: {packRecord.MsiNuGetVersion}");
                    UpdateDependentReferenceCounts(depProvider, ShouldBeInstalled);

                    // Recheck the registry to see if there are any remaining dependents. If not, we can
                    // remove the workload pack. We'll add it to the list and remove the packs at the end.
                    IEnumerable<string> remainingDependents = depProvider.Dependents;

                    if (remainingDependents.Any())
                    {
                        Log?.LogMessage($"{packRecord.MsiId} ({packRecord.MsiNuGetVersion}) will not be removed because other dependents remain: {string.Join(", ", remainingDependents)}.");
                    }
                    else
                    {
                        packsToRemove.Add(packRecord);
                        Log?.LogMessage($"Removing {packRecord.MsiId} ({packRecord.MsiNuGetVersion}) as no dependents remain.");
                    }
                }

                RemoveWorkloadPacks(packsToRemove, offlineCache);

                if (cleanAllPacks)
                {
                    DeleteAllWorkloadInstallationRecords();
                }
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public WorkloadSet InstallWorkloadSet(ITransactionContext context, string workloadSetVersion, DirectoryPath? offlineCache)
        {
            ReportPendingReboot();

            var (msi, msiPackageId, installationFolder) = GetWorkloadSetPayload(workloadSetVersion, offlineCache);

            context.Run(
                action: () =>
                {
                    DetectState state = DetectPackage(msi.ProductCode, out Version installedVersion);
                    InstallAction plannedAction = PlanPackage(msi, state, InstallAction.Install, installedVersion);

                    if (plannedAction == InstallAction.Install)
                    {
                        Elevate();

                        ExecutePackage(msi, plannedAction, msiPackageId);

                        // Update the reference count against the MSI.
                        UpdateDependent(InstallRequestType.AddDependent, msi.Manifest.ProviderKeyName, _dependent);
                    }
                },
                rollback: () =>
                {
                    DetectState state = DetectPackage(msi.ProductCode, out Version installedVersion);
                    InstallAction plannedAction = PlanPackage(msi, state, InstallAction.Uninstall, installedVersion);

                    if (plannedAction == InstallAction.Uninstall)
                    {
                        Elevate();

                        // Update the reference count against the MSI.
                        UpdateDependent(InstallRequestType.RemoveDependent, msi.Manifest.ProviderKeyName, _dependent);

                        ExecutePackage(msi, plannedAction, msiPackageId);
                    }
                });

            return WorkloadSet.FromWorkloadSetFolder(installationFolder, workloadSetVersion, _sdkFeatureBand);
        }

        (MsiPayload msi, string msiPackageId, string installationFolder) GetWorkloadSetPayload(string workloadSetVersion, DirectoryPath? offlineCache)
        {
            SdkFeatureBand workloadSetFeatureBand;
            string msiPackageVersion = WorkloadSetVersion.ToWorkloadSetPackageVersion(workloadSetVersion, out workloadSetFeatureBand);
            string msiPackageId = GetManifestPackageId(new ManifestId("Microsoft.NET.Workloads"), workloadSetFeatureBand).ToString();

            Log?.LogMessage($"Resolving Microsoft.NET.Workloads ({workloadSetVersion}) to {msiPackageId} ({msiPackageVersion}).");

            // Retrieve the payload from the MSI package cache.
            MsiPayload msi;
            try
            {
                msi = GetCachedMsiPayload(msiPackageId, msiPackageVersion, offlineCache);
            }
            //  Unwrap AggregateException caused by switch from async to sync
            catch (Exception ex) when (ex is NuGetPackageNotFoundException || ex.InnerException is NuGetPackageNotFoundException)
            {
                throw new GracefulException(string.Format(Update.LocalizableStrings.WorkloadVersionRequestedNotFound, workloadSetVersion), ex is NuGetPackageNotFoundException ? ex : ex.InnerException);
            }
            VerifyPackage(msi);

            string installationFolder = Path.Combine(DotNetHome, "sdk-manifests", workloadSetFeatureBand.ToString(), "workloadsets", workloadSetVersion);

            return (msi, msiPackageId, installationFolder);
        }

        /// <summary>
        /// Find all the dependents that look like they belong to SDKs. We only care
        /// about dependents that match the SDK host we're running under. For example, an x86 SDK should not be
        /// modifying the x64 MSI dependents. After this, decrement any dependents (registry keys) that should be removed.
        /// </summary>
        /// <param name="depProvider"></param>
        /// <param name="shouldBeInstalledFunc">Function to determine whether a dependency record (reference count) for a feature band should be kept</param>
        private void UpdateDependentReferenceCounts(
            DependencyProvider depProvider,
            Func<SdkFeatureBand, (bool shouldBeInstalled, string reason)> shouldBeInstalledFunc
            )
        {
            IEnumerable<string> sdkDependents = depProvider.Dependents
                .Where(d => d.StartsWith($"{DependentPrefix}"))
                .Where(d => d.EndsWith($",{HostArchitecture}"));

            foreach (string dependent in sdkDependents)
            {
                // Dependents created by the SDK should have 3 parts, for example, "Microsoft.NET.Sdk,6.0.100,x86".
                string[] dependentParts = dependent.Split(',');

                if (dependentParts.Length != 3)
                {
                    Log?.LogMessage($"Skipping dependent: {dependent}");
                    continue;
                }

                try
                {
                    SdkFeatureBand dependentFeatureBand = new(dependentParts[1]);

                    var (shouldBeInstalled, reason) = shouldBeInstalledFunc(dependentFeatureBand);
                    if (shouldBeInstalled)
                    {
                        Log?.LogMessage($"Dependent '{dependent}' was not removed because {reason}");
                    }
                    else
                    {
                        Log?.LogMessage($"Removing dependent '{dependent}' from provider key '{depProvider.ProviderKeyName}' because {reason}");
                        UpdateDependent(InstallRequestType.RemoveDependent, depProvider.ProviderKeyName, dependent);
                    }
                }
                catch (Exception e)
                {
                    Log?.LogMessage($"{e.Message}");
                    Log?.LogMessage($"{e.StackTrace}");
                    continue;
                }
            }
        }

        private void RemoveWorkloadSets(List<WorkloadSetRecord> workloadSetsToRemove, DirectoryPath? offlineCache)
        {
            foreach (WorkloadSetRecord record in workloadSetsToRemove)
            {
                DetectState state = DetectPackage(record.ProductCode, out Version _);
                if (state == DetectState.Present)
                {
                    string msiNuGetPackageId = $"Microsoft.NET.Workloads.{record.WorkloadSetFeatureBand}.Msi.{HostArchitecture}";
                    MsiPayload msi = GetCachedMsiPayload(msiNuGetPackageId, record.WorkloadSetPackageVersion, offlineCache);

                    if (!string.Equals(record.ProductCode, msi.ProductCode, StringComparison.OrdinalIgnoreCase))
                    {
                        Log?.LogMessage($"ProductCode mismatch! Cached package: {msi.ProductCode}, workload set record: {record.ProductCode}.");
                        string logFile = GetMsiLogName(record.ProductCode, InstallAction.Uninstall);
                        uint error = ExecuteWithProgress(String.Format(LocalizableStrings.MsiProgressUninstall, msiNuGetPackageId), () => UninstallMsi(record.ProductCode, logFile));
                        ExitOnError(error, $"Failed to uninstall {msi.MsiPath}.");
                    }
                    else
                    {
                        VerifyPackage(msi);
                        ExecutePackage(msi, InstallAction.Uninstall, msiNuGetPackageId);
                    }
                }
            }
        }

        private void RemoveWorkloadManifests(List<WorkloadManifestRecord> manifestToRemove, DirectoryPath? offlineCache)
        {
            foreach (WorkloadManifestRecord record in manifestToRemove)
            {
                DetectState state = DetectPackage(record.ProductCode, out Version _);
                if (state == DetectState.Present)
                {
                    string msiNuGetPackageId = $"{record.ManifestId}.Manifest-{record.ManifestFeatureBand}.Msi.{HostArchitecture}";
                    MsiPayload msi = GetCachedMsiPayload(msiNuGetPackageId, record.ManifestVersion, offlineCache);

                    if (!string.Equals(record.ProductCode, msi.ProductCode, StringComparison.OrdinalIgnoreCase))
                    {
                        Log?.LogMessage($"ProductCode mismatch! Cached package: {msi.ProductCode}, manifest record: {record.ProductCode}.");
                        string logFile = GetMsiLogName(record.ProductCode, InstallAction.Uninstall);
                        uint error = ExecuteWithProgress(String.Format(LocalizableStrings.MsiProgressUninstall, msiNuGetPackageId), () => UninstallMsi(record.ProductCode, logFile));
                        ExitOnError(error, $"Failed to uninstall {msi.MsiPath}.");
                    }
                    else
                    {
                        VerifyPackage(msi);
                        ExecutePackage(msi, InstallAction.Uninstall, msiNuGetPackageId);
                    }
                }
            }
        }

        private void RemoveWorkloadPacks(List<WorkloadPackRecord> packsToRemove, DirectoryPath? offlineCache)
        {
            foreach (WorkloadPackRecord record in packsToRemove)
            {
                // We need to make sure the product is actually installed and that we're not dealing with an orphaned record, e.g.
                // if a previous removal was interrupted. We can't safely clean up orphaned records because it's too expensive
                // to query all installed components and determine the product codes associated with the component that
                // created the record.
                DetectState state = DetectPackage(record.ProductCode, out Version _);

                if (state == DetectState.Present)
                {
                    // Manually construct the MSI payload package details
                    string id = $"{record.MsiId}.Msi.{HostArchitecture}";
                    MsiPayload msi = GetCachedMsiPayload(id, record.MsiNuGetVersion.ToString(), offlineCache);

                    // Make sure the package we have in the cache matches with the record. If it doesn't, we'll do the uninstall
                    // the hard way
                    if (!string.Equals(record.ProductCode, msi.ProductCode, StringComparison.OrdinalIgnoreCase))
                    {
                        Log?.LogMessage($"ProductCode mismatch! Cached package: {msi.ProductCode}, pack record: {record.ProductCode}.");
                        string logFile = GetMsiLogName(record, InstallAction.Uninstall);
                        uint error = ExecuteWithProgress(string.Format(LocalizableStrings.MsiProgressUninstall, id), () => UninstallMsi(record.ProductCode, logFile));
                        ExitOnError(error, $"Failed to uninstall {msi.MsiPath}.");
                    }
                    else
                    {
                        // No need to plan. We know that there are no other dependents, the MSI is installed and we
                        // want to remove it.
                        VerifyPackage(msi);
                        ExecutePackage(msi, InstallAction.Uninstall, id);
                    }
                }
            }
        }

        /// <summary>
        /// Remove all workload installation records that aren't from Visual Studio.
        /// </summary>
        private void DeleteAllWorkloadInstallationRecords()
        {
            var allFeatureBands = RecordRepository.GetFeatureBandsWithInstallationRecords();

            Log?.LogMessage($"Attempting to delete all workload msi installation records.");

            foreach (SdkFeatureBand potentialBandToClean in allFeatureBands)
            {
                Log?.LogMessage($"Detected band with installation record: '{potentialBandToClean}'.");

                var workloadInstallationRecordIds = RecordRepository.GetInstalledWorkloads(potentialBandToClean);
                foreach (WorkloadId workloadInstallationRecordId in workloadInstallationRecordIds)
                {
                    Log?.LogMessage($"Workload {workloadInstallationRecordId} for '{potentialBandToClean}' has been marked for deletion.");
                    RecordRepository.DeleteWorkloadInstallationRecord(workloadInstallationRecordId, potentialBandToClean);
                }

                Log?.LogMessage($"No more workloads detected in band: '{potentialBandToClean}'.");
            }
        }

        public IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository() => RecordRepository;

        public void InstallWorkloadManifest(ManifestVersionUpdate manifestUpdate, ITransactionContext transactionContext, DirectoryPath? offlineCache = null)
        {
            try
            {
                transactionContext.Run(
                    action: () =>
                    {
                        InstallWorkloadManifestImplementation(manifestUpdate, offlineCache);
                    },
                    rollback: () =>
                    {
                        InstallWorkloadManifestImplementation(manifestUpdate, offlineCache: null, action: InstallAction.Uninstall);
                    });
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        void InstallWorkloadManifestImplementation(ManifestVersionUpdate manifestUpdate, DirectoryPath? offlineCache = null, InstallAction action = InstallAction.Install)
        {
            ReportPendingReboot();

            // Rolling back a manifest update after a successful install is essentially a downgrade, which is blocked so we have to
            // treat it as a special case and is different from the install failing and rolling that back, though depending where the install
            // failed, it may have removed the old product already.
            Log?.LogMessage($"Installing manifest: Id: {manifestUpdate.ManifestId}, version: {manifestUpdate.NewVersion}, feature band: {manifestUpdate.NewFeatureBand}.");

            // Resolve the package ID for the manifest payload package
            string msiPackageId = GetManifestPackageId(manifestUpdate.ManifestId, new SdkFeatureBand(manifestUpdate.NewFeatureBand)).ToString();
            string msiPackageVersion = $"{manifestUpdate.NewVersion}";

            Log?.LogMessage($"Resolving {manifestUpdate.ManifestId} ({manifestUpdate.NewVersion}) to {msiPackageId} ({msiPackageVersion}).");

            // Retrieve the payload from the MSI package cache.
            MsiPayload msi = GetCachedMsiPayload(msiPackageId, msiPackageVersion, offlineCache);
            VerifyPackage(msi);
            DetectState state = DetectPackage(msi.ProductCode, out Version installedVersion);
            InstallAction plannedAction = PlanPackage(msi, state, action, installedVersion);

            ExecutePackage(msi, plannedAction, msiPackageId);

            // Update the reference count against the MSI.
            UpdateDependent(InstallRequestType.AddDependent, msi.Manifest.ProviderKeyName, _dependent);
        }

        public void RepairWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null)
        {
            try
            {
                ReportPendingReboot();

                foreach (var aquirableMsi in GetMsisForWorkloads(workloadIds))
                {
                    // Retrieve the payload from the MSI package cache.
                    MsiPayload msi = GetCachedMsiPayload(aquirableMsi.NuGetPackageId, aquirableMsi.NuGetPackageVersion, offlineCache);
                    VerifyPackage(msi);
                    DetectState state = DetectPackage(msi, out Version installedVersion);
                    InstallAction plannedAction = PlanPackage(msi, state, InstallAction.Repair, installedVersion);
                    ExecutePackage(msi, plannedAction, aquirableMsi.NuGetPackageId);

                    // Update the reference count against the MSI.
                    UpdateDependent(InstallRequestType.AddDependent, msi.Manifest.ProviderKeyName, _dependent);
                }
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, ITransactionContext transactionContext, DirectoryPath? offlineCache = null)
        {
            ReportPendingReboot();

            var msisToInstall = GetMsisForWorkloads(workloadIds);

            foreach (var msiToInstall in msisToInstall)
            {
                bool shouldRollBackPack = false;

                transactionContext.Run(action: () =>
                {
                    try
                    {
                        // Retrieve the payload from the MSI package cache.
                        MsiPayload msi = GetCachedMsiPayload(msiToInstall.NuGetPackageId, msiToInstall.NuGetPackageVersion, offlineCache);
                        VerifyPackage(msi);
                        DetectState state = DetectPackage(msi, out Version installedVersion);
                        InstallAction plannedAction = PlanPackage(msi, state, InstallAction.Install, installedVersion);
                        if (plannedAction == InstallAction.Install)
                        {
                            shouldRollBackPack = true;
                        }
                        ExecutePackage(msi, plannedAction, msiToInstall.NuGetPackageId);

                        // Update the reference count against the MSI.
                        UpdateDependent(InstallRequestType.AddDependent, msi.Manifest.ProviderKeyName, _dependent);
                    }
                    catch (Exception e)
                    {
                        LogException(e);
                        throw;
                    }
                },
                rollback: () =>
                {
                    if (shouldRollBackPack)
                    {
                        RollBackMsiInstall(msiToInstall);
                    }
                });
            }
        }

        public void WriteWorkloadInstallRecords(IEnumerable<WorkloadId> workloadsToWriteRecordsFor)
        {
            foreach (var workload in workloadsToWriteRecordsFor)
            {
                Log?.LogMessage($"The workload with id: {workload} was detected as being from VS only and having no SDK records. Creating one now under feature band {_sdkFeatureBand}.");
                RecordRepository.WriteWorkloadInstallationRecord(workload, _sdkFeatureBand);
            }
        }

        void RollBackMsiInstall(WorkloadDownload msiToRollback, DirectoryPath? offlineCache = null)
        {
            try
            {
                ReportPendingReboot();
                Log?.LogMessage($"Rolling back workload pack installation for {msiToRollback.NuGetPackageId}.");

                // Retrieve the payload from the MSI package cache.
                MsiPayload msi = GetCachedMsiPayload(msiToRollback.NuGetPackageId, msiToRollback.NuGetPackageVersion, offlineCache);
                VerifyPackage(msi);

                // Check the provider key first in case we were installed and we only need to remove
                // a dependent.
                DependencyProvider depProvider = new(msi.Manifest.ProviderKeyName);

                // Try and remove the dependent against this SDK. If any remain we'll simply exit.
                UpdateDependent(InstallRequestType.RemoveDependent, msi.Manifest.ProviderKeyName, _dependent);

                if (depProvider.Dependents.Any())
                {
                    Log?.LogMessage($"Cannot remove pack, other dependents remain: {string.Join(", ", depProvider.Dependents)}.");
                    return;
                }

                // Make sure the MSI is actually installed.
                DetectState state = DetectPackage(msi, out Version installedVersion);
                InstallAction plannedAction = PlanPackage(msi, state, InstallAction.Uninstall, installedVersion);

                // The previous steps would have logged the final action. If the verdict is not to uninstall we can exit.
                if (plannedAction == InstallAction.Uninstall)
                {
                    ExecutePackage(msi, plannedAction, msiToRollback.NuGetPackageId);
                }

                Log?.LogMessage("Rollback completed.");
            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public IEnumerable<WorkloadHistoryRecord> GetWorkloadHistoryRecords(string sdkFeatureBand)
        {
            return WorkloadFileBasedInstall.GetWorkloadHistoryRecords(GetWorkloadHistoryDirectory(sdkFeatureBand.ToString()));
        }

        public void Shutdown()
        {
            Log?.LogMessage("Shutting down");

            if (IsElevated)
            {
                UpdateAgent.Start();
            }
            else if (IsClient && Dispatcher != null && Dispatcher.IsConnected)
            {
                InstallResponseMessage response = Dispatcher.SendShutdownRequest();
            }

            Log?.LogMessage("Shutdown completed.");
            Log?.LogMessage($"Restart required: {Restart}");
            ((TimestampedFileLogger)Log).Dispose();
            _shutdown = true;
        }

        public PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand)
        {
            if (manifestId.ToString().Equals("Microsoft.NET.Workloads", StringComparison.OrdinalIgnoreCase))
            {
                return new PackageId($"{manifestId}.{featureBand}.Msi.{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}");
            }
            else
            {
                return new PackageId($"{manifestId}.Manifest-{featureBand}.Msi.{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}");
            }
        }

        private static object _msiAdminInstallLock = new();

        public async Task ExtractManifestAsync(string nupkgPath, string targetPath)
        {
            Log?.LogMessage($"ExtractManifestAsync: Extracting '{nupkgPath}' to '{targetPath}'");
            string extractionPath = PathUtilities.CreateTempSubdirectory();

            try
            {
                Log?.LogMessage($"ExtractManifestAsync: Temporary extraction path: '{extractionPath}'");
                await _nugetPackageDownloader.ExtractPackageAsync(nupkgPath, new DirectoryPath(extractionPath));
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                }

                string extractedManifestPath = Path.Combine(extractionPath, "data", "extractedManifest");
                if (Directory.Exists(extractedManifestPath))
                {
                    Log?.LogMessage($"ExtractManifestAsync: Copying manifest from '{extractionPath}' to '{targetPath}'");
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(extractedManifestPath, targetPath));
                }
                else
                {
                    string packageDataPath = Path.Combine(extractionPath, "data");
                    if (!Cache.TryGetMsiPathFromPackageData(packageDataPath, out string msiPath, out _))
                    {
                        throw new FileNotFoundException(string.Format(LocalizableStrings.ManifestMsiNotFoundInNuGetPackage, extractionPath));
                    }
                    string msiExtractionPath = Path.Combine(extractionPath, "msi");


                    lock (_msiAdminInstallLock)
                    {
                        string adminInstallLog = GetMsiLogNameForAdminInstall(msiPath);

                        Log?.LogMessage($"ExtractManifestAsync: Running admin install for '{msiExtractionPath}'.  Log file: '{adminInstallLog}'");

                        ConfigureInstall(adminInstallLog);

                        var result = WindowsInstaller.InstallProduct(msiPath, $"TARGETDIR={msiExtractionPath} ACTION=ADMIN");

                        if (result != Error.SUCCESS)
                        {
                            Log?.LogMessage($"ExtractManifestAsync: Admin install failed: {result}");
                            throw new GracefulException(string.Format(LocalizableStrings.FailedToExtractMsi, msiPath));
                        }
                    }

                    var manifestsFolder = Path.Combine(msiExtractionPath, "dotnet", "sdk-manifests");

                    string manifestFolder = null;
                    string manifestsFeatureBandFolder = Directory.GetDirectories(manifestsFolder).SingleOrDefault();
                    if (manifestsFeatureBandFolder != null)
                    {
                        manifestFolder = Directory.GetDirectories(manifestsFeatureBandFolder).SingleOrDefault();
                    }

                    if (manifestFolder == null)
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.ExpectedSingleManifest, nupkgPath));
                    }

                    FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(manifestFolder, targetPath));
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(extractionPath) && Directory.Exists(extractionPath))
                {
                    Directory.Delete(extractionPath, true);
                }
            }
        }

        private void LogPackInfo(PackInfo packInfo)
        {
            Log?.LogMessage($"{nameof(PackInfo)}: {nameof(packInfo.Id)}: {packInfo.Id}, {nameof(packInfo.Kind)}: {packInfo.Kind}, {nameof(packInfo.Version)}: {packInfo.Version}, {nameof(packInfo.ResolvedPackageId)}: {packInfo.ResolvedPackageId}");
        }

        /// <summary>
        /// Determines the state of the specified product.
        /// </summary>
        /// <param name="productCode">The product code of the MSI to detect.</param>
        /// <param name="installedVersion">If detected, contains the version of the installed MSI.</param>
        /// <returns>The detect state of the specified MSI.</returns>
        private DetectState DetectPackage(string productCode, out Version installedVersion)
        {
            installedVersion = default;
            uint error = WindowsInstaller.GetProductInfo(productCode, InstallProperty.VERSIONSTRING, out string versionValue);

            DetectState state = error == Error.SUCCESS ? DetectState.Present
                : (error == Error.UNKNOWN_PRODUCT) || (error == Error.UNKNOWN_PROPERTY) ? DetectState.Absent
                : DetectState.Unknown;

            ExitOnError(state == DetectState.Unknown, error, $"DetectPackage: Failed to detect MSI package, ProductCode: {productCode}.");

            if (state == DetectState.Present)
            {
                if (!Version.TryParse(versionValue, out installedVersion))
                {
                    Log?.LogMessage($"DetectPackage: Failed to parse version: {versionValue}.");
                }
            }

            Log?.LogMessage($"DetectPackage: ProductCode: {productCode}, version: {installedVersion?.ToString() ?? "n/a"}, state: {state}.");

            return state;
        }

        /// <summary>
        /// Determines the state of the specified product.
        /// </summary>
        /// <param name="msi">The MSI package to detect.</param>
        /// <param name="installedVersion">If detected, contains the version of the installed MSI.</param>
        /// <returns>The detect state of the specified MSI.</returns>
        private DetectState DetectPackage(MsiPayload msi, out Version installedVersion)
        {
            return DetectPackage(msi.ProductCode, out installedVersion);
        }

        /// <summary>
        /// Plans the specified MSI payload based on its state and the requested install action.
        /// </summary>
        /// <param name="msi">The MSI package to plan.</param>
        /// <param name="state">The detected state of the package.</param>
        /// <param name="requestedAction">The requested action to perform.</param>
        /// <returns>The action that will be performed.</returns>
        private InstallAction PlanPackage(MsiPayload msi, DetectState state, InstallAction requestedAction, Version installedVersion)
        {
            InstallAction plannedAction = InstallAction.None;

            Log?.LogMessage($"PlanPackage: Begin, name: {msi.Name}, version: {msi.ProductVersion}, state: {state}, installed version: {installedVersion?.ToString() ?? "n/a"}, requested: {requestedAction}.");

            // Manifest packages, workload packs, and workload sets should now always be SxS (ProductCode and Upgrade should be different for each version).
            if (state == DetectState.Present)
            {
                if (msi.ProductVersion < installedVersion)
                {
                    throw new WorkloadException($"PlanPackage: Downgrade detected, installed version: {installedVersion}, requested version: {msi.ProductVersion}.");
                }
                else if (msi.ProductVersion > installedVersion)
                {
                    throw new WorkloadException($"PlanPackage: Minor update detected, installed version: {installedVersion}, requested version: {msi.ProductVersion}.");
                }
                else
                {
                    // If the package is installed, then we can uninstall and repair it.
                    plannedAction = (requestedAction != InstallAction.Repair) && (requestedAction != InstallAction.Uninstall) ? InstallAction.None : requestedAction;
                }
            }
            else if (state == DetectState.Absent)
            {
                // If we're absent, convert repair to install or keep install.
                plannedAction = (requestedAction == InstallAction.Repair) ? InstallAction.Install
                    : (requestedAction == InstallAction.Install) ? InstallAction.Install
                    : InstallAction.None;
            }

            Log?.LogMessage($"PlanPackage: Completed, name: {msi.Name}, version: {msi.ProductVersion}, state: {state}, installed version: {installedVersion?.ToString() ?? "n/a"}, requested: {requestedAction}, planned: {plannedAction}.");

            return plannedAction;
        }

        /// <summary>
        /// Derives the MSI package ID from the specified pack information based on the bitness of
        /// the SDK host.
        /// </summary>
        /// <param name="packInfo">The pack information used to generate the package ID.</param>
        /// <returns>The ID of the NuGet package containing the MSI corresponding to the pack.</returns>
        private static string GetMsiPackageId(PackInfo packInfo)
        {
            return $"{packInfo.ResolvedPackageId}.Msi.{HostArchitecture}";
        }

        /// <summary>
        /// Extracts the MSI and JSON manifest using the NuGet package in the offline cache to a temporary
        /// folder or downloads a copy before extracting it.
        /// </summary>
        /// <param name="packageId">The ID of the package to download.</param>
        /// <param name="packageVersion">The version of the package to download.</param>
        /// <param name="offlineCache">The path of the offline package cache. If <see langword="null"/>, the package
        /// is downloaded.</param>
        /// <returns>The directory where the package was extracted.</returns>
        /// <exception cref="FileNotFoundException" />
        private string ExtractPackage(string packageId, string packageVersion, DirectoryPath? offlineCache)
        {
            string packagePath;

            if (offlineCache == null || !offlineCache.HasValue)
            {
                Reporter.WriteLine($"Downloading {packageId} ({packageVersion})");
                packagePath = _nugetPackageDownloader.DownloadPackageAsync(new PackageId(packageId), new NuGetVersion(packageVersion),
                    _packageSourceLocation).Result;
                Log?.LogMessage($"Downloaded {packageId} ({packageVersion}) to '{packagePath}");
            }
            else
            {
                packagePath = Path.Combine(offlineCache.Value.Value, $"{packageId}.{packageVersion}.nupkg");
                Log?.LogMessage($"Using offline cache, package: {packagePath}");
            }

            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException(string.Format(LocalizableStrings.CacheMissingPackage, packageId, packageVersion, offlineCache));
            }

            // Extract the contents to a random folder to avoid potential file injection/hijacking
            // shenanigans before moving it to the final cache directory.
            string extractionDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(extractionDirectory);
            Log?.LogMessage($"Extracting '{packageId}' to '{extractionDirectory}'");
            _ = _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(extractionDirectory)).Result;

            return extractionDirectory;
        }

        /// <summary>
        /// Gets a set of all the installed SDK feature bands.
        /// </summary>
        /// <returns>A List of all the installed SDK feature bands.</returns>
        private static IEnumerable<SdkFeatureBand> GetInstalledFeatureBands(ISetupLogger log = null)
        {
            HashSet<SdkFeatureBand> installedFeatureBands = new();
            foreach (string sdkVersion in GetInstalledSdkVersions())
            {
                try
                {
                    installedFeatureBands.Add(new SdkFeatureBand(sdkVersion));
                }
                catch (Exception e)
                {
                    log?.LogMessage($"Failed to map SDK version {sdkVersion} to a feature band. ({e.Message})");
                }
            }

            return installedFeatureBands;
        }

        /// <summary>
        /// Tries to retrieve the MSI payload for the specified package ID and version from
        /// the MSI package cache.
        /// </summary>
        /// <param name="packageId">The ID of the payload package.</param>
        /// <param name="packageVersion">The version of the payload package.</param>
        /// <param name="offlineCache">The path to the offline cache. When <see langword="null"/>, packages are downloaded using the
        /// existing package feeds.</param>
        /// <returns>The MSI payload or <see langword="null"/> if unsuccessful.</returns>
        private MsiPayload GetCachedMsiPayload(string packageId, string packageVersion, DirectoryPath? offlineCache)
        {
            if (!Cache.TryGetPayloadFromCache(packageId, packageVersion, out MsiPayload msiPayload))
            {
                // If it's not fully cached, download or copy if from the local cache and extract the payload package into a
                // temporary location and try to cache it again in the MSI cache. We DO NOT trust partially cached packages.
                string extractedPackageRootPath = ExtractPackage(packageId, packageVersion, offlineCache);
                string manifestPath = Path.Combine(extractedPackageRootPath, "data", "msi.json");
                Cache.CachePayload(packageId, packageVersion, manifestPath);
                Directory.Delete(extractedPackageRootPath, recursive: true);

                if (!Cache.TryGetPayloadFromCache(packageId, packageVersion, out msiPayload))
                {
                    ExitOnError(Error.FILE_NOT_FOUND, $"Failed to retrieve MSI payload from cache, Id: {packageId}, version: {packageVersion}.");
                }
            }

            return msiPayload;
        }

        /// <summary>
        /// Executes an MSI package. The type of execution is determined by the requested action.
        /// </summary>
        /// <param name="msi">The MSI package to execute.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="displayName">A friendly name to display to the user when reporting progress. If no value is provided, the MSI
        /// filename will be used.</param>
        private void ExecutePackage(MsiPayload msi, InstallAction action, string displayName = null)
        {
            uint error = Error.SUCCESS;
            string logFile = GetMsiLogName(msi, action);
            string name = string.IsNullOrWhiteSpace(displayName) ? msi.Payload : displayName;

            switch (action)
            {
                case InstallAction.Install:
                    error = ExecuteWithProgress(String.Format(LocalizableStrings.MsiProgressInstall, name), () => InstallMsi(msi.MsiPath, logFile));
                    ExitOnError(error, $"Failed to install {msi.Payload}.");
                    break;

                case InstallAction.Repair:
                    error = ExecuteWithProgress(string.Format(LocalizableStrings.MsiProgressRepair, name), () => RepairMsi(msi.ProductCode, logFile));
                    ExitOnError(error, $"Failed to repair {msi.Payload}.");
                    break;

                case InstallAction.Uninstall:
                    error = ExecuteWithProgress(string.Format(LocalizableStrings.MsiProgressUninstall, name), () => UninstallMsi(msi.ProductCode, logFile));
                    ExitOnError(error, $"Failed to remove {msi.Payload}.");
                    break;

                case InstallAction.None:
                default:
                    break;
            }
        }

        /// <summary>
        /// Executes the install delegate using a separate task while reporting progress on the current thread.
        /// </summary>
        /// <param name="progressLabel">A label to be written before writing progress information.</param>
        /// <param name="installDelegate">The function to execute.</param>
        private uint ExecuteWithProgress(string progressLabel, Func<uint> installDelegate)
        {
            uint error = Error.SUCCESS;

            Task<uint> installTask = Task.Run<uint>(installDelegate);
            Reporter.Write($"{progressLabel}...");

            // This is just simple progress, a.k.a., a series of dots. Ideally we need to wire up the external
            // UI handler. Since that potentially runs on the elevated server instance, we'd need to create
            // an additional thread for handling progress reports from the server.
            while (!installTask.IsCompleted)
            {
                Reporter.Write(".");
                Thread.Sleep(500);
            }

            if (installTask.IsFaulted)
            {
                Reporter.WriteLine(" Failed");
                throw installTask.Exception.InnerException;
            }

            error = installTask.Result;

            if (!Error.Success(error))
            {
                Reporter.WriteLine(" Failed");
            }
            else
            {
                Reporter.WriteLine($" Done");
            }

            return error;
        }

        /// <summary>
        /// Verifies that the <see cref="MsiPayload"/> refers to a valid Windows Installer package (MSI).
        /// </summary>
        /// <param name="msiPayload">The payload to verify.</param>
        private void VerifyPackage(MsiPayload msiPayload)
        {
            uint error = WindowsInstaller.VerifyPackage(msiPayload.MsiPath);
            ExitOnError(error, $"Failed to verify package: {msiPayload.MsiPath}.");
        }

        /// <summary>
        /// Creates a new <see cref="NetSdkMsiInstallerClient"/> instance. If the current host process is not elevated,
        /// the elevated server process will also be started by running an additional command.
        /// </summary>
        /// <param name="nugetPackageDownloader"></param>
        /// <param name="verbosity"></param>
        /// <param name="packageSourceLocation"></param>
        /// <returns></returns>
        public static NetSdkMsiInstallerClient Create(
            bool verifySignatures,
            SdkFeatureBand sdkFeatureBand,
            IWorkloadResolver workloadResolver,
            INuGetPackageDownloader nugetPackageDownloader = null,
            VerbosityOptions verbosity = VerbosityOptions.normal,
            PackageSourceLocation packageSourceLocation = null,
            IReporter reporter = null,
            string tempDirPath = null,
            RestoreActionConfig restoreActionConfig = null,
            bool shouldLog = true)
        {
            ISynchronizingLogger logger =
                shouldLog ? new TimestampedFileLogger(Path.Combine(Path.GetTempPath(), $"Microsoft.NET.Workload_{Environment.ProcessId}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log"))
                          : new NullInstallerLogger();
            InstallClientElevationContext elevationContext = new(logger);

            if (nugetPackageDownloader == null)
            {
                DirectoryPath tempPackagesDir = new(string.IsNullOrWhiteSpace(tempDirPath) ? PathUtilities.CreateTempSubdirectory() : tempDirPath);

                nugetPackageDownloader = new NuGetPackageDownloader(tempPackagesDir,
                    filePermissionSetter: null, new FirstPartyNuGetPackageSigningVerifier(),
                    new NullLogger(), restoreActionConfig: restoreActionConfig);
            }

            return new NetSdkMsiInstallerClient(elevationContext, logger, verifySignatures, workloadResolver, sdkFeatureBand, nugetPackageDownloader,
                verbosity, packageSourceLocation, reporter);
        }

        /// <summary>
        /// Reports any pending reboots.
        /// </summary>
        private void ReportPendingReboot()
        {
            if (RebootPending)
            {
                ReportOnce(AnsiExtensions.Yellow(LocalizableStrings.PendingReboot));
            }
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            if (!_shutdown)
            {
                try
                {
                    Shutdown();
                }
                catch (Exception ex)
                {
                    // Don't rethrow. We'll call ShutDown during abnormal termination when control is passing back to the host
                    // so there's nothing in the CLI that will catch the exception.
                    Log?.LogMessage($"OnProcessExit: Shutdown failed, {ex.Message}");
                }
                finally
                {
                    if (Log is IDisposable tfl)
                    {
                        tfl.Dispose();
                    }
                }
            }
        }

        void IInstaller.UpdateInstallMode(SdkFeatureBand sdkFeatureBand, bool? newMode)
        {
            UpdateInstallMode(sdkFeatureBand, newMode);
            string newModeString = newMode == null ? "<null>" : newMode.Value ? WorkloadConfigCommandParser.UpdateMode_WorkloadSet : WorkloadConfigCommandParser.UpdateMode_Manifests;
            Reporter.WriteLine(string.Format(LocalizableStrings.UpdatedWorkloadMode, newModeString));
        }
    }
}
