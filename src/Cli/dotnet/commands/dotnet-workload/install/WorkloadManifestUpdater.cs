// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Versioning;
using WorkloadCollection = System.Collections.Generic.Dictionary<Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadId, Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadDefinition>;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadManifestUpdater : IWorkloadManifestUpdater
    {
        private readonly IReporter _reporter;
        private readonly IWorkloadResolver _workloadResolver;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly SdkFeatureBand _sdkFeatureBand;
        private readonly string _userProfileDir;
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly Func<string, string> _getEnvironmentVariable;
        private readonly IWorkloadInstallationRecordRepository _workloadRecordRepo;
        private readonly IWorkloadManifestInstaller _workloadManifestInstaller;
        private readonly bool _displayManifestUpdates;

        public WorkloadManifestUpdater(IReporter reporter,
            IWorkloadResolver workloadResolver,
            INuGetPackageDownloader nugetPackageDownloader,
            string userProfileDir,
            IWorkloadInstallationRecordRepository workloadRecordRepo,
            IWorkloadManifestInstaller workloadManifestInstaller,
            PackageSourceLocation packageSourceLocation = null,
            Func<string, string> getEnvironmentVariable = null,
            bool displayManifestUpdates = true,
            SdkFeatureBand? sdkFeatureBand = null)
        {
            _reporter = reporter;
            _workloadResolver = workloadResolver;
            _userProfileDir = userProfileDir;
            _nugetPackageDownloader = nugetPackageDownloader;
            _sdkFeatureBand = sdkFeatureBand ?? new SdkFeatureBand(_workloadResolver.GetSdkFeatureBand());
            _packageSourceLocation = packageSourceLocation;
            _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
            _workloadRecordRepo = workloadRecordRepo;
            _workloadManifestInstaller = workloadManifestInstaller;
            _displayManifestUpdates = displayManifestUpdates;
        }

        private static WorkloadManifestUpdater GetInstance(string userProfileDir)
        {
            var reporter = new NullReporter();
            var dotnetPath = Path.GetDirectoryName(Environment.ProcessPath);
            var sdkVersion = Product.Version;
            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, sdkVersion, userProfileDir, SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(Environment.CurrentDirectory));
            var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetPath, sdkVersion, userProfileDir);
            var tempPackagesDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());
            var nugetPackageDownloader = new NuGetPackageDownloader(tempPackagesDir,
                                          filePermissionSetter: null,
                                          new FirstPartyNuGetPackageSigningVerifier(),
                                          new NullLogger(),
                                          reporter,
                                          verifySignatures: SignCheck.IsDotNetSigned());
            var installer = WorkloadInstallerFactory.GetWorkloadInstaller(reporter, new SdkFeatureBand(sdkVersion),
                workloadResolver, VerbosityOptions.normal, userProfileDir, verifySignatures: false);
            var workloadRecordRepo = installer.GetWorkloadInstallationRecordRepository();

            return new WorkloadManifestUpdater(reporter, workloadResolver, nugetPackageDownloader, userProfileDir, workloadRecordRepo, installer);
        }

        public async Task UpdateAdvertisingManifestsAsync(bool includePreviews, DirectoryPath? offlineCache = null)
        {
            // this updates all the manifests 
            var manifests = _workloadResolver.GetInstalledManifests();
            await Task.WhenAll(manifests.Select(manifest => UpdateAdvertisingManifestAsync(manifest, includePreviews, offlineCache))).ConfigureAwait(false);
            WriteUpdatableWorkloadsFile();
        }

        public static async Task BackgroundUpdateAdvertisingManifestsAsync(string userProfileDir)
        {
            try
            {
                var manifestUpdater = GetInstance(userProfileDir);
                await manifestUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync();
            }
            catch (Exception)
            {
                // Never surface messages on background updates
            }
        }

        public async Task BackgroundUpdateAdvertisingManifestsWhenRequiredAsync()
        {
            if (!BackgroundUpdatesAreDisabled() &&
                AdManifestSentinelIsDueForUpdate() &&
                UpdatedAdManifestPackagesExistAsync().GetAwaiter().GetResult())
            {
                await UpdateAdvertisingManifestsAsync(false);
                var sentinelPath = GetAdvertisingManifestSentinelPath(_sdkFeatureBand);
                if (File.Exists(sentinelPath))
                {
                    File.SetLastAccessTime(sentinelPath, DateTime.Now);
                }
                else
                {
                    File.Create(sentinelPath).Close();
                }
            }
        }

        private void WriteUpdatableWorkloadsFile()
        {
            var installedWorkloads = _workloadRecordRepo.GetInstalledWorkloads(_sdkFeatureBand);
            var updatableWorkloads = GetUpdatableWorkloadsToAdvertise(installedWorkloads);
            var filePath = GetAdvertisingWorkloadsFilePath(_sdkFeatureBand);
            var jsonContent = JsonSerializer.Serialize(updatableWorkloads.Select(workload => workload.ToString()).ToArray());
            if (Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            File.WriteAllText(filePath, jsonContent);
        }

        public void DeleteUpdatableWorkloadsFile()
        {
            var filePath = GetAdvertisingWorkloadsFilePath(_sdkFeatureBand);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        public static void AdvertiseWorkloadUpdates()
        {
            try
            {
                var backgroundUpdatesDisabled = bool.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_DISABLE), out var disableEnvVar) && disableEnvVar;
                SdkFeatureBand featureBand = new(Product.Version);
                var adUpdatesFile = GetAdvertisingWorkloadsFilePath(CliFolderPathCalculator.DotnetUserProfileFolderPath, featureBand);
                if (!backgroundUpdatesDisabled && File.Exists(adUpdatesFile))
                {
                    var updatableWorkloads = JsonSerializer.Deserialize<string[]>(File.ReadAllText(adUpdatesFile));
                    if (updatableWorkloads != null && updatableWorkloads.Any())
                    {
                        Console.WriteLine();
                        Console.WriteLine(LocalizableStrings.WorkloadUpdatesAvailable);
                    }
                }
            }
            catch (Exception)
            {
                // Never surface errors
            }
        }

        public IEnumerable<ManifestUpdateWithWorkloads> CalculateManifestUpdates()
        {
            var currentManifestIds = GetInstalledManifestIds();
            foreach (var manifestId in currentManifestIds)
            {
                var advertisingInfo = GetAdvertisingManifestVersionAndWorkloads(manifestId);
                if (advertisingInfo == null)
                {
                    continue;
                }

                var (installedVersion, installedBand) = GetInstalledManifestVersion(manifestId);
                var ((adVersion, adBand), adWorkloads) = advertisingInfo.Value;
                if ((adVersion.CompareTo(installedVersion) > 0 && adBand.Equals(installedBand)) ||
                    adBand.CompareTo(installedBand) > 0)
                {
                    var update = new ManifestVersionUpdate(manifestId, installedVersion, installedBand.ToString(), adVersion, adBand.ToString());
                    yield return new(update, adWorkloads);
                }
            }
        }

        public IEnumerable<WorkloadId> GetUpdatableWorkloadsToAdvertise(IEnumerable<WorkloadId> installedWorkloads)
        {
            try
            {
                var overlayProvider = new TempDirectoryWorkloadManifestProvider(Path.Combine(_userProfileDir, "sdk-advertising", _sdkFeatureBand.ToString()), _sdkFeatureBand.ToString());
                var advertisingManifestResolver = _workloadResolver.CreateOverlayResolver(overlayProvider);
                return _workloadResolver.GetUpdatedWorkloads(advertisingManifestResolver, installedWorkloads);
            }
            catch
            {
                return Array.Empty<WorkloadId>();
            }
        }

        public IEnumerable<ManifestVersionUpdate> CalculateManifestRollbacks(string rollbackDefinitionFilePath)
        {
            var currentManifestIds = GetInstalledManifestIds();
            var manifestRollbacks = ParseRollbackDefinitionFile(rollbackDefinitionFilePath);

            var unrecognizedManifestIds = manifestRollbacks.Where(rollbackManifest => !currentManifestIds.Contains(rollbackManifest.Id));
            if (unrecognizedManifestIds.Any())
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.RollbackDefinitionContainsExtraneousManifestIds, rollbackDefinitionFilePath, string.Join(" ", unrecognizedManifestIds)).Yellow());
                manifestRollbacks = manifestRollbacks.Where(rollbackManifest => currentManifestIds.Contains(rollbackManifest.Id));
            }

            var manifestUpdates = manifestRollbacks.Select(manifest =>
            {
                var (id, (version, band)) = manifest;
                var (installedVersion, installedBand) = GetInstalledManifestVersion(id);
                return new ManifestVersionUpdate(id, installedVersion, installedBand.ToString(), version, band.ToString());
            });

            return manifestUpdates;
        }

        public async Task<IEnumerable<WorkloadDownload>> GetManifestPackageDownloadsAsync(bool includePreviews, SdkFeatureBand providedSdkFeatureBand, SdkFeatureBand installedSdkFeatureBand)
        {
            var downloads = new List<WorkloadDownload>();
            foreach (var manifest in _workloadResolver.GetInstalledManifests())
            {
                try
                {
                    PackageId? providedPackageId = null;
                    var fallbackFeatureBand = new SdkFeatureBand(manifest.ManifestFeatureBand);
                    // The bands should be checked in the order defined here.
                    SdkFeatureBand[] bands = [providedSdkFeatureBand, installedSdkFeatureBand, fallbackFeatureBand];
                    var success = false;
                    // Use Distinct to eliminate bands that are the same.
                    foreach (var band in bands.Distinct())
                    {
                        var packageId = _workloadManifestInstaller.GetManifestPackageId(new ManifestId(manifest.Id), band);
                        providedPackageId ??= packageId;

                        try
                        {
                            var latestVersion = await _nugetPackageDownloader.GetLatestPackageVersion(packageId, _packageSourceLocation, includePreviews);
                            success = true;
                            downloads.Add(new WorkloadDownload(manifest.Id, packageId.ToString(), latestVersion.ToString()));
                            break;
                        }
                        catch (NuGetPackageNotFoundException)
                        {
                        }
                    }

                    if (!success)
                    {
                        _reporter.WriteLine(LocalizableStrings.ManifestPackageUrlNotResolved, providedPackageId);
                    }
                }
                catch
                {
                    _reporter.WriteLine(LocalizableStrings.ManifestPackageUrlNotResolved, manifest.Id);
                }
            }

            return downloads;
        }

        private IEnumerable<ManifestId> GetInstalledManifestIds() => _workloadResolver.GetInstalledManifests().Select(manifest => new ManifestId(manifest.Id));

        private async Task UpdateAdvertisingManifestAsync(WorkloadManifestInfo manifest, bool includePreviews, DirectoryPath? offlineCache = null)
        {
            string packagePath = null;
            var manifestId = new ManifestId(manifest.Id);
            try
            {
                SdkFeatureBand? currentFeatureBand = null;
                var fallbackFeatureBand = new SdkFeatureBand(manifest.ManifestFeatureBand);
                // The bands should be checked in the order defined here.
                SdkFeatureBand[] bands = [_sdkFeatureBand, fallbackFeatureBand];
                var success = false;
                // Use Distinct to eliminate bands that are the same.
                foreach (var band in bands.Distinct())
                {
                    var manifestPackageId = _workloadManifestInstaller.GetManifestPackageId(manifestId, band);
                    currentFeatureBand = band;

                    try
                    {
                        // If an offline cache is present, use that. Otherwise, try to acquire the package online.
                        packagePath = offlineCache != null ?
                            Directory.GetFiles(offlineCache.Value.Value)
                                .Where(path => path.EndsWith(".nupkg") && Path.GetFileName(path).StartsWith(manifestPackageId.ToString(), StringComparison.OrdinalIgnoreCase))
                                .Max() :
                            await _nugetPackageDownloader.DownloadPackageAsync(manifestPackageId, packageSourceLocation: _packageSourceLocation, includePreview: includePreviews);

                        if (packagePath != null)
                        {
                            success = true;
                            break;
                        }
                    }
                    catch (NuGetPackageNotFoundException)
                    {
                    }
                }

                if (!success)
                {
                    _reporter.WriteLine(LocalizableStrings.AdManifestPackageDoesNotExist, manifestId);
                    return;
                }

                var adManifestPath = GetAdvertisingManifestPath(_sdkFeatureBand, manifestId);
                await _workloadManifestInstaller.ExtractManifestAsync(packagePath, adManifestPath);

                // add file that contains the advertised manifest feature band so GetAdvertisingManifestVersionAndWorkloads will use correct feature band, regardless of if rollback occurred or not
                File.WriteAllText(Path.Combine(adManifestPath, "AdvertisedManifestFeatureBand.txt"), currentFeatureBand);

                if (_displayManifestUpdates)
                {
                    _reporter.WriteLine(LocalizableStrings.AdManifestUpdated, manifestId);
                }

            }
            catch (Exception e)
            {
                _reporter.WriteLine(LocalizableStrings.FailedAdManifestUpdate, manifestId, e.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(packagePath) && File.Exists(packagePath) && (offlineCache == null || !offlineCache.HasValue))
                {
                    File.Delete(packagePath);
                }
                if (!string.IsNullOrEmpty(packagePath) && (offlineCache == null || !offlineCache.HasValue))
                {
                    var versionDir = Path.GetDirectoryName(packagePath);

                    if (Directory.Exists(versionDir) && !Directory.GetFileSystemEntries(versionDir).Any())
                    {
                        Directory.Delete(versionDir);
                        var idDir = Path.GetDirectoryName(versionDir);
                        if (Directory.Exists(idDir) && !Directory.GetFileSystemEntries(idDir).Any())
                        {
                            Directory.Delete(idDir);
                        }
                    }
                }
            }
        }

        private (ManifestVersionWithBand ManifestWithBand, WorkloadCollection Workloads)? GetAdvertisingManifestVersionAndWorkloads(ManifestId manifestId)
        {
            var manifestPath = Path.Combine(GetAdvertisingManifestPath(_sdkFeatureBand, manifestId), "WorkloadManifest.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            using (FileStream fsSource = new(manifestPath, FileMode.Open, FileAccess.Read))
            {
                adManifestFeatureBand = new SdkFeatureBand(File.ReadAllText(adManifestFeatureBandPath));
            }

            ManifestVersionWithBand manifestWithBand = new(new ManifestVersion(manifest.Version), adManifestFeatureBand);
            var workloads = manifest.Workloads.Values.OfType<WorkloadDefinition>().ToDictionary(w => w.Id);
            return (manifestWithBand, workloads);
        }

        private ManifestVersionWithBand GetInstalledManifestVersion(ManifestId manifestId)
        {
            var manifest = _workloadResolver.GetInstalledManifests().FirstOrDefault(manifest => manifest.Id.ToLowerInvariant().Equals(manifestId.ToString()));
            if (manifest == null)
            {
                throw new Exception(string.Format(LocalizableStrings.ManifestDoesNotExist, manifestId.ToString()));
            }
            return new(new ManifestVersion(manifest.Version), new SdkFeatureBand(manifest.ManifestFeatureBand));
        }

        private bool AdManifestSentinelIsDueForUpdate()
        {
            var sentinelPath = GetAdvertisingManifestSentinelPath(_sdkFeatureBand);
            if (!int.TryParse(_getEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS), out int updateIntervalHours))
            {
                updateIntervalHours = 24;
            }

            if (File.Exists(sentinelPath))
            {
                var lastAccessTime = File.GetLastAccessTime(sentinelPath);
                if (lastAccessTime.AddHours(updateIntervalHours) > DateTime.Now)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> UpdatedAdManifestPackagesExistAsync()
        {
            var manifests = GetInstalledManifestIds();
            var availableUpdates = await Task.WhenAll(manifests.Select(manifest => NewerManifestPackageExists(manifest))).ConfigureAwait(false);
            return availableUpdates.Any();
        }

        private async Task<bool> NewerManifestPackageExists(ManifestId manifest)
        {
            try
            {
                var currentVersion = NuGetVersion.Parse(_workloadResolver.GetManifestVersion(manifest.ToString()));
                var latestVersion = await _nugetPackageDownloader.GetLatestPackageVersion(_workloadManifestInstaller.GetManifestPackageId(manifest, _sdkFeatureBand));
                return latestVersion > currentVersion;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IEnumerable<(ManifestId Id, ManifestVersionWithBand ManifestWithBand)> ParseRollbackDefinitionFile(string rollbackDefinitionFilePath)
        {
            string fileContent;

            if (Uri.TryCreate(rollbackDefinitionFilePath, UriKind.Absolute, out var rollbackUri) && !rollbackUri.IsFile)
            {
                fileContent = (new HttpClient()).GetStringAsync(rollbackDefinitionFilePath).Result;
            }
            else
            {
                if (File.Exists(rollbackDefinitionFilePath))
                {
                    fileContent = File.ReadAllText(rollbackDefinitionFilePath);
                }
                else
                {
                    throw new ArgumentException(string.Format(LocalizableStrings.RollbackDefinitionFileDoesNotExist, rollbackDefinitionFilePath));
                }
            }

            var versions = WorkloadSet.FromJson(fileContent, _sdkFeatureBand).ManifestVersions;
            return versions.Select(kvp => (kvp.Key, new ManifestVersionWithBand(kvp.Value.Version, kvp.Value.FeatureBand)));
        }

        private bool BackgroundUpdatesAreDisabled() => bool.TryParse(_getEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_DISABLE), out var disableEnvVar) && disableEnvVar;

        private string GetAdvertisingManifestSentinelPath(SdkFeatureBand featureBand) => Path.Combine(_userProfileDir, $".workloadAdvertisingManifestSentinel{featureBand}");

        private string GetAdvertisingWorkloadsFilePath(SdkFeatureBand featureBand) => GetAdvertisingWorkloadsFilePath(_userProfileDir, featureBand);

        private static string GetAdvertisingWorkloadsFilePath(string userProfileDir, SdkFeatureBand featureBand) => Path.Combine(userProfileDir, $".workloadAdvertisingUpdates{featureBand}");

        private async Task<string> GetOnlinePackagePath(SdkFeatureBand sdkFeatureBand, ManifestId manifestId, bool includePreviews)
        {
            string packagePath = await _nugetPackageDownloader.DownloadPackageAsync(
                _workloadManifestInstaller.GetManifestPackageId(manifestId, sdkFeatureBand),
                packageSourceLocation: _packageSourceLocation,
                includePreview: includePreviews);
        }
        private string GetAdvertisingManifestPath(SdkFeatureBand featureBand, ManifestId manifestId) => Path.Combine(_userProfileDir, "sdk-advertising", featureBand.ToString(), manifestId.ToString());
        private record ManifestVersionWithBand(ManifestVersion Version, SdkFeatureBand Band);
    }
}
