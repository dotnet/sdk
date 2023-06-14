// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallCommand : WorkloadCommandBase
    {
        private readonly bool _skipManifestUpdate;
        private readonly string _fromCacheOption;
        private readonly string _downloadToCacheOption;
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly bool _printDownloadLinkOnly;
        private readonly bool _includePreviews;
        private readonly IReadOnlyCollection<string> _workloadIds;
        private readonly IInstaller _workloadInstaller;
        private IWorkloadResolver _workloadResolver;        
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly ReleaseVersion _sdkVersion;
        private readonly SdkFeatureBand _sdkFeatureBand;
        private readonly string _userProfileDir;
        private readonly string _dotnetPath;
        private readonly string _fromRollbackDefinition;

        public WorkloadInstallCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            string dotnetDir = null,
            string userProfileDir = null,
            string tempDirPath = null,
            string version = null,
            IReadOnlyCollection<string> workloadIds = null)
            : base(parseResult, reporter: reporter, tempDirPath: tempDirPath, nugetPackageDownloader: nugetPackageDownloader)
        {
            _skipManifestUpdate = parseResult.GetValueForOption(WorkloadInstallCommandParser.SkipManifestUpdateOption);
            _includePreviews = parseResult.GetValueForOption(WorkloadInstallCommandParser.IncludePreviewOption);
            _printDownloadLinkOnly = parseResult.GetValueForOption(WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.FromCacheOption);
            _downloadToCacheOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.DownloadToCacheOption);
            _workloadIds = workloadIds ?? parseResult.GetValueForArgument(WorkloadInstallCommandParser.WorkloadIdArgument).ToList().AsReadOnly();
            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _userProfileDir = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValueForOption(WorkloadInstallCommandParser.VersionOption), version, _dotnetPath, _userProfileDir);
            _sdkFeatureBand = new SdkFeatureBand(string.Join('.', _sdkVersion.Major, _sdkVersion.Minor, _sdkVersion.SdkFeatureBand));
            _fromRollbackDefinition = parseResult.GetValueForOption(WorkloadInstallCommandParser.FromRollbackFileOption);

            var configOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: sourceOption);

            var sdkWorkloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString(), userProfileDir);
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(sdkWorkloadManifestProvider, _dotnetPath, _sdkVersion.ToString(), _userProfileDir);
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            var tempPackagesDir = new DirectoryPath(Path.Combine(TempDirectoryPath, "dotnet-sdk-advertising-temp"));
            
            _workloadInstaller = workloadInstaller ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand,
                                     _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader, _dotnetPath, TempDirectoryPath,
                                     _packageSourceLocation, RestoreActionConfiguration, elevationRequired: !_printDownloadLinkOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));
            bool displayManifestUpdates = false;
            if (Verbosity.VerbosityIsDetailedOrDiagnostic())
            {
                displayManifestUpdates = true;
            }
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(Reporter, _workloadResolver, PackageDownloader, _userProfileDir, TempDirectoryPath, 
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), _packageSourceLocation, displayManifestUpdates: displayManifestUpdates);

            ValidateWorkloadIdsInput();
        }

        private void ValidateWorkloadIdsInput()
        {
            var availableWorkloads = _workloadResolver.GetAvailableWorkloads();
            foreach (var workloadId in _workloadIds)
            {
                if (!availableWorkloads.Select(workload => workload.Id.ToString()).Contains(workloadId))
                {
                    if (_workloadResolver.IsPlatformIncompatibleWorkload(new WorkloadId(workloadId)))
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadNotSupportedOnPlatform, workloadId), isUserError: false);
                    }
                    else
                    {
                        throw new GracefulException(string.Format(LocalizableStrings.WorkloadNotRecognized, workloadId), isUserError: false);
                    }
                }
            }
        }

        public override int Execute()
        {
            bool usedRollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);
            if (_printDownloadLinkOnly)
            {
                Reporter.WriteLine(string.Format(LocalizableStrings.ResolvingPackageUrls, string.Join(", ", _workloadIds)));
                var packageUrls = GetPackageDownloadUrlsAsync(_workloadIds.Select(id => new WorkloadId(id)), _skipManifestUpdate, _includePreviews).GetAwaiter().GetResult();

                Reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                Reporter.WriteLine(JsonSerializer.Serialize(packageUrls));
                Reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
            }
            else if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
            {
                try
                {
                    DownloadToOfflineCacheAsync(_workloadIds.Select(id => new WorkloadId(id)), new DirectoryPath(_downloadToCacheOption), _skipManifestUpdate, _includePreviews).Wait();
                }
                catch (Exception e)
                {
                    _workloadInstaller.Shutdown();
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e, isUserError: false);
                }
            }
            else if (_skipManifestUpdate && usedRollback)
            {
                throw new GracefulException(string.Format(LocalizableStrings.CannotCombineSkipManifestAndRollback, 
                    WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, WorkloadInstallCommandParser.FromRollbackFileOption.Name,
                    WorkloadInstallCommandParser.SkipManifestUpdateOption.Name, WorkloadInstallCommandParser.FromRollbackFileOption.Name), isUserError: true);
            }
            else
            {
                try
                {
                    InstallWorkloads(
                        _workloadIds.Select(id => new WorkloadId(id)),
                        _skipManifestUpdate,
                        _includePreviews,
                        string.IsNullOrWhiteSpace(_fromCacheOption) ? null : new DirectoryPath(_fromCacheOption));
                }
                catch (Exception e)
                {
                    _workloadInstaller.Shutdown();
                    // Don't show entire stack trace
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadInstallationFailed, e.Message), e, isUserError: false);
                }
            }

            return _workloadInstaller.ExitCode;
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate = false, bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            Reporter.WriteLine();

            var manifestsToUpdate = Enumerable.Empty<ManifestVersionUpdate> ();
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

                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();
                manifestsToUpdate = string.IsNullOrWhiteSpace(_fromRollbackDefinition) ?
                    _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.manifestUpdate) :
                    _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition);
            }

            InstallWorkloadsWithInstallRecord(workloadIds, _sdkFeatureBand, manifestsToUpdate, offlineCache);

            TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, offlineCache);

            Reporter.WriteLine();
            Reporter.WriteLine(string.Format(LocalizableStrings.InstallationSucceeded, string.Join(" ", workloadIds)));
            Reporter.WriteLine();
        }

        internal static void TryRunGarbageCollection(IInstaller workloadInstaller, IReporter reporter, VerbosityOptions verbosity, DirectoryPath? offlineCache = null)
        {
            try
            {
                if (workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
                {
                    workloadInstaller.GetPackInstaller().GarbageCollectInstalledWorkloadPacks(offlineCache);
                }
            }
            catch (Exception e)
            {
                // Garbage collection failed, warn user
                reporter.WriteLine(string.Format(LocalizableStrings.GarbageCollectionFailed,
                    verbosity.VerbosityIsDetailedOrDiagnostic() ? e.StackTrace.ToString() : e.Message).Yellow());
            }
        }

        private void InstallWorkloadsWithInstallRecord(
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<ManifestVersionUpdate> manifestsToUpdate,
            DirectoryPath? offlineCache)
        {
            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();
                IEnumerable<PackInfo> workloadPackToInstall = new List<PackInfo>();
                IEnumerable<WorkloadId> newWorkloadInstallRecords = new List<WorkloadId>();

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

                        workloadPackToInstall = GetPacksToInstall(workloadIds);

                        installer.InstallWorkloadPacks(workloadPackToInstall, sdkFeatureBand, context, offlineCache);

                        var recordRepo = _workloadInstaller.GetWorkloadInstallationRecordRepository();
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
                            _workloadInstaller.GetWorkloadInstallationRecordRepository()
                                .DeleteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
                        }
                    });
            }
            else
            {
                var installer = _workloadInstaller.GetWorkloadInstaller();
                foreach (var workloadId in workloadIds)
                {
                    installer.InstallWorkload(workloadId);
                }
            }
        }

        private async Task<IEnumerable<string>> GetPackageDownloadUrlsAsync(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate, bool includePreview)
        {
            var packageUrls = new List<string>();
            DirectoryPath? tempPath = null;

            try
            {
                if (!skipManifestUpdate)
                {
                    var manifestPackageUrls = _workloadManifestUpdater.GetManifestPackageUrls(includePreview);
                    packageUrls.AddRange(manifestPackageUrls);

                    tempPath = new DirectoryPath(Path.Combine(TempDirectoryPath, "dotnet-manifest-extraction"));
                    await UseTempManifestsToResolvePacksAsync(tempPath.Value, includePreview);

                    var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion));
                    workloadIds = workloadIds.Concat(installedWorkloads).Distinct();
                }

                if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
                {
                    var installer = _workloadInstaller.GetPackInstaller();

                    var packUrls = GetPacksToInstall(workloadIds)
                        .Select(pack => PackageDownloader.GetPackageUrl(new PackageId(pack.ResolvedPackageId), new NuGetVersion(pack.Version),
                            packageSourceLocation: _packageSourceLocation, includePreview: includePreview).GetAwaiter().GetResult());
                    packageUrls.AddRange(packUrls);
                }
                else
                {
                    throw new NotImplementedException();
                }

                return packageUrls;
            }
            finally
            {
                if (tempPath != null && tempPath.HasValue && Directory.Exists(tempPath.Value.Value))
                {
                    Directory.Delete(tempPath.Value.Value, true);
                }
            }
        }

        private async Task UseTempManifestsToResolvePacksAsync(DirectoryPath tempPath, bool includePreview)
        {
            var manifestPackagePaths = await _workloadManifestUpdater.DownloadManifestPackagesAsync(includePreview, tempPath);
            if (manifestPackagePaths == null || !manifestPackagePaths.Any())
            {
                Reporter.WriteLine(LocalizableStrings.SkippingManifestUpdate);
                return;
            }
            await _workloadManifestUpdater.ExtractManifestPackagesToTempDirAsync(manifestPackagePaths, tempPath);
            var overlayProvider = new TempDirectoryWorkloadManifestProvider(tempPath.Value, _sdkVersion.ToString());
            _workloadResolver = _workloadResolver.CreateOverlayResolver(overlayProvider);
        }

        private async Task DownloadToOfflineCacheAsync(IEnumerable<WorkloadId> workloadIds, DirectoryPath offlineCache, bool skipManifestUpdate, bool includePreviews)
        {
            string tempManifestDir = null;
            if (!skipManifestUpdate)
            {
                var manifestPackagePaths = await _workloadManifestUpdater.DownloadManifestPackagesAsync(includePreviews, offlineCache);
                if (manifestPackagePaths != null && manifestPackagePaths.Any())
                {
                    tempManifestDir = Path.Combine(offlineCache.Value, "temp-manifests");
                    await _workloadManifestUpdater.ExtractManifestPackagesToTempDirAsync(manifestPackagePaths, new DirectoryPath(tempManifestDir));
                    var overlayProvider = new TempDirectoryWorkloadManifestProvider(tempManifestDir, _sdkVersion.ToString());
                    _workloadResolver = _workloadResolver.CreateOverlayResolver(overlayProvider);
                }
                else
                {
                    Reporter.WriteLine(LocalizableStrings.SkippingManifestUpdate);
                }

                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion));
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();
            }

            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();

                var workloadPacks = GetPacksToInstall(workloadIds);

                foreach (var pack in workloadPacks)
                {
                    installer.DownloadToOfflineCache(pack, offlineCache, includePreviews);
                }
            }
            else
            {
                var installer = _workloadInstaller.GetWorkloadInstaller();
                foreach (var workloadId in workloadIds)
                {
                    installer.DownloadToOfflineCache(workloadId, offlineCache, includePreviews);
                }
            }

            if (!string.IsNullOrWhiteSpace(tempManifestDir) && Directory.Exists(tempManifestDir))
            {
                Directory.Delete(tempManifestDir, true);
            }
        }

        private IEnumerable<PackInfo> GetPacksToInstall(IEnumerable<WorkloadId> workloadIds)
        {
            var installedPacks = _workloadInstaller.GetPackInstaller().GetInstalledPacks(_sdkFeatureBand);
            return workloadIds
                .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId))
                .Distinct()
                .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                .Where(pack => pack != null)
                .Where(pack => !installedPacks.Contains((pack.Id, pack.Version)));
        }
    }
}
