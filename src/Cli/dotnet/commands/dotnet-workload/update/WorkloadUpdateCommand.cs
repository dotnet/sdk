// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
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

        public override int Execute()
        {
            WorkloadHistoryRecorder recorder = new WorkloadHistoryRecorder(_workloadResolver, _workloadInstaller);
            recorder.HistoryRecord.CommandName = "update";
            //recorder.HistoryRecord.WorkloadArguments = _workloadIds.Select(id => id.ToString()).ToList();

            bool usedRollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);
            var rollbackFileContents = usedRollback ? WorkloadManifestUpdater.ParseRollbackDefinitionFile(_fromRollbackDefinition, _sdkFeatureBand) : null;
            if (usedRollback)
            {
                var rollbackContents = new Dictionary<string, string>();
                foreach (var (id, version, featureBand) in rollbackFileContents)
                {
                    rollbackContents[id.ToString()] = $"{version}/{featureBand}";
                }

                recorder.HistoryRecord.RollbackFileContents = rollbackContents;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
                {
                    try
                    {
                        recorder.Run(() => DownloadToOfflineCacheAsync(new DirectoryPath(_downloadToCacheOption), _includePreviews).Wait());
                    }
                    catch (Exception e)
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e, isUserError: false);
                    }
                }
                else if (_printDownloadLinkOnly)
                {
                    var packageUrls = GetUpdatablePackageUrlsAsync(_includePreviews).GetAwaiter().GetResult();
                    Reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                    Reporter.WriteLine(JsonSerializer.Serialize(packageUrls, new JsonSerializerOptions() { WriteIndented = true }));
                    Reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
                }
                else if (_adManifestOnlyOption)
                {
                    recorder.Run(() => _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption)).Wait());
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
                            UpdateWorkloads(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption), rollbackFileContents);
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

        public void UpdateWorkloads(bool includePreviews = false, DirectoryPath? offlineCache = null, IEnumerable<(ManifestId id, ManifestVersion version, SdkFeatureBand featureBand)> rollbackFileContents = null)
        {
            Reporter.WriteLine();

            var workloadIds = GetUpdatableWorkloads();
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();

            var useRollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

            var manifestsToUpdate = useRollback ?
                _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition) :
                _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.ManifestUpdate);

            UpdateWorkloadsWithInstallRecord(_sdkFeatureBand, manifestsToUpdate, useRollback, offlineCache);

            WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion), offlineCache);

            _workloadManifestUpdater.DeleteUpdatableWorkloadsFile();

            Reporter.WriteLine();
            Reporter.WriteLine(string.Format(LocalizableStrings.UpdateSucceeded, string.Join(" ", workloadIds)));
            Reporter.WriteLine();
        }

        private IEnumerable<ManifestVersionUpdate> CalculateManifestUpdates(IEnumerable<(ManifestId id, ManifestVersion version, SdkFeatureBand featureBand)> rollbackFileContents)
        {
            if (string.IsNullOrWhiteSpace(_fromRollbackDefinition))
            {
                return _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.manifestUpdate);
            }
            else if (_fromHistorySpecified)
            {
                var workloadHistoryRecords = _workloadInstaller.GetWorkloadHistoryRecords().OrderBy(r => r.TimeStarted).ToList();
                WorkloadHistoryState state;
                if (!string.IsNullOrEmpty(_afterID))
                {
                    if (!int.TryParse(_afterID, out int index))
                    {
                        throw;
                    }

                    var workloadHistoryRecord = workloadHistoryRecords[index - 1];
                    state = workloadHistoryRecord.StateAfterCommand;
                }
                else if (!string.IsNullOrEmpty(_beforeID))
                {
                    if (!int.TryParse(_beforeID, out int index))
                    {
                        throw;
                    }

                    var workloadHistoryRecord = workloadHistoryRecords[index - 1];
                    state = workloadHistoryRecord.StateBeforeCommand;
                }
                else
                {
                    throw;
                }

                var currentWorkloadState = WorkloadRollbackInfo.FromManifests(_workloadResolver.GetInstalledManifests());

                var versionUpdates = new List<ManifestVersionUpdate>();

                foreach (KeyValuePair<string, string> m in state.ManifestVersions)
                {
                    var manifestId = new ManifestId(m.Key);
                    var currentVersionInformation = _workloadManifestUpdater.GetInstalledManifestVersion(manifestId);
                    versionUpdates.Add(new ManifestVersionUpdate(
                        manifestId,
                        currentVersionInformation.manifestVersion,
                        currentVersionInformation.sdkFeatureBand.ToString(),
                        new ManifestVersion(m.Value.Split('/')[0]),
                        m.Value.Split('/')[1]));
                }

                return versionUpdates;
            }
            else
            {
                return _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition, rollbackFileContents);
            }
        }

        private void UpdateWorkloadsWithInstallRecord(
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<ManifestVersionUpdate> manifestsToUpdate,
            bool useRollback,
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
                    bool rollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

                    foreach (var manifestUpdate in manifestsToUpdate)
                    {
                        _workloadInstaller.InstallWorkloadManifest(manifestUpdate, context, offlineCache, rollback);
                    }

                    _workloadResolver.RefreshWorkloadManifests();

                    if (!_fromHistorySpecified || !_historyManifestOnlyOption)
                    {
                        var workloads = GetUpdatableWorkloads();

                    _workloadInstaller.InstallWorkloads(workloads, sdkFeatureBand, context, offlineCache);

                    UpdateInstallState(useRollback, manifestsToUpdate);
                },
                rollback: () =>
                {
                    //  Nothing to roll back at this level, InstallWorkloadManifest and InstallWorkloadPacks handle the transaction rollback
                });
        }

        private async Task DownloadToOfflineCacheAsync(DirectoryPath offlineCache, bool includePreviews)
        {
            await GetDownloads(GetUpdatableWorkloads(), skipManifestUpdate: false, includePreviews, offlineCache.Value);
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
            var workloads = GetInstalledWorkloads(_fromPreviousSdk);

            if (workloads == null || !workloads.Any())
            {
                Reporter.WriteLine(LocalizableStrings.NoWorkloadsToUpdate);
            }

            return workloads;
        }
    }
}
