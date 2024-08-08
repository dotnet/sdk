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
using System.Text;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallCommand : InstallingWorkloadCommand
    {
        private bool _skipManifestUpdate;
        private readonly IReadOnlyCollection<string> _workloadIds;

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
            if (_printDownloadLinkOnly)
            {
                ValidateWorkloadIdsInput();

                Reporter.WriteLine(string.Format(LocalizableStrings.ResolvingPackageUrls, string.Join(", ", _workloadIds)));

                //  Take the union of the currently installed workloads and the ones that are being requested.  This is so that if there are updates to the manifests
                //  which require new packs for currently installed workloads, those packs will be downloaded.
                //  If the packs are already installed, they won't be included in the results
                var existingWorkloads = GetInstalledWorkloads(false);
                var workloadsToDownload = existingWorkloads.Union(_workloadIds.Select(id => new WorkloadId(id))).ToList();

                var packageUrls = GetPackageDownloadUrlsAsync(workloadsToDownload, _skipManifestUpdate, _includePreviews).GetAwaiter().GetResult();

                Reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                Reporter.WriteLine(JsonSerializer.Serialize(packageUrls, new JsonSerializerOptions() { WriteIndented = true }));
                Reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
            }
            else if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
            {
                ValidateWorkloadIdsInput();

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
            else if (_skipManifestUpdate && SpecifiedWorkloadSetVersionOnCommandLine)
            {
                throw new GracefulException(string.Format(LocalizableStrings.CannotCombineSkipManifestAndVersion,
                    WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, InstallingWorkloadCommandParser.VersionOption.Name), isUserError: true);
            }
            else if (_skipManifestUpdate && SpecifiedWorkloadSetVersionInGlobalJson)
            {
                throw new GracefulException(string.Format(LocalizableStrings.CannotUseSkipManifestWithGlobalJsonWorkloadVersion,
                    WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, _globalJsonPath), isUserError: true);
            }
            else
            {
                try
                {
                    //  Normally we want to validate that the workload IDs specified were valid.  However, if there is a global.json file with a workload
                    //  set version specified, and we might install that workload version, then we don't do that check here, because we might not have the right
                    //  workload set installed yet, and trying to list the available workloads would throw an error
                    if (_skipManifestUpdate || string.IsNullOrEmpty(_workloadSetVersionFromGlobalJson))
                    {
                        ValidateWorkloadIdsInput();
                    }

                    Reporter.WriteLine();

                    DirectoryPath? offlineCache = string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption);
                    var workloadIds = _workloadIds.Select(id => new WorkloadId(id));

                    // Add workload Ids that already exist to our collection to later trigger an update in those installed workloads
                    var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);
                    var previouslyInstalledWorkloads = installedWorkloads.Intersect(workloadIds);
                    if (previouslyInstalledWorkloads.Any())
                    {
                        Reporter.WriteLine(string.Format(LocalizableStrings.WorkloadAlreadyInstalled, string.Join(" ", previouslyInstalledWorkloads)).Yellow());
                    }
                    workloadIds = workloadIds.Concat(installedWorkloads).Distinct();

                    if (!_skipManifestUpdate)
                    {
                        var installStateFilePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(_sdkFeatureBand, _dotnetPath), "default.json");
                        if (string.IsNullOrWhiteSpace(_fromRollbackDefinition) &&
                            !SpecifiedWorkloadSetVersionOnCommandLine &&
                            !SpecifiedWorkloadSetVersionInGlobalJson &&
                            InstallStateContents.FromPath(installStateFilePath) is InstallStateContents installState &&
                            (installState.Manifests != null || installState.WorkloadVersion != null))
                        {
                            //  If the workload version is pinned in the install state, then we don't want to automatically update workloads when a workload is installed
                            //  To update to a new version, the user would need to run "dotnet workload update"
                            _skipManifestUpdate = true;
                        }
                    }

                    RunInNewTransaction(context =>
                    {
                       if (!_skipManifestUpdate)
                       {
                            if (Verbosity != VerbosityOptions.quiet && Verbosity != VerbosityOptions.q)
                            {
                                Reporter.WriteLine(LocalizableStrings.CheckForUpdatedWorkloadManifests);
                            }
                            UpdateWorkloadManifests(context, offlineCache);
                        }

                        //   This depends on getting the available workloads, so it needs to run after manifests hae potentially been installed
                        workloadIds = WriteSDKInstallRecordsForVSWorkloads(workloadIds);

                        _workloadInstaller.InstallWorkloads(workloadIds, _sdkFeatureBand, context, offlineCache);

                        //  Write workload installation records
                        var recordRepo = _workloadInstaller.GetWorkloadInstallationRecordRepository();
                        var newWorkloadInstallRecords = workloadIds.Except(recordRepo.GetInstalledWorkloads(_sdkFeatureBand));
                        context.Run(
                            action: () =>
                            {
                                foreach (var workloadId in newWorkloadInstallRecords)
                                {
                                    recordRepo.WriteWorkloadInstallationRecord(workloadId, _sdkFeatureBand);
                                }
                            },
                            rollback: () =>
                            {
                                foreach (var workloadId in newWorkloadInstallRecords)
                                {
                                    recordRepo.DeleteWorkloadInstallationRecord(workloadId, _sdkFeatureBand);
                                }
                            });

                        TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion), offlineCache);

                        Reporter.WriteLine();
                        Reporter.WriteLine(string.Format(LocalizableStrings.InstallationSucceeded, string.Join(" ", newWorkloadInstallRecords)));
                        Reporter.WriteLine();

                    });
                }
                catch (Exception e)
                {
                    // Don't show entire stack trace
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadInstallationFailed, e.Message), e, isUserError: false);
                }
            }

            _workloadInstaller.Shutdown();
            return _workloadInstaller.ExitCode;
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
