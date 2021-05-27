// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
        private IWorkloadManifestProvider _workloadManifestProvider;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _userHome;
        private readonly string _tempDirPath;
        private readonly string _dotnetPath;

        public WorkloadInstallCommand(
            ParseResult parseResult,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            IInstaller workloadInstaller = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            string dotnetDir = null,
            string userHome = null,
            string tempDirPath = null,
            string version = null)
            : base(parseResult)
        {
            _reporter = reporter ?? Reporter.Output;
            _skipManifestUpdate = parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.SkipManifestUpdateOption);
            _includePreviews = parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.IncludePreviewOption);
            _printDownloadLinkOnly = parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.ValueForOption<string>(WorkloadInstallCommandParser.FromCacheOption);
            _downloadToCacheOption = parseResult.ValueForOption<string>(WorkloadInstallCommandParser.DownloadToCacheOption);
            _workloadIds = parseResult.ValueForArgument<IEnumerable<string>>(WorkloadInstallCommandParser.WorkloadIdArgument).ToList().AsReadOnly();
            _verbosity = parseResult.ValueForOption<VerbosityOptions>(WorkloadInstallCommandParser.VerbosityOption);
            _sdkVersion = string.IsNullOrEmpty(parseResult.ValueForOption<string>(WorkloadInstallCommandParser.VersionOption)) ?
                new ReleaseVersion(version ?? Product.Version) :
                new ReleaseVersion(parseResult.ValueForOption<string>(WorkloadInstallCommandParser.VersionOption));
            _tempDirPath = tempDirPath ?? (string.IsNullOrWhiteSpace(parseResult.ValueForOption<string>(WorkloadInstallCommandParser.TempDirOption)) ?
                Path.GetTempPath() :
                parseResult.ValueForOption<string>(WorkloadInstallCommandParser.TempDirOption));

            var configOption = parseResult.ValueForOption<string>(WorkloadInstallCommandParser.ConfigOption);
            var addSourceOption = parseResult.ValueForOption<string[]>(WorkloadInstallCommandParser.AddSourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (addSourceOption == null || !addSourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: addSourceOption);

            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString());
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(_workloadManifestProvider, _dotnetPath, _sdkVersion.ToString());
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadInstaller = workloadInstaller ?? 
                WorkloadInstallerFactory.GetWorkloadInstaller(_reporter, sdkFeatureBand, _workloadResolver, _verbosity, nugetPackageDownloader, _dotnetPath, _packageSourceLocation);
            _userHome = userHome ?? CliFolderPathCalculator.DotnetHomePath;
            var tempPackagesDir = new DirectoryPath(Path.Combine(_tempDirPath, "dotnet-sdk-advertising-temp"));
            _nugetPackageDownloader = nugetPackageDownloader ?? new NuGetPackageDownloader(tempPackagesDir, filePermissionSetter: null, new NullLogger());
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter, _workloadManifestProvider, _nugetPackageDownloader, _userHome, _tempDirPath, _packageSourceLocation);
        }

        public override int Execute()
        {
            if (_printDownloadLinkOnly)
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.ResolvingPackageUrls, string.Join(", ", _workloadIds)));
                var packageUrls = GetPackageDownloadUrlsAsync(_workloadIds.Select(id => new WorkloadId(id)), _skipManifestUpdate, _includePreviews).Result;

                _reporter.WriteLine("==allPackageLinksJsonOutputStart==");
                _reporter.WriteLine(JsonSerializer.Serialize(packageUrls));
                _reporter.WriteLine("==allPackageLinksJsonOutputEnd==");
            }
			else if (!string.IsNullOrWhiteSpace(_downloadToCacheOption))
            {
                try
                {
                    DownloadToOfflineCacheAsync(_workloadIds.Select(id => new WorkloadId(id)), new DirectoryPath(_downloadToCacheOption), _skipManifestUpdate, _includePreviews).Wait();
                }
                catch (Exception e)
                {
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadCacheDownloadFailed, e.Message), e);
                }
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
                    // Don't show entire stack trace
                    throw new GracefulException(string.Format(LocalizableStrings.WorkloadInstallationFailed, e.Message), e);
                }
            }

            return 0;
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate = false, bool includePreviews = false, DirectoryPath? offlineCache = null)
        {
            _reporter.WriteLine();
            var featureBand = new SdkFeatureBand(string.Join('.', _sdkVersion.Major, _sdkVersion.Minor, _sdkVersion.SdkFeatureBand));

            IEnumerable<(ManifestId, ManifestVersion, ManifestVersion)> manifestsToUpdate = new List<(ManifestId,  ManifestVersion, ManifestVersion)>();
            if (!skipManifestUpdate)
            {
                // Update currently installed workloads
                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(featureBand);
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();

                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(includePreviews, offlineCache).Wait();
                manifestsToUpdate = _workloadManifestUpdater.CalculateManifestUpdates();
            }

            InstallWorkloadsWithInstallRecord(workloadIds, featureBand, manifestsToUpdate, offlineCache);

            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                _workloadInstaller.GetPackInstaller().GarbageCollectInstalledWorkloadPacks();
            }

            _reporter.WriteLine();
            _reporter.WriteLine(string.Format(LocalizableStrings.InstallationSucceeded, string.Join(" ", workloadIds)));
            _reporter.WriteLine();
        }

        private void InstallWorkloadsWithInstallRecord(
            IEnumerable<WorkloadId> workloadIds,
            SdkFeatureBand sdkFeatureBand,
            IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> manifestsToUpdate,
			DirectoryPath? offlineCache)
        {
            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();
                IEnumerable<PackInfo> workloadPackToInstall = new List<PackInfo>();

                TransactionalAction.Run(
                    action: () =>
                    {
                        foreach (var manifest in manifestsToUpdate)
                        {
                            _workloadInstaller.InstallWorkloadManifest(manifest.manifestId, manifest.newVersion, sdkFeatureBand, offlineCache);
                        }

                        _workloadResolver.RefreshWorkloadManifests();

                        workloadPackToInstall = workloadIds
                            .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                            .Distinct()
                            .Select(packId => _workloadResolver.TryGetPackInfo(packId));

                        foreach (var packId in workloadPackToInstall)
                        {
                            installer.InstallWorkloadPack(packId, sdkFeatureBand, offlineCache);
                        }

                        foreach (var workloadId in workloadIds)
                        {
                            _workloadInstaller.GetWorkloadInstallationRecordRepository()
                                .WriteWorkloadInstallationRecord(workloadId, sdkFeatureBand);
                        }
                    },
                    rollback: () => {
                        try
                        {
                             _reporter.WriteLine(LocalizableStrings.RollingBackInstall);
							
							 foreach (var manifest in manifestsToUpdate)
                             {
                                 _workloadInstaller.InstallWorkloadManifest(manifest.manifestId, manifest.existingVersion, sdkFeatureBand);
                             }
							
                            foreach (var packId in workloadPackToInstall)
                            {
                                installer.RollBackWorkloadPackInstall(packId, sdkFeatureBand);
                            }

                            foreach (var workloadId in workloadIds)
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

                    tempPath = new DirectoryPath(Path.Combine(_tempDirPath, "dotnet-manifest-extraction"));
                    await UseTempManifestsToResolvePacksAsync(tempPath.Value, includePreview);

                    var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion));
                    workloadIds = workloadIds.Concat(installedWorkloads).Distinct();
                }

                if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
                {
                    var installer = _workloadInstaller.GetPackInstaller();

                    var packUrls = workloadIds
                        .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                        .Distinct()
                        .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                        .Where(pack => pack != null)
                        .Select(pack => _nugetPackageDownloader.GetPackageUrl(new PackageId(pack.ResolvedPackageId), new NuGetVersion(pack.Version),
                            packageSourceLocation: _packageSourceLocation, includePreview: includePreview).Result);
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
            await _workloadManifestUpdater.ExtractManifestPackagesToTempDirAsync(manifestPackagePaths, tempPath);
            _workloadManifestProvider = new TempDirectoryWorkloadManifestProvider(tempPath.Value, _sdkVersion.ToString());
            _workloadResolver = WorkloadResolver.Create(_workloadManifestProvider, _dotnetPath, _sdkVersion.ToString());
        }

        private async Task DownloadToOfflineCacheAsync(IEnumerable<WorkloadId> workloadIds, DirectoryPath offlineCache, bool skipManifestUpdate, bool includePreviews)
        {
            string tempManifestDir = null;
            if (!skipManifestUpdate)
            {
                var manifestPackagePaths = await _workloadManifestUpdater.DownloadManifestPackagesAsync(includePreviews, offlineCache);
                tempManifestDir = Path.Combine(offlineCache.Value, "temp-manifests");
                await _workloadManifestUpdater.ExtractManifestPackagesToTempDirAsync(manifestPackagePaths, new DirectoryPath(tempManifestDir));
                _workloadManifestProvider = new TempDirectoryWorkloadManifestProvider(tempManifestDir, _sdkVersion.ToString());
                _workloadResolver = WorkloadResolver.Create(_workloadManifestProvider, _dotnetPath, _sdkVersion.ToString());

                var installedWorkloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(new SdkFeatureBand(_sdkVersion));
                workloadIds = workloadIds.Concat(installedWorkloads).Distinct();
            }

            if (_workloadInstaller.GetInstallationUnit().Equals(InstallationUnit.Packs))
            {
                var installer = _workloadInstaller.GetPackInstaller();

                var workloadPacks = workloadIds
                    .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId.ToString()))
                    .Distinct()
                    .Select(packId => _workloadResolver.TryGetPackInfo(packId));

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
    }
}
