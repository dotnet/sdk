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
using NuGet.Common;
using NuGet.Versioning;
using static Microsoft.DotNet.Workloads.Workload.Install.WorkloadManifestUpdater;

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
            var resolvedReporter = _printDownloadLinkOnly || _printRollbackDefinitionOnly ? NullReporter.Instance : Reporter;

            _workloadInstaller = _workloadInstallerFromConstructor ?? WorkloadInstallerFactory.GetWorkloadInstaller(resolvedReporter,
                                _sdkFeatureBand, _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader,
                                _dotnetPath, TempDirectoryPath, packageSourceLocation: _packageSourceLocation, RestoreActionConfiguration,
                                elevationRequired: !_printDownloadLinkOnly && !_printRollbackDefinitionOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));

            _workloadManifestUpdater = _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(resolvedReporter, _workloadResolver, PackageDownloader, _userProfileDir,
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
            var rollbackFileContents = usedRollback ? _workloadManifestUpdater.ParseRollbackDefinitionFile(_fromRollbackDefinition, _sdkFeatureBand) : null;
            if (usedRollback)
            {
                var rollbackContents = new Dictionary<string, string>();
                recorder.HistoryRecord.WorkloadArguments = new List<string>();
                foreach ((ManifestId id, ManifestVersionWithBand ManifestWithBand) content in rollbackFileContents)
                {
                    var id = content.Item1;
                    var versionWithBand = content.Item2;
                    var idString = id.ToString();
                    recorder.HistoryRecord.WorkloadArguments.Add(idString);
                    rollbackContents[idString] = $"{versionWithBand.Version}/{versionWithBand.Band}";
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
                    var packageDownloader = IsPackageDownloaderProvided ? PackageDownloader : new NuGetPackageDownloader(
                    TempPackagesDirectory,
                    filePermissionSetter: null,
                    new FirstPartyNuGetPackageSigningVerifier(),
                    new NullLogger(),
                    NullReporter.Instance,
                    restoreActionConfig: RestoreActionConfiguration,
                    verifySignatures: VerifySignatures);

                    var packageUrls = GetUpdatablePackageUrlsAsync(_includePreviews, NullReporter.Instance, packageDownloader).GetAwaiter().GetResult();
                    Reporter.WriteLine(JsonSerializer.Serialize(packageUrls, new JsonSerializerOptions() { WriteIndented = true }));
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
                    var workloadSet = WorkloadSet.FromManifests(_workloadResolver.GetInstalledManifests());
                    Reporter.WriteLine(workloadSet.ToJson());
                }
                else
                {
                    recorder.Run(() =>
                    {
                        try
                        {
                            var workloadIds = GetUpdatableWorkloads();
                            recorder.HistoryRecord.WorkloadArguments = workloadIds.Select(id => id.ToString()).ToList();
                            UpdateWorkloads(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption), rollbackFileContents, workloadIds: workloadIds);
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

        public void UpdateWorkloads(bool includePreviews = false, DirectoryPath? offlineCache = null, IEnumerable<(ManifestId id, ManifestVersionWithBand versionWithBand)> rollbackFileContents = null, IEnumerable<WorkloadId> workloadIds = null)
        {
            Reporter.WriteLine();

            workloadIds ??= GetUpdatableWorkloads();
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();

            var manifestsToUpdate = CalculateManifestUpdates(rollbackFileContents);
            var useRollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

            UpdateWorkloadsWithInstallRecord(_sdkFeatureBand, manifestsToUpdate, useRollback, offlineCache);

            WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion), offlineCache);

            _workloadManifestUpdater.DeleteUpdatableWorkloadsFile();

            Reporter.WriteLine();
            Reporter.WriteLine(string.Format(LocalizableStrings.UpdateSucceeded, string.Join(" ", workloadIds)));
            Reporter.WriteLine();
        }

        private IEnumerable<ManifestVersionUpdate> CalculateManifestUpdates(IEnumerable<(ManifestId id, ManifestVersionWithBand versionWithBand)> rollbackFileContents)
        {
            bool fromHistory = !string.IsNullOrWhiteSpace(_fromHistorySpecified);
            if (string.IsNullOrWhiteSpace(_fromRollbackDefinition) && !fromHistory)
            {
                return _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.ManifestUpdate);
            }
            else if (fromHistory)
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

            var transaction = new CliTransaction
            {
                RollbackStarted = () =>
                {
                    Reporter.WriteLine(LocalizableStrings.RollingBackInstall);
                },
                // Don't hide the original error if roll back fails, but do log the rollback failure
                RollbackFailed = ex =>
                {
                    Reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, ex.Message));
                }
            };

            transaction.Run(
                action: context =>
                {
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

                    UpdateInstallState(useRollback || !string.IsNullOrWhiteSpace(_fromHistorySpecified), manifestsToUpdate);
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

        private async Task<IEnumerable<string>> GetUpdatablePackageUrlsAsync(bool includePreview, IReporter reporter = null, INuGetPackageDownloader packageDownloader = null)
        {
            reporter ??= Reporter;
            packageDownloader ??= PackageDownloader;
            var downloads = await GetDownloads(GetUpdatableWorkloads(reporter), skipManifestUpdate: false, includePreview, reporter: reporter, packageDownloader: packageDownloader);

            var urls = new List<string>();
            foreach (var download in downloads)
            {
                urls.Add(await packageDownloader.GetPackageUrl(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion), _packageSourceLocation));
            }

            return urls;
        }

        private IEnumerable<WorkloadId> GetUpdatableWorkloads(IReporter reporter = null, List<WorkloadHistoryRecord> workloadHistoryRecords = null)
        {
            reporter ??= Reporter;
            var workloads = !string.IsNullOrWhiteSpace(_fromHistorySpecified) ?
                _WorkloadHistoryRecord.StateAfterCommand.InstalledWorkloads.Select(s => new WorkloadId(s)) :
                GetInstalledWorkloads(_fromPreviousSdk);

            if (workloads == null || !workloads.Any())
            {
                reporter.WriteLine(LocalizableStrings.NoWorkloadsToUpdate);
            }

            return workloads;
        }
    }
}
