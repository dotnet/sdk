// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using NuGet.Versioning;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly bool _skipManifestUpdate;
        private readonly string _fromCacheOption;
        private readonly string _downloadToCacheOption;
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly bool _printDownloadLinkOnly;
        private readonly bool _includePreviews;
        private readonly VerbosityOptions _verbosity;
        private readonly IReadOnlyCollection<string> _workloadIds;
        private readonly IInstaller _workloadInstaller;
        private IWorkloadResolver _workloadResolver;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly ReleaseVersion _sdkVersion;
        private readonly SdkFeatureBand _sdkFeatureBand;
        private readonly string _userProfileDir;
        private readonly string _tempDirPath;
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
            : base(parseResult)
        {
            _reporter = reporter ?? Reporter.Output;
            _skipManifestUpdate = parseResult.GetValueForOption(WorkloadInstallCommandParser.SkipManifestUpdateOption);
            _includePreviews = parseResult.GetValueForOption(WorkloadInstallCommandParser.IncludePreviewOption);
            _printDownloadLinkOnly = parseResult.GetValueForOption(WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.FromCacheOption);
            _downloadToCacheOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.DownloadToCacheOption);
            _workloadIds = workloadIds ?? parseResult.GetValueForArgument(WorkloadInstallCommandParser.WorkloadIdArgument).ToList().AsReadOnly();
            _verbosity = parseResult.GetValueForOption(WorkloadInstallCommandParser.VerbosityOption);
            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _userProfileDir = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValueForOption(WorkloadInstallCommandParser.VersionOption), version, _dotnetPath, _userProfileDir);
            _sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _tempDirPath = tempDirPath ?? (string.IsNullOrWhiteSpace(parseResult.GetValueForOption(WorkloadInstallCommandParser.TempDirOption)) ?
                Path.GetTempPath() :
                parseResult.GetValueForOption(WorkloadInstallCommandParser.TempDirOption));
            _fromRollbackDefinition = parseResult.GetValueForOption(WorkloadInstallCommandParser.FromRollbackFileOption);

            var configOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValueForOption(WorkloadInstallCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: sourceOption);

            var sdkWorkloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString(), userProfileDir);
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(sdkWorkloadManifestProvider, _dotnetPath, _sdkVersion.ToString(), _userProfileDir);
            var tempPackagesDir = new DirectoryPath(Path.Combine(_tempDirPath, "dotnet-sdk-advertising-temp"));
            var restoreActionConfig = _parseResult.ToRestoreActionConfig();
            _nugetPackageDownloader = nugetPackageDownloader ??
                                      new NuGetPackageDownloader(tempPackagesDir,
                                          filePermissionSetter: null,
                                          new FirstPartyNuGetPackageSigningVerifier(tempPackagesDir, _verbosity.VerbosityIsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger()),
                                          _verbosity.VerbosityIsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger(), restoreActionConfig: restoreActionConfig);
            _workloadInstaller = workloadInstaller ??
                                 WorkloadInstallerFactory.GetWorkloadInstaller(_reporter, _sdkFeatureBand,
                                     _workloadResolver, _verbosity, _userProfileDir, _nugetPackageDownloader, _dotnetPath, _tempDirPath,
                                     _packageSourceLocation, restoreActionConfig, elevationRequired: !_printDownloadLinkOnly && string.IsNullOrWhiteSpace(_downloadToCacheOption));
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter, _workloadResolver, _nugetPackageDownloader, _userProfileDir, _tempDirPath, 
                _workloadInstaller.GetWorkloadInstallationRecordRepository(), _packageSourceLocation);

            ValidateWorkloadIdsInput();
        }

        void ValidateWorkloadIdsInput()
        {
            var availableWorkloads = _workloadResolver.GetAvailableWorkloads();
            foreach (var workloadId in _workloadIds)
            {
                if (availableWorkloads.Any(workload => workload.Id.ToString() == workloadId))
                    continue;

                var errorMsg = _workloadResolver.IsPlatformIncompatibleWorkload(new (workloadId)) ? 
                    string.Format(LocalizableStrings.WorkloadNotSupportedOnPlatform, workloadId) :
                    string.Format(LocalizableStrings.WorkloadNotRecognized, workloadId);
                
                throw new GracefulException(errorMsg, isUserError: false);
            }
        }

        int PrintDownloadLink() { 
            _reporter.WriteLine(string.Format(LocalizableStrings.ResolvingPackageUrls, string.Join(", ", _workloadIds)));
            var packageUrls = GetPackageDownloadUrlsAsync(_workloadIds.Select(id => new WorkloadId(id)), _skipManifestUpdate, _includePreviews).GetAwaiter().GetResult();

            _reporter.WriteLine("==allPackageLinksJsonOutputStart==");
            _reporter.WriteLine(JsonSerializer.Serialize(packageUrls));
            _reporter.WriteLine("==allPackageLinksJsonOutputEnd==");

            return _workloadInstaller.ExitCode;
        }

        int DownloadToCache()
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
                throw new GracefulException(string.Format(LocalizableStrings.WorkloadInstallationFailed, e.Message),
                    e, isUserError: false);
            }
            
            return _workloadInstaller.ExitCode;
        }

        public override int Execute()
        {
            if (_printDownloadLinkOnly)
            {
                return PrintDownloadLink();
            }

            if (string.IsNullOrWhiteSpace(_downloadToCacheOption))
            {
                return DownloadToCache();
            }
            
            try
            {
                DownloadToOfflineCacheAsync(_workloadIds.Select(id => new WorkloadId(id)),
                    new DirectoryPath(_downloadToCacheOption), _skipManifestUpdate, _includePreviews).Wait();
            }
            catch (Exception e)
            {
                _workloadInstaller.Shutdown();
                throw new GracefulException(
                    string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e,
                    isUserError: false);
            }

            return _workloadInstaller.ExitCode;
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate = false, bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            _reporter.WriteLine();

            IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> manifestsToUpdate = new List<(ManifestId, ManifestVersion, ManifestVersion)>();
            if (!skipManifestUpdate)
            {
                // Update currently installed workloads
                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);
                var previouslyInstalledWorkloads = installedWorkloads.Intersect(workloadIds);
                if (previouslyInstalledWorkloads.Any())
                {
                    _reporter.WriteLine(string.Format(LocalizableStrings.WorkloadAlreadyInstalled, string.Join(" ", previouslyInstalledWorkloads)).Yellow());
                }
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();

                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();
                manifestsToUpdate = string.IsNullOrWhiteSpace(_fromRollbackDefinition) ?
                    _workloadManifestUpdater.CalculateManifestUpdates().Select(m => (m.manifestId, m.existingVersion, m.newVersion)) :
                    _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition);
            }

            InstallWorkloadsWithInstallRecord(workloadIds, _sdkFeatureBand, manifestsToUpdate, offlineCache);

            TryRunGarbageCollection(_workloadInstaller, _reporter, _verbosity, offlineCache);

            _reporter.WriteLine();
            _reporter.WriteLine(string.Format(LocalizableStrings.InstallationSucceeded, string.Join(" ", workloadIds)));
            _reporter.WriteLine();
        }

        internal static void TryRunGarbageCollection(IInstaller workloadInstaller, IReporter reporter, VerbosityOptions verbosity, DirectoryPath? offlineCache = null)
        {
            if (workloadInstaller.GetInstallationUnit() != InstallationUnit.Packs)
                return;
            try
            {
                workloadInstaller.GetPackInstaller().GarbageCollectInstalledWorkloadPacks(offlineCache);
            }
            catch (Exception e)
            {
                // Garbage collection failed, warn user
                reporter.WriteLine(string.Format(LocalizableStrings.GarbageCollectionFailed,
                    verbosity.VerbosityIsDetailedOrDiagnostic() ? e.StackTrace : e.Message).Yellow());
            }
        }
        
        void InstallWorkloadsWithInstallRecord(IEnumerable<WorkloadId> workloadIds)
        {
            var installer = _workloadInstaller.GetWorkloadInstaller();
            foreach (var workloadId in workloadIds)
            {
                installer.InstallWorkload(workloadId);
            }
        }

        void InstallPacksWithInstallRecord(
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> manifestsToUpdate,
            DirectoryPath? offlineCache)
        {
            var installer = _workloadInstaller.GetPackInstaller();
            var workloadPackToInstall = Enumerable.Empty<PackInfo>();
            var newWorkloadInstallRecords = Enumerable.Empty<WorkloadId>();;

            TransactionalAction.Run(
                action: () =>
                {
                    bool rollback = !string.IsNullOrWhiteSpace(_fromRollbackDefinition);

                    foreach (var (manifestId, _, newVersion) in manifestsToUpdate)
                    {
                        _workloadInstaller.InstallWorkloadManifest(manifestId, newVersion, sdkFeatureBand, offlineCache, rollback);
                    }

                    _workloadResolver.RefreshWorkloadManifests();

                    workloadPackToInstall = GetPacksToInstall(workloadIds);

                    foreach (var packId in workloadPackToInstall)
                    {
                        installer.InstallWorkloadPack(packId, sdkFeatureBand, offlineCache);
                    }

                    var recordRepo = _workloadInstaller.GetWorkloadInstallationRecordRepository();
                    newWorkloadInstallRecords = workloadIds.Except(recordRepo.GetInstalledWorkloads(sdkFeatureBand));
                    foreach (var workloadId in newWorkloadInstallRecords)
                    {
                        recordRepo.WriteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
                    }
                },
                rollback: () =>
                {
                    try
                    {
                        _reporter.WriteLine(LocalizableStrings.RollingBackInstall);

                        foreach (var (manifestId, existingVersion, _) in manifestsToUpdate)
                        {
                            _workloadInstaller.InstallWorkloadManifest(manifestId, existingVersion, sdkFeatureBand, null, true);
                        }

                        foreach (var packId in workloadPackToInstall)
                        {
                            installer.RollBackWorkloadPackInstall(packId, sdkFeatureBand);
                        }

                        foreach (var workloadId in newWorkloadInstallRecords)
                        {
                            _workloadInstaller.GetWorkloadInstallationRecordRepository()
                                .DeleteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
                        }
                    }
                    catch (Exception e)
                    {
                        // Don't hide the original error if roll back fails
                        _reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, e.Message));
                    }
                });
        }

        void InstallWorkloadsWithInstallRecord(
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> manifestsToUpdate,
            DirectoryPath? offlineCache)
        {
            var installUnit = _workloadInstaller.GetInstallationUnit();
            switch(installUnit)
            {
                case InstallationUnit.Packs:
                    InstallPacksWithInstallRecord(workloadIds, sdkFeatureBand, manifestsToUpdate, offlineCache);
                    break;
                default:    
                    InstallWorkloadsWithInstallRecord(workloadIds);
                    break;
            }
        }

        async Task<IEnumerable<string>> GetPackageDownloadUrlsAsync(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate, bool includePreview)
        {
            var packageUrls = new List<string>();
            DirectoryPath? tempPath = null;

            try
            {
                if (!skipManifestUpdate)
                {
                    var manifestPackageUrls = _workloadManifestUpdater.GetManifestPackageUrls(includePreview);
                    packageUrls.AddRange(manifestPackageUrls);

                    tempPath = new DirectoryPath(Path.Combine(_tempDirPath, "dotnet-manifest-extraction"));
                    await UseTempManifestsToResolvePacksAsync(tempPath.Value, includePreview);

                    var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository()
                        .GetInstalledWorkloads(new(_sdkVersion));
                    workloadIds = workloadIds.Concat(installedWorkloads).Distinct();
                }

                switch (_workloadInstaller.GetInstallationUnit())
                {
                    case InstallationUnit.Packs:
                        // The select will return a list of tasks Task<string> from the packageDownloader,
                        // we then await for all of them to complete with Tasks.WhenAll and get an array of strings 
                        var packUrls = await Task.WhenAll(
                            GetPacksToInstall(workloadIds).Select(pack => 
                                _nugetPackageDownloader.GetPackageUrl(new(pack.ResolvedPackageId), new(pack.Version), _packageSourceLocation, includePreview)));
                        packageUrls.AddRange(packUrls);
                        return packageUrls;
                    default:
                        throw new NotImplementedException();
                }
            }
            finally
            {
                if ( Directory.Exists(tempPath?.Value)) 
                {
                    Directory.Delete(tempPath?.Value, true); // will not throw NRE thanks to Directory.Exists(null) => false
                }
            }
        }

        private async Task UseTempManifestsToResolvePacksAsync(DirectoryPath tempPath, bool includePreview)
        {
            var manifestPackagePaths = await _workloadManifestUpdater.DownloadManifestPackagesAsync(includePreview, tempPath);
            if (manifestPackagePaths == null || !manifestPackagePaths.Any())
            {
                _reporter.WriteLine(LocalizableStrings.SkippingManifestUpdate);
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
                    _reporter.WriteLine(LocalizableStrings.SkippingManifestUpdate);
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
