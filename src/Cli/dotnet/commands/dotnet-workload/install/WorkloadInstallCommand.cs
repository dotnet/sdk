// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Text.Json;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallCommand : InstallingWorkloadCommand
    {
        private readonly bool _skipManifestUpdate;
        private readonly IReadOnlyCollection<string> _workloadIds;

        public bool IsRunningRestore { get; set; }

        public WorkloadInstallCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolverFactory workloadResolverFactory = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            string tempDirPath = null,
            IReadOnlyCollection<string> workloadIds = null)
            : base(parseResult, reporter: reporter, workloadResolverFactory: workloadResolverFactory, workloadInstaller: workloadInstaller,
                  nugetPackageDownloader: nugetPackageDownloader, workloadManifestUpdater: workloadManifestUpdater,
                  tempDirPath: tempDirPath)
        {
            _skipManifestUpdate = parseResult.GetValue(WorkloadInstallCommandParser.SkipManifestUpdateOption);
            _workloadIds = workloadIds ?? parseResult.GetValue(WorkloadInstallCommandParser.WorkloadIdArgument).ToList().AsReadOnly();

            _workloadInstaller = _workloadInstallerFromConstructor ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, _sdkFeatureBand,
                                     _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader, _dotnetPath, TempDirectoryPath,
                                     _packageSourceLocation, RestoreActionConfiguration, elevationRequired: !_printDownloadLinkOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));

            _workloadManifestUpdater = _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(Reporter, _workloadResolver, PackageDownloader, _userProfileDir,
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), _workloadInstaller, _packageSourceLocation, displayManifestUpdates: Verbosity.IsDetailedOrDiagnostic());

            _workloadSetVersion = parseResult.GetValue(InstallingWorkloadCommandParser.WorkloadSetVersionOption);
            if (string.IsNullOrWhiteSpace(_workloadSetVersion))
            {
                // If the version of the workload set is currently pinned, treat it as if it were freshly pinned.
                var installStateContents = InstallStateContents.FromPath(Path.Combine(WorkloadInstallType.GetInstallStateFolder(_sdkFeatureBand, _dotnetPath), "default.json"));
                _workloadSetVersion = installStateContents.WorkloadVersion;
            }

            ValidateWorkloadIdsInput();
        }

        private void ValidateWorkloadIdsInput()
        {
            var availableWorkloads = _workloadResolver.GetAvailableWorkloads();
            foreach (var workloadId in _workloadIds)
            {
                if (!availableWorkloads.Select(workload => workload.Id.ToString()).Contains(workloadId))
                {
                    var exceptionMessage = _workloadResolver.IsPlatformIncompatibleWorkload(new WorkloadId(workloadId)) ?
                        LocalizableStrings.WorkloadNotSupportedOnPlatform : LocalizableStrings.WorkloadNotRecognized;

                    throw new GracefulException(string.Format(exceptionMessage, workloadId), isUserError: false);
                }
            }
        }

        public override int Execute()
        {
            bool usedRollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

            WorkloadHistoryRecorder recorder = new WorkloadHistoryRecorder(_workloadResolver, _workloadInstaller);
            recorder.HistoryRecord.CommandName = IsRunningRestore ? "restore" : "install";
            recorder.HistoryRecord.WorkloadArguments = _workloadIds.Select(id => id.ToString()).ToList();

            try
            {
                if (_printDownloadLinkOnly)
                {
                    Reporter.WriteLine(string.Format(LocalizableStrings.ResolvingPackageUrls, string.Join(", ", _workloadIds)));

                    //  Take the union of the currently installed workloads and the ones that are being requested.  This is so that if there are updates to the manifests
                    //  which require new packs for currently installed workloads, those packs will be downloaded.
                    //  If the packs are already installed, they won't be included in the results
                    var existingWorkloads = GetInstalledWorkloads(false);
                    var workloadsToDownload = existingWorkloads.Union(_workloadIds.Select(id => new WorkloadId(id))).ToList();

                    var packageUrls = GetPackageDownloadUrlsAsync(workloadsToDownload, _skipManifestUpdate, _includePreviews).GetAwaiter().GetResult();
                    PrintDownloadLink(packageUrls);
                }
                else if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
                {
                    try
                    {
                        //  Take the union of the currently installed workloads and the ones that are being requested.  This is so that if there are updates to the manifests
                        //  which require new packs for currently installed workloads, those packs will be downloaded.
                        //  If the packs are already installed, they won't be included in the results
                        var existingWorkloads = GetInstalledWorkloads(false);
                        var workloadsToDownload = existingWorkloads.Union(_workloadIds.Select(id => new WorkloadId(id))).ToList();

                        DownloadToOfflineCacheAsync(workloadsToDownload, new DirectoryPath(_downloadToCacheOption), _skipManifestUpdate, _includePreviews).Wait();
                    }
                    catch (Exception e)
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e, isUserError: false);
                    }
                }
                else if (_skipManifestUpdate && usedRollback)
                {
                    throw new GracefulException(string.Format(LocalizableStrings.CannotCombineSkipManifestAndRollback,
                        WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, InstallingWorkloadCommandParser.FromRollbackFileOption.Name,
                        WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, InstallingWorkloadCommandParser.FromRollbackFileOption.Name), isUserError: true);
                }
                else
                {
                    recorder.Run(() =>
                    {
                        try
                        {
                            DirectoryPath? offlineCache = string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption);
                            var workloadIds = _workloadIds.Select(id => new WorkloadId(id));
                            if (string.IsNullOrWhiteSpace(_workloadSetVersion))
                            {
                                InstallWorkloads(
                                    workloadIds,
                                    _skipManifestUpdate,
                                    _includePreviews,
                                    offlineCache,
                                    recorder);
                            }
                            else
                            {
                                RunInNewTransaction(context =>
                                {
                                    var manifests = HandleWorkloadUpdateFromVersion(context, offlineCache, recorder);
                                    InstallWorkloadsAndGarbageCollect(context, workloadIds, manifests, offlineCache, false);
                                });
                            }
                        }
                        catch (Exception e)
                        {
                            // Don't show entire stack trace
                            throw new GracefulException(string.Format(LocalizableStrings.WorkloadInstallationFailed, e.Message), e, isUserError: false);
                        }
                    });
                }
            }
            finally
            {
                _workloadInstaller.Shutdown();
            }
            
            return _workloadInstaller.ExitCode;
        }

        public void InstallWorkloads(
            IEnumerable<WorkloadId> workloadIds,
            bool skipManifestUpdate = false,
            bool includePreviews = false,
            DirectoryPath? offlineCache = null,
            WorkloadHistoryRecorder recorder = null)
        {
            Reporter.WriteLine();

            var manifestsToUpdate = Enumerable.Empty<ManifestVersionUpdate>();
            var useRollback = false;

            WriteSDKInstallRecordsForVSWorkloads();

            if (!skipManifestUpdate)
            {
                var installStateFilePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(_sdkFeatureBand, _dotnetPath), "default.json");
                if (string.IsNullOrWhiteSpace(_fromRollbackDefinition) && File.Exists(installStateFilePath) && InstallStateContents.FromString(File.ReadAllText(installStateFilePath)).Manifests is not null)
                {
                    //  If there is a rollback state file, then we don't want to automatically update workloads when a workload is installed
                    //  To update to a new version, the user would need to run "dotnet workload update"
                    skipManifestUpdate = true;
                }
            }

            RunInNewTransaction(context =>
            {
                if (!skipManifestUpdate)
                {
                    if (Verbosity != VerbosityOptions.quiet && Verbosity != VerbosityOptions.q)
                    {
                        Reporter.WriteLine(LocalizableStrings.CheckForUpdatedWorkloadManifests);
                    }
                    // Add workload Ids that already exist to our collection to later trigger an update in those installed workloads
                    var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);
                    var previouslyInstalledWorkloads = installedWorkloads.Intersect(workloadIds);
                    if (previouslyInstalledWorkloads.Any())
                    {
                        Reporter.WriteLine(string.Format(LocalizableStrings.WorkloadAlreadyInstalled, string.Join(" ", previouslyInstalledWorkloads)).Yellow());
                    }
                    workloadIds = workloadIds.Concat(installedWorkloads).Distinct();

                    var useWorkloadSets = ShouldUseWorkloadSetMode(_sdkFeatureBand, _dotnetPath);
                    useRollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

                    _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, useWorkloadSets, offlineCache).Wait();

                    if (useWorkloadSets)
                    {
                        manifestsToUpdate = InstallWorkloadSet(context);
                    }
                    else
                    {
                        manifestsToUpdate = useRollback ? _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition, recorder) :
                            _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.ManifestUpdate);
                    }
                }

                InstallWorkloadsAndGarbageCollect(context, workloadIds, manifestsToUpdate, offlineCache, useRollback);
            });
        }

        private void InstallWorkloadsAndGarbageCollect(ITransactionContext context, IEnumerable<WorkloadId> workloadIds, IEnumerable<ManifestVersionUpdate> manifestsToUpdate, DirectoryPath? offlineCache, bool useRollback)
        {
            InstallWorkloadsWithInstallRecord(context, _workloadInstaller, workloadIds, _sdkFeatureBand, manifestsToUpdate, offlineCache, useRollback);

            TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion), offlineCache);

            Reporter.WriteLine();
            Reporter.WriteLine(string.Format(LocalizableStrings.InstallationSucceeded, string.Join(" ", workloadIds)));
            Reporter.WriteLine();
        }

        internal static void TryRunGarbageCollection(IInstaller workloadInstaller, IReporter reporter, VerbosityOptions verbosity, Func<string, IWorkloadResolver> getResolverForWorkloadSet, DirectoryPath? offlineCache = null)
        {
            try
            {
                workloadInstaller.GarbageCollect(getResolverForWorkloadSet, offlineCache);
            }
            catch (Exception e)
            {
                // Garbage collection failed, warn user
                reporter.WriteLine(string.Format(LocalizableStrings.GarbageCollectionFailed,
                    verbosity.IsDetailedOrDiagnostic() ? e.ToString() : e.Message).Yellow());
            }
        }

        private void WriteSDKInstallRecordsForVSWorkloads()
        {
#if !DOT_NET_BUILD_FROM_SOURCE
            if (OperatingSystem.IsWindows())
            {
                // The 'workload restore' command relies on this happening through the existing chain of logic, if this is massively refactored please ensure this is called.
                VisualStudioWorkloads.WriteSDKInstallRecordsForVSWorkloads(_workloadInstaller, _workloadResolver, GetInstalledWorkloads(false), Reporter);
            }
#endif
        }

        private void InstallWorkloadsWithInstallRecord(
            ITransactionContext context,
            IInstaller installer,
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<ManifestVersionUpdate> manifestsToUpdate,
            DirectoryPath? offlineCache,
            bool usingRollback)
        {
            IEnumerable<PackInfo> workloadPackToInstall = new List<PackInfo>();
            IEnumerable<WorkloadId> newWorkloadInstallRecords = new List<WorkloadId>();

            context.Run(
                action: () =>
                {
                    bool rollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

                    foreach (var manifestUpdate in manifestsToUpdate)
                    {
                        installer.InstallWorkloadManifest(manifestUpdate, context, offlineCache, rollback);
                    }

                    if (usingRollback)
                    {
                        installer.SaveInstallStateManifestVersions(sdkFeatureBand, GetInstallStateContents(manifestsToUpdate));
                    }

                    installer.AdjustWorkloadSetInInstallState(sdkFeatureBand, string.IsNullOrWhiteSpace(_workloadSetVersion) ? null : _workloadSetVersion);

                    _workloadResolver.RefreshWorkloadManifests();

                    installer.InstallWorkloads(workloadIds, sdkFeatureBand, context, offlineCache);

                    var recordRepo = installer.GetWorkloadInstallationRecordRepository();
                    newWorkloadInstallRecords = workloadIds.Except(recordRepo.GetInstalledWorkloads(sdkFeatureBand));
                    foreach (var workloadId in newWorkloadInstallRecords)
                    {
                        recordRepo.WriteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
                    }
                },
                rollback: () =>
                {
                    //  InstallWorkloadManifest and InstallWorkloadPacks already handle rolling back their actions, so here we only
                    //  need to delete the installation records
                    foreach (var workloadId in newWorkloadInstallRecords)
                    {
                        installer.GetWorkloadInstallationRecordRepository()
                            .DeleteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
                    }

                    //  Refresh the workload manifests to make sure that the resolver has the updated state after the rollback
                    _workloadResolver.RefreshWorkloadManifests();
                });

        }

        private async Task<IEnumerable<string>> GetPackageDownloadUrlsAsync(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate, bool includePreview)
        {
            var downloads = await GetDownloads(workloadIds, skipManifestUpdate, includePreview);

            var urls = new List<string>();
            foreach (var download in downloads)
            {
                urls.Add(await PackageDownloader.GetPackageUrl(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion), _packageSourceLocation));
            }

            return urls;
        }

        private Task DownloadToOfflineCacheAsync(IEnumerable<WorkloadId> workloadIds, DirectoryPath offlineCache, bool skipManifestUpdate, bool includePreviews)
        {
            return GetDownloads(workloadIds, skipManifestUpdate, includePreviews, offlineCache.Value);
        }

        private void RunInNewTransaction(Action<ITransactionContext> a)
        {
            var transaction = new CliTransaction()
            {
                RollbackStarted = () => Reporter.WriteLine(LocalizableStrings.RollingBackInstall),
                // Don't hide the original error if roll back fails, but do log the rollback failure
                RollbackFailed = ex => Reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, ex.Message))
            };
            transaction.Run(context => a(context));
        }
    }
}
