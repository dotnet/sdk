// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Versioning;
using static Microsoft.DotNet.Workloads.Workload.Install.WorkloadManifestUpdater;
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
            var resolvedReporter = _printDownloadLinkOnly ? NullReporter.Instance : Reporter;

            _workloadInstaller = _workloadInstallerFromConstructor ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(resolvedReporter, _sdkFeatureBand,
                                     _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader, _dotnetPath, TempDirectoryPath,
                                     _packageSourceLocation, RestoreActionConfiguration, elevationRequired: !_printDownloadLinkOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));

            _workloadManifestUpdater = _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(resolvedReporter, _workloadResolver, PackageDownloader, _userProfileDir,
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), _workloadInstaller, _packageSourceLocation, displayManifestUpdates: Verbosity.IsDetailedOrDiagnostic());

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
                var manifestRollbacks = usedRollback ? _workloadManifestUpdater.ParseRollbackDefinitionFile(_fromRollbackDefinition, _sdkFeatureBand) : null;

                if (usedRollback)
                {
                    var rollbackContents = new Dictionary<string, string>();
                    foreach (var rollback in manifestRollbacks)
                    {
                        rollbackContents[rollback.id.ToString()] = $"{rollback.ManifestWithBand.Version}/{rollback.ManifestWithBand.Band}";
                    }

                    recorder.HistoryRecord.RollbackFileContents = rollbackContents;
                }

                if (_printDownloadLinkOnly)
                {
                    var packageDownloader = IsPackageDownloaderProvided ? PackageDownloader : new NuGetPackageDownloader(
                    TempPackagesDirectory,
                    filePermissionSetter: null,
                    new FirstPartyNuGetPackageSigningVerifier(),
                    new NullLogger(),
                    NullReporter.Instance,
                    restoreActionConfig: RestoreActionConfiguration,
                    verifySignatures: VerifySignatures);

                    //  Take the union of the currently installed workloads and the ones that are being requested.  This is so that if there are updates to the manifests
                    //  which require new packs for currently installed workloads, those packs will be downloaded.
                    //  If the packs are already installed, they won't be included in the results
                    var existingWorkloads = GetInstalledWorkloads(false);
                    var workloadsToDownload = existingWorkloads.Union(_workloadIds.Select(id => new WorkloadId(id))).ToList();

                    var packageUrls = GetPackageDownloadUrlsAsync(workloadsToDownload, _skipManifestUpdate, _includePreviews, NullReporter.Instance, packageDownloader).GetAwaiter().GetResult();

                    Reporter.WriteLine(JsonSerializer.Serialize(packageUrls, new JsonSerializerOptions() { WriteIndented = true }));
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

                        recorder.Run(() => DownloadToOfflineCacheAsync(workloadsToDownload, new DirectoryPath(_downloadToCacheOption), _skipManifestUpdate, _includePreviews).Wait());
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
                            InstallWorkloads(
                                _workloadIds.Select(id => new WorkloadId(id)),
                                _skipManifestUpdate,
                                _includePreviews,
                                string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption),
                                manifestRollbacks);
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
            IEnumerable<(ManifestId, ManifestVersionWithBand)> rollbackContents = null)
        {
            Reporter.WriteLine();

            var manifestsToUpdate = Enumerable.Empty<ManifestVersionUpdate> ();
            var useRollback = false;

            if (!skipManifestUpdate)
            {
                var installStateFilePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(_sdkFeatureBand, _dotnetPath), "default.json");
                if (File.Exists(installStateFilePath))
                {
                    //  If there is a rollback state file, then we don't want to automatically update workloads when a workload is installed
                    //  To update to a new version, the user would need to run "dotnet workload update"
                    skipManifestUpdate = true;
                }
            }

            if (!skipManifestUpdate)
            {
                if (Verbosity != VerbosityOptions.quiet && Verbosity != VerbosityOptions.q)
                {
                    Reporter.WriteLine(LocalizableStrings.CheckForUpdatedWorkloadManifests);
                }
                // Update currently installed workloads
                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);
                var previouslyInstalledWorkloads = installedWorkloads.Intersect(workloadIds);
                if (previouslyInstalledWorkloads.Any())
                {
                    Reporter.WriteLine(string.Format(LocalizableStrings.WorkloadAlreadyInstalled, string.Join(" ", previouslyInstalledWorkloads)).Yellow());
                }
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();

                useRollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();
                manifestsToUpdate = useRollback ?
                    _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition, rollbackContents) :
                    _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.ManifestUpdate);
            }

            InstallWorkloadsWithInstallRecord(_workloadInstaller, workloadIds, _sdkFeatureBand, manifestsToUpdate, offlineCache, useRollback);

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

        private void InstallWorkloadsWithInstallRecord(
            IInstaller installer,
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<ManifestVersionUpdate> manifestsToUpdate,
            DirectoryPath? offlineCache,
            bool usingRollback)
        {
            IEnumerable<PackInfo> workloadPackToInstall = new List<PackInfo>();
            IEnumerable<WorkloadId> newWorkloadInstallRecords = new List<WorkloadId>();

            var transaction = new CliTransaction
            {
                RollbackStarted = () => Reporter.WriteLine(LocalizableStrings.RollingBackInstall),
                // Don't hide the original error if roll back fails, but do log the rollback failure
                RollbackFailed = ex => Reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, ex.Message))
            };

            transaction.Run(
                action: context =>
                {
                    bool rollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

                    foreach (var manifestUpdate in manifestsToUpdate)
                    {
                        installer.InstallWorkloadManifest(manifestUpdate, context, offlineCache, rollback);
                    }

                    _workloadResolver.RefreshWorkloadManifests();

                    installer.InstallWorkloads(workloadIds, sdkFeatureBand, context, offlineCache);

                    var recordRepo = installer.GetWorkloadInstallationRecordRepository();
                    newWorkloadInstallRecords = workloadIds.Except(recordRepo.GetInstalledWorkloads(sdkFeatureBand));
                    foreach (var workloadId in newWorkloadInstallRecords)
                    {
                        recordRepo.WriteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
                    }

                    if (usingRollback)
                    {
                        UpdateInstallState(true, manifestsToUpdate);
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
                });
        }

        private async Task<IEnumerable<string>> GetPackageDownloadUrlsAsync(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate, bool includePreview,
            IReporter reporter = null, INuGetPackageDownloader packageDownloader = null)
        {
            reporter ??= Reporter;
            packageDownloader ??= PackageDownloader;
            var downloads = await GetDownloads(workloadIds, skipManifestUpdate, includePreview, reporter: reporter, packageDownloader: packageDownloader);

            var urls = new List<string>();
            foreach (var download in downloads)
            {
                urls.Add(await packageDownloader.GetPackageUrl(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion), _packageSourceLocation));
            }

            return urls;
        }

        private Task DownloadToOfflineCacheAsync(IEnumerable<WorkloadId> workloadIds, DirectoryPath offlineCache, bool skipManifestUpdate, bool includePreviews)
        {
            return GetDownloads(workloadIds, skipManifestUpdate, includePreviews, offlineCache.Value);
        }
    }
}
