// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
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
            _fromPreviousSdk = parseResult.GetValue(WorkloadUpdateCommandParser.FromPreviousSdkOption);
            _adManifestOnlyOption = parseResult.GetValue(WorkloadUpdateCommandParser.AdManifestOnlyOption);
            _printRollbackDefinitionOnly = parseResult.GetValue(WorkloadUpdateCommandParser.PrintRollbackOption);
            var resolvedReporter = _printDownloadLinkOnly || _printRollbackDefinitionOnly ? NullReporter.Instance : Reporter;
            _workloadSetMode = parseResult.GetValue(InstallingWorkloadCommandParser.WorkloadSetMode);

            _workloadInstaller = _workloadInstallerFromConstructor ?? WorkloadInstallerFactory.GetWorkloadInstaller(resolvedReporter,
                                _sdkFeatureBand, _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader,
                                _dotnetPath, TempDirectoryPath, packageSourceLocation: _packageSourceLocation, RestoreActionConfiguration,
                                elevationRequired: !_printDownloadLinkOnly && !_printRollbackDefinitionOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));

            _workloadManifestUpdater = _workloadManifestUpdaterFromConstructor ?? new WorkloadManifestUpdater(resolvedReporter, _workloadResolver, PackageDownloader, _userProfileDir,
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), _workloadInstaller, _packageSourceLocation, sdkFeatureBand: _sdkFeatureBand);
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
                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption)).Wait();
                Reporter.WriteLine();
                Reporter.WriteLine(LocalizableStrings.WorkloadUpdateAdManifestsSucceeded);
            }
            else if (_printRollbackDefinitionOnly)
            {
                var workloadSet = WorkloadSet.FromManifests(_workloadResolver.GetInstalledManifests());
                Reporter.WriteLine(workloadSet.ToJson());
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
                try
                {
                    UpdateWorkloads(_includePreviews, string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption));
                }
                catch (Exception e)
                {
                    // Don't show entire stack trace
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadUpdateFailed, e.Message), e, isUserError: false);
                }
            }

            _workloadInstaller.Shutdown();
            return _workloadInstaller.ExitCode;
        }

        public void UpdateWorkloads(bool includePreviews = false, DirectoryPath? offlineCache = null)
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
                    foreach (var manifestUpdate in manifestsToUpdate)
                    {
                        _workloadInstaller.InstallWorkloadManifest(manifestUpdate, context, offlineCache, useRollback);
                    }

                    _workloadResolver.RefreshWorkloadManifests();

                    var workloads = GetUpdatableWorkloads();

                    _workloadInstaller.InstallWorkloads(workloads, sdkFeatureBand, context, offlineCache);

                    if (useRollback)
                    {
                        _workloadInstaller.SaveInstallStateManifestVersions(sdkFeatureBand, GetInstallStateContents(manifestsToUpdate));
                    }
                    else
                    {
                        _workloadInstaller.RemoveManifestsFromInstallState(sdkFeatureBand);
                    }
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
            var workloads = GetInstalledWorkloads(_fromPreviousSdk);

            if (workloads == null || !workloads.Any())
            {
                reporter.WriteLine(LocalizableStrings.NoWorkloadsToUpdate);
            }

            return workloads;
        }
    }
}
