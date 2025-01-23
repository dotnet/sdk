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

namespace Microsoft.DotNet.Workloads.Workload.Update
{
    internal class WorkloadUpdateCommand : InstallingWorkloadCommand
    {
        private readonly bool _adManifestOnlyOption;
        private readonly bool _printRollbackDefinitionOnly;
        private readonly bool _fromPreviousSdk;
        private WorkloadHistoryRecorder _recorder;
        private readonly bool _isRestoring;
        private readonly bool _shouldShutdownInstaller;
        public WorkloadUpdateCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolverFactory workloadResolverFactory = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            string tempDirPath = null,
            bool isRestoring = false,
            WorkloadHistoryRecorder recorder = null)
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

            _shouldShutdownInstaller = _workloadInstallerFromConstructor != null;


            _workloadManifestUpdater = _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(resolvedReporter, _workloadResolver, PackageDownloader, _userProfileDir,
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), _workloadInstaller, _packageSourceLocation, sdkFeatureBand: _sdkFeatureBand);
            _recorder = recorder;
            if (_recorder is null)
            {
                _recorder = new(_workloadResolver, _workloadInstaller, () => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, null));
                _recorder.HistoryRecord.CommandName = "update";

            }

            _fromHistorySpecified = parseResult.GetValue(WorkloadUpdateCommandParser.FromHistoryOption);
            _historyManifestOnlyOption = !string.IsNullOrWhiteSpace(parseResult.GetValue(WorkloadUpdateCommandParser.HistoryManifestOnlyOption));
            _isRestoring = isRestoring;
        }

        public override int Execute()
        {
            if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
            {
                try
                {
                    DownloadToOfflineCacheAsync(new DirectoryPath(_downloadToCacheOption), _includePreviews).Wait();
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
                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(
                    _includePreviews,
                    ShouldUseWorkloadSetMode(_sdkFeatureBand, _workloadRootDir),
                    string.IsNullOrWhiteSpace(_fromCacheOption) ?
                        null :
                        new DirectoryPath(_fromCacheOption))
                    .Wait();
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
                Reporter.WriteLine();
                try
                {
                    if (!_isRestoring)
                    {
                        _recorder.Run(() =>
                        {
                            UpdateWorkloads();
                        });
                    }
                    else
                    {
                        UpdateWorkloads();
                    }
                }
                catch (Exception e)
                {
                    // Don't show entire stack trace
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadUpdateFailed, e.Message), e, isUserError: false);
                }
            }

            if (_shouldShutdownInstaller)
            {
                _workloadInstaller.Shutdown();
            }
            return _workloadInstaller.ExitCode;
        }

        private void UpdateWorkloads()
        {
            DirectoryPath? offlineCache = string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption);
            var workloadIds = Enumerable.Empty<WorkloadId>();
            RunInNewTransaction(context =>
            {
                UpdateWorkloadManifests(_recorder, context, offlineCache);

                // This depends on getting the available workloads, so it needs to run after manifests have potentially been installed
                workloadIds = WriteSDKInstallRecordsForVSWorkloads(GetUpdatableWorkloads());

                if (FromHistory)
                {
                    if (!_historyManifestOnlyOption)
                    {
                        UpdateInstalledWorkloadsFromHistory(context, offlineCache);
                    }
                }
                else
                {
                    _workloadInstaller.InstallWorkloads(workloadIds, _sdkFeatureBand, context, offlineCache);
                }
            });

            WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion), offlineCache);

            //  TODO: potentially only do this in some cases (ie not if global.json specifies workload set)
            _workloadManifestUpdater.DeleteUpdatableWorkloadsFile();

            Reporter.WriteLine();
            Reporter.WriteLine(string.Format(LocalizableStrings.UpdateSucceeded, string.Join(" ", workloadIds)));
            Reporter.WriteLine();
        }

        private void UpdateInstalledWorkloadsFromHistory(ITransactionContext context, DirectoryPath? offlineCache)
        {
            if (FromHistory)
            {
                // Only have specified workloads installed afterwards.
                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);
                var desiredWorkloads = _WorkloadHistoryRecord.InstalledWorkloads.Select(id => new WorkloadId(id));

                var workloadsToInstall = desiredWorkloads.Except(installedWorkloads).ToList();
                var workloadsToUninstall = installedWorkloads.Except(desiredWorkloads).ToList();

                _workloadInstaller.InstallWorkloads(workloadsToInstall, _sdkFeatureBand, context, offlineCache);

                foreach (var id in workloadsToUninstall)
                {
                    _workloadInstaller.GetWorkloadInstallationRecordRepository()
                       .DeleteWorkloadInstallationRecord(id, _sdkFeatureBand);
                }
            }
        }

        private async Task DownloadToOfflineCacheAsync(DirectoryPath offlineCache, bool includePreviews)
        {
            await GetDownloads(GetUpdatableWorkloads(), skipManifestUpdate: false, includePreviews, offlineCache.Value);
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

        private IEnumerable<WorkloadId> GetUpdatableWorkloads(IReporter reporter = null)
        {
            reporter ??= Reporter;
            var workloads = FromHistory ? _WorkloadHistoryRecord.InstalledWorkloads.Select(s => new WorkloadId(s)) : GetInstalledWorkloads(_fromPreviousSdk);

            if (workloads == null || !workloads.Any())
            {
                reporter.WriteLine(LocalizableStrings.NoWorkloadsToUpdate);
            }

            return workloads;
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
