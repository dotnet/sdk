// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.History;
using Microsoft.DotNet.Workloads.Workload.Install;
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
            _fromPreviousSdk = parseResult.GetValue(WorkloadUpdateCommandParser.FromPreviousSdkOption);
            _adManifestOnlyOption = parseResult.GetValue(WorkloadUpdateCommandParser.AdManifestOnlyOption);
            _printRollbackDefinitionOnly = parseResult.GetValue(WorkloadUpdateCommandParser.PrintRollbackOption);

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
                    var workloadHistoryRecords = _workloadInstaller.GetWorkloadHistoryRecords().OrderBy(r => r.TimeStarted).ToList();
                    if (!int.TryParse(_fromHistorySpecified, out int index) || index < 1 || index > workloadHistoryRecords.Count)
                    {
                        throw new GracefulException(LocalizableStrings.WorkloadHistoryRecordNonIntegerId, isUserError: true);
                    }

                    _workloadHistoryState = workloadHistoryRecords[index - 1];
                    return _workloadHistoryState;
                }

                return null;
            }
        }

        public override int Execute()
        {
            WorkloadHistoryRecorder recorder = new WorkloadHistoryRecorder(_workloadResolver, _workloadInstaller);
            recorder.HistoryRecord.CommandName = "update";

            bool usedRollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);
            var rollbackFileContents = usedRollback ? WorkloadManifestUpdater.ParseRollbackDefinitionFile(_fromRollbackDefinition, _sdkFeatureBand) : null;
            if (usedRollback)
            {
                var rollbackContents = new Dictionary<string, string>();
                recorder.HistoryRecord.WorkloadArguments = new List<string>();
                foreach (var (id, version, featureBand) in rollbackFileContents)
                {
                    var idString = id.ToString();
                    recorder.HistoryRecord.WorkloadArguments.Add(idString);
                    rollbackContents[idString] = $"{version}/{featureBand}";
                }

                recorder.HistoryRecord.RollbackFileContents = rollbackContents;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
                {
                    try
                    {
                        var workloadIds = GetUpdatableWorkloads();
                        recorder.HistoryRecord.WorkloadArguments = workloadIds.Select(id => id.ToString()).ToList();
                        recorder.Run(() => DownloadToOfflineCacheAsync(new DirectoryPath(_downloadToCacheOption), _includePreviews, workloadIds).Wait());
                    }
                    catch (Exception e)
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e, isUserError: false);
                    }
                }
                else if (_printDownloadLinkOnly)
                {
                    var packageUrls = GetUpdatablePackageUrlsAsync(_includePreviews).GetAwaiter().GetResult();
                    PrintDownloadLink(packageUrls);
                }
                else if (_adManifestOnlyOption)
                {
                    var workloadManifestInfo = _workloadResolver.GetInstalledManifests();
                    recorder.HistoryRecord.WorkloadArguments = workloadManifestInfo.Select(m => m.Id).ToList();
                    recorder.Run(() => _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption), workloadManifestInfo).Wait());
                    Reporter.WriteLine();
                    Reporter.WriteLine(LocalizableStrings.WorkloadUpdateAdManifestsSucceeded);
                }
                else if (_printRollbackDefinitionOnly)
                {
                    var manifestInfo = WorkloadRollbackInfo.FromManifests(_workloadResolver.GetInstalledManifests());
                    Reporter.WriteLine("==workloadRollbackDefinitionJsonOutputStart==");
                    Reporter.WriteLine(manifestInfo.ToJson());
                    Reporter.WriteLine("==workloadRollbackDefinitionJsonOutputEnd==");
                }
                else
                {
                    recorder.Run(() =>
                    {
                        try
                        {
                            UpdateWorkloads(recorder, _includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption), rollbackFileContents);
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

        public void UpdateWorkloads(WorkloadHistoryRecorder recorder = null, bool includePreviews = false, DirectoryPath? offlineCache = null, IEnumerable<(ManifestId id, ManifestVersion version, SdkFeatureBand featureBand)> rollbackFileContents = null)
        {
            Reporter.WriteLine();

            var workloadIds = GetUpdatableWorkloads();
            if (recorder is not null)
            {
                recorder.HistoryRecord.WorkloadArguments = workloadIds.Select(id => id.ToString()).ToList();
            }

            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();

            var useRollbackOrHistory = !string.IsNullOrWhiteSpace(_fromRollbackDefinition) || !string.IsNullOrWhiteSpace(_fromHistorySpecified);

            var manifestsToUpdate = CalculateManifestUpdates(recorder);

            UpdateWorkloadsWithInstallRecord(_sdkFeatureBand, manifestsToUpdate, useRollbackOrHistory, offlineCache);

            WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion), offlineCache);

            _workloadManifestUpdater.DeleteUpdatableWorkloadsFile();

            Reporter.WriteLine();
            Reporter.WriteLine(string.Format(LocalizableStrings.UpdateSucceeded, string.Join(" ", workloadIds)));
            Reporter.WriteLine();
        }

        private IEnumerable<ManifestVersionUpdate> CalculateManifestUpdates(WorkloadHistoryRecorder recorder)
        {
            if (!string.IsNullOrWhiteSpace(_fromHistorySpecified))
            {
                WorkloadHistoryState state = _WorkloadHistoryRecord.StateAfterCommand;

                var currentWorkloadState = WorkloadRollbackInfo.FromManifests(_workloadResolver.GetInstalledManifests());

                var versionUpdates = new List<ManifestVersionUpdate>();

                foreach (KeyValuePair<string, string> m in state.ManifestVersions)
                {
                    var manifestId = new ManifestId(m.Key);
                    var featureBandAndVersion = m.Value.Split('/');
                    var currentVersionInformation = _workloadManifestUpdater.GetInstalledManifestVersion(manifestId);
                    versionUpdates.Add(new ManifestVersionUpdate(
                        manifestId,
                        currentVersionInformation.Version,
                        currentVersionInformation.Band.ToString(),
                        new ManifestVersion(featureBandAndVersion[0]),
                        featureBandAndVersion[1]));
                }

                return versionUpdates;
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
            DirectoryPath? offlineCache = null)
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

            transaction.Run(
                action: context =>
                {
                    UpdateInstalledWorkloadsFromHistory(sdkFeatureBand, context, offlineCache);

                    bool rollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

                    foreach (var manifestUpdate in manifestsToUpdate)
                    {
                        if (!manifestUpdate.ExistingVersion.Equals(manifestUpdate.NewVersion) ||
                            !manifestUpdate.ExistingFeatureBand.ToString().Equals(manifestUpdate.NewFeatureBand))
                        {
                            _workloadInstaller.InstallWorkloadManifest(manifestUpdate, context, offlineCache, rollback);
                        }
                    }

                    _workloadResolver.RefreshWorkloadManifests();

                    if (string.IsNullOrWhiteSpace(_fromHistorySpecified) || !_historyManifestOnlyOption)
                    {
                        var workloads = GetUpdatableWorkloads();
                        _workloadInstaller.InstallWorkloads(workloads, sdkFeatureBand, context, offlineCache);
                    }

                    UpdateInstallState(shouldUpdateInstallState, manifestsToUpdate);
                },
                rollback: () =>
                {
                    //  Nothing to roll back at this level, InstallWorkloadManifest and InstallWorkloadPacks handle the transaction rollback
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
                var desiredWorkloads = _WorkloadHistoryRecord.WorkloadArguments.Select(id => new WorkloadId(id));

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
    }
}
