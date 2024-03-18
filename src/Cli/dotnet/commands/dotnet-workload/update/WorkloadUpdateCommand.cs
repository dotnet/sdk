// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.History;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;

namespace Microsoft.DotNet.Workloads.Workload.Update
{
    internal class WorkloadUpdateCommand : InstallingWorkloadCommand
    {
        private readonly bool _adManifestOnlyOption;
        private readonly bool _printRollbackDefinitionOnly;
        private readonly bool _fromPreviousSdk;
        private WorkloadHistoryRecord _workloadHistoryState;
        private readonly string _workloadSetMode;

        public WorkloadUpdateCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolverFactory workloadResolverFactory = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            string tempDirPath = null)
            : base(parseResult, reporter: reporter, workloadResolverFactory: workloadResolverFactory, workloadInstaller: workloadInstaller,
                  nugetPackageDownloader: nugetPackageDownloader, workloadManifestUpdater: workloadManifestUpdater,
                  tempDirPath: tempDirPath)

        {
            _workloadSetVersion = parseResult.GetValue(InstallingWorkloadCommandParser.WorkloadSetVersionOption);
            _fromPreviousSdk = parseResult.GetValue(WorkloadUpdateCommandParser.FromPreviousSdkOption);
            _adManifestOnlyOption = parseResult.GetValue(WorkloadUpdateCommandParser.AdManifestOnlyOption);
            _printRollbackDefinitionOnly = parseResult.GetValue(WorkloadUpdateCommandParser.PrintRollbackOption);
            _workloadSetMode = parseResult.GetValue(InstallingWorkloadCommandParser.WorkloadSetMode);

            _workloadInstaller = _workloadInstallerFromConstructor ?? WorkloadInstallerFactory.GetWorkloadInstaller(Reporter,
                                _sdkFeatureBand, _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader,
                                _dotnetPath, TempDirectoryPath, packageSourceLocation: _packageSourceLocation, RestoreActionConfiguration,
                                elevationRequired: !_printDownloadLinkOnly && !_printRollbackDefinitionOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));

            _workloadManifestUpdater = _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(Reporter, _workloadResolver, PackageDownloader, _userProfileDir,
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), _workloadInstaller, _packageSourceLocation, sdkFeatureBand: _sdkFeatureBand);
        }

        private WorkloadHistoryRecord _WorkloadHistoryRecord
        {
            get
            {
                if (_workloadHistoryState is not null)
                {
                    return _workloadHistoryState;
                }

                if (!string.IsNullOrWhiteSpace(_fromHistorySpecified))
                {
                    var workloadHistoryRecords = _workloadInstaller.GetWorkloadHistoryRecords(_sdkFeatureBand.ToString()).OrderBy(r => r.TimeStarted).ToList();
                    var historyRecordsWithUnknownAndInitial = new List<WorkloadHistoryRecord>();
                    historyRecordsWithUnknownAndInitial.Add(new WorkloadHistoryRecord()
                    {
                        StateAfterCommand = workloadHistoryRecords.First().StateBeforeCommand
                    });

                    var previous = historyRecordsWithUnknownAndInitial.First();

                    foreach (var historyRecord in workloadHistoryRecords)
                    {
                        if (!historyRecord.StateBeforeCommand.Equals(previous.StateAfterCommand))
                        {
                            historyRecordsWithUnknownAndInitial.Add(new WorkloadHistoryRecord()
                            {
                                StateAfterCommand = historyRecord.StateBeforeCommand
                            });
                        }

                        historyRecordsWithUnknownAndInitial.Add(historyRecord);
                    }

                    if (!int.TryParse(_fromHistorySpecified, out int index) || index < 1 || index > historyRecordsWithUnknownAndInitial.Count)
                    {
                        throw new GracefulException(LocalizableStrings.WorkloadHistoryRecordNonIntegerId, isUserError: true);
                    }

                    _workloadHistoryState = historyRecordsWithUnknownAndInitial[index - 1];
                    return _workloadHistoryState;
                }

                return null;
            }
        }

        public override int Execute()
        {
            WorkloadHistoryRecorder recorder = new WorkloadHistoryRecorder(_workloadResolver, _workloadInstaller);
            recorder.HistoryRecord.CommandName = "update";

            try
            {
                if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
                {
                    try
                    {
                        var workloadIds = GetUpdatableWorkloads();
                        recorder.HistoryRecord.WorkloadArguments = workloadIds.Select(id => id.ToString()).ToList();
                        DownloadToOfflineCacheAsync(new DirectoryPath(_downloadToCacheOption), _includePreviews, workloadIds).Wait();
                    }
                    catch (Exception e)
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e, isUserError: false);
                    }
                }
                else if (_printDownloadLinkOnly)
                {
                    var packageUrls = GetUpdatablePackageUrlsAsync(_includePreviews).GetAwaiter().GetResult();
                    Reporter.WriteLine(JsonSerializer.Serialize(packageUrls, new JsonSerializerOptions() { WriteIndented = true }));
                }
                else if (_adManifestOnlyOption)
                {
                     _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(
                        _includePreviews,
                        ShouldUseWorkloadSetMode(_sdkFeatureBand, _dotnetPath),
                        string.IsNullOrWhiteSpace(_fromCacheOption) ?
                            null :
                            new DirectoryPath(_fromCacheOption))
                        .Wait();
                    Reporter.WriteLine();
                    Reporter.WriteLine(LocalizableStrings.WorkloadUpdateAdManifestsSucceeded);
                }
                else if (_printRollbackDefinitionOnly)
                {
                    var manifestInfo = WorkloadRollbackInfo.FromManifests(_workloadResolver.GetInstalledManifests());
                    Reporter.WriteLine(manifestInfo.ToJson());
                }
                else if (!string.IsNullOrWhiteSpace(_workloadSetMode))
                {
                    if (_workloadSetMode.Equals("workloadset", StringComparison.OrdinalIgnoreCase))
                    {
                        _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, true);
                    }
                    else if (_workloadSetMode.Equals("loosemanifest", StringComparison.OrdinalIgnoreCase) ||
                            _workloadSetMode.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    {
                        _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, false);
                    }
                    else
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadSetModeTakesWorkloadSetLooseManifestOrAuto, _workloadSetMode), isUserError: true);
                    }
                }
                else
                {
                    recorder.Run(() =>
                    {
                        try
                        {
                            DirectoryPath? offlineCache = string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption);
                            if (string.IsNullOrWhiteSpace(_workloadSetVersion))
                            {
                                CalculateManifestUpdatesAndUpdateWorkloads(recorder, _includePreviews, offlineCache);
                            }
                            else
                            {
                                RunInNewTransaction(context =>
                                {
                                    var manifestUpdates = HandleWorkloadUpdateFromVersion(context, offlineCache);
                                    UpdateWorkloads(false, manifestUpdates, offlineCache, context, recorder);
                                });
                            }
                        }
                        catch (Exception e)
                        {
                            // Don't show entire stack trace
                            throw new GracefulException(string.Format(LocalizableStrings.WorkloadUpdateFailed, e.Message), e, isUserError: false);
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

        public void CalculateManifestUpdatesAndUpdateWorkloads(WorkloadHistoryRecorder recorder = null, bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            Reporter.WriteLine();

            var useRollbackOrHistory = !string.IsNullOrWhiteSpace(_fromRollbackDefinition) || !string.IsNullOrWhiteSpace(_fromHistorySpecified);
            var useWorkloadSets = ShouldUseWorkloadSetMode(_sdkFeatureBand, _dotnetPath);

            if (useRollbackOrHistory && useWorkloadSets)
            {
                // Rollback files are only for loose manifests. Update the mode to be loose manifests.
                Reporter.WriteLine(LocalizableStrings.UpdateFromRollbackSwitchesModeToLooseManifests);
                _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, false);
                useWorkloadSets = false;
            }

            var workloadIds = GetUpdatableWorkloads();
            if (recorder is not null)
            {
                recorder.HistoryRecord.WorkloadArguments = workloadIds.Select(id => id.ToString()).ToList();
            }
            WriteSDKInstallRecordsForVSWorkloads(workloadIds);
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, useWorkloadSets, offlineCache).Wait();

            IEnumerable<ManifestVersionUpdate> manifestsToUpdate;
            RunInNewTransaction(context =>
            {
                if (useWorkloadSets)
                {
                    manifestsToUpdate = InstallWorkloadSet(context);
                }
                else
                {
                    manifestsToUpdate = useRollbackOrHistory ? _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition, recorder) :
                    _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.ManifestUpdate);
                }

                UpdateWorkloads(useRollbackOrHistory, manifestsToUpdate, offlineCache, context, recorder);
                
                Reporter.WriteLine();
                Reporter.WriteLine(string.Format(LocalizableStrings.UpdateSucceeded, string.Join(" ", workloadIds)));
                Reporter.WriteLine();
            });
        }

        private void UpdateWorkloads(bool useRollback, IEnumerable<ManifestVersionUpdate> manifestsToUpdate, DirectoryPath? offlineCache, ITransactionContext context, WorkloadHistoryRecorder recorder = null)
        {
            var workloadIds = GetUpdatableWorkloads();
            if (recorder is not null)
            {
                recorder.HistoryRecord.WorkloadArguments = workloadIds.Select(id => id.ToString()).ToList();
            }

            UpdateWorkloadsWithInstallRecord(_sdkFeatureBand, manifestsToUpdate, useRollback, context, offlineCache);

            WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion), offlineCache);

            _workloadManifestUpdater.DeleteUpdatableWorkloadsFile();
        }

        private void WriteSDKInstallRecordsForVSWorkloads(IEnumerable<WorkloadId> updateableWorkloads)
        {
#if !DOT_NET_BUILD_FROM_SOURCE
            if (OperatingSystem.IsWindows())
            {
                VisualStudioWorkloads.WriteSDKInstallRecordsForVSWorkloads(_workloadInstaller, _workloadResolver, updateableWorkloads, Reporter);
            }
#endif
        }

        private IEnumerable<ManifestVersionUpdate> CalculateManifestUpdates(WorkloadHistoryRecorder recorder)
        {
            if (!string.IsNullOrWhiteSpace(_fromHistorySpecified))
            {
                return _workloadManifestUpdater.CalculateManifestUpdatesFromHistory(_WorkloadHistoryRecord);
            }
            else if (!string.IsNullOrWhiteSpace(_fromRollbackDefinition))
            {
                return _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition, recorder);
            }
            else
            {
                return _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.ManifestUpdate);
            }
        }

        private void UpdateWorkloadsWithInstallRecord(
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<ManifestVersionUpdate> manifestsToUpdate,
            bool shouldUpdateInstallState,
            ITransactionContext context,
            DirectoryPath? offlineCache = null)
        {
            context.Run(
                action: () =>
                {
                    foreach (var manifestUpdate in manifestsToUpdate)
                    {
                        if (manifestUpdate.NewFeatureBand != null && manifestUpdate.NewVersion != null &&
                            (manifestUpdate.ExistingFeatureBand is null ||
                            !manifestUpdate.ExistingVersion.Equals(manifestUpdate.NewVersion) ||
                            !manifestUpdate.ExistingFeatureBand.ToString().Equals(manifestUpdate.NewFeatureBand)))
                        {
                            _workloadInstaller.InstallWorkloadManifest(manifestUpdate, context, offlineCache, shouldUpdateInstallState);
                        }
                    }

                    if (shouldUpdateInstallState)
                    {
                        _workloadInstaller.SaveInstallStateManifestVersions(_sdkFeatureBand, GetInstallStateContents(manifestsToUpdate));
                    }
                    else
                    {
                        _workloadInstaller.RemoveManifestsFromInstallState(_sdkFeatureBand);
                    }

                    if (!string.IsNullOrWhiteSpace(_fromHistorySpecified))
                    {
                        _workloadInstaller.GarbageCollect(workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion, useInstallStateOnly: true), offlineCache);
                    }

                    _workloadInstaller.AdjustWorkloadSetInInstallState(sdkFeatureBand, string.IsNullOrWhiteSpace(_workloadSetVersion) ? null : _workloadSetVersion);

                    _workloadResolver.RefreshWorkloadManifests();

                    if (string.IsNullOrWhiteSpace(_fromHistorySpecified))
                    {
                        var workloads = GetUpdatableWorkloads();
                        _workloadInstaller.InstallWorkloads(workloads, sdkFeatureBand, context, offlineCache);
                    }
                    else if (!_historyManifestOnlyOption)
                    {
                        UpdateInstalledWorkloadsFromHistory(sdkFeatureBand, context, offlineCache);
                    }
                },
                rollback: () =>
                {
                    //  Nothing to roll back at this level, InstallWorkloadManifest and InstallWorkloadPacks handle the transaction rollback
                    //  We will refresh the workload manifests to make sure that the resolver has the updated state after the rollback
                    _workloadResolver.RefreshWorkloadManifests();
                });
        }

        private async Task DownloadToOfflineCacheAsync(DirectoryPath offlineCache, bool includePreviews, IEnumerable<WorkloadId> workloadIds)
        {
            await GetDownloads(workloadIds, skipManifestUpdate: false, includePreviews, offlineCache.Value);
        }

        private async Task<IEnumerable<string>> GetUpdatablePackageUrlsAsync(bool includePreview)
        {
            var downloads = await GetDownloads(GetUpdatableWorkloads(), skipManifestUpdate: false, includePreview);

            var urls = new List<string>();
            foreach (var download in downloads)
            {
                urls.Add(await PackageDownloader.GetPackageUrl(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion), _packageSourceLocation));
            }

            return urls;
        }

        private IEnumerable<WorkloadId> GetUpdatableWorkloads()
        {
            var workloads = !string.IsNullOrWhiteSpace(_fromHistorySpecified) ?
                _WorkloadHistoryRecord.StateAfterCommand.InstalledWorkloads.Select(s => new WorkloadId(s)) :
                GetInstalledWorkloads(_fromPreviousSdk);

            if (workloads == null || !workloads.Any())
            {
                Reporter.WriteLine(LocalizableStrings.NoWorkloadsToUpdate);
            }

            return workloads;
        }

        private void UpdateInstalledWorkloadsFromHistory(SdkFeatureBand sdkFeatureBand, ITransactionContext context, DirectoryPath? offlineCache)
        {
            if (!string.IsNullOrWhiteSpace(_fromHistorySpecified))
            {
                // Only have specified workloads installed afterwards.
                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);
                var desiredWorkloads = _WorkloadHistoryRecord.StateAfterCommand.InstalledWorkloads.Select(id => new WorkloadId(id));

                var workloadsToInstall = new List<WorkloadId>();
                var workloadsToUninstall = new List<WorkloadId>();

                foreach (var id in installedWorkloads)
                {
                    if (!desiredWorkloads.Contains(id))
                    {
                        workloadsToUninstall.Add(id);
                    }
                }

                foreach (var id in desiredWorkloads)
                {
                    if (!installedWorkloads.Contains(id))
                    {
                        workloadsToInstall.Add(id);
                    }
                }

                _workloadInstaller.InstallWorkloads(workloadsToInstall, sdkFeatureBand, context, offlineCache);

                foreach (var id in workloadsToUninstall)
                {
                    _workloadInstaller.GetWorkloadInstallationRecordRepository()
                       .DeleteWorkloadInstallationRecord(id, sdkFeatureBand);
                }
            }
        }
        
        private void RunInNewTransaction(Action<ITransactionContext> a)
        {
            var transaction = new CliTransaction();
            transaction.RollbackStarted = () =>
            {
                Reporter.WriteLine(LocalizableStrings.RollingBackInstall);
            };
            // Don't hide the original error if roll back fails, but do log the rollback failure
            transaction.RollbackFailed = ex =>
            {
                Reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, ex.Message));
            };

            transaction.Run(context => a(context));
        }
    }
}
