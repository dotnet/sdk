// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.History;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadManifestUpdater : IWorkloadManifestUpdater
    {
        public static readonly string WorkloadSetManifestId = "Microsoft.NET.Workloads";

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

        public async Task UpdateAdvertisingManifestsAsync(bool includePreviews, bool useWorkloadSets = false, DirectoryPath? offlineCache = null)
        {
            if (useWorkloadSets)
            {
                await UpdateManifestWithVersionAsync("Microsoft.NET.Workloads", includePreviews, _sdkFeatureBand, null, offlineCache);
            }
            else
            {
                // this updates all the manifests 
                var manifests = _workloadResolver.GetInstalledManifests();
                await Task.WhenAll(manifests.Select(manifest => UpdateAdvertisingManifestAsync(manifest, includePreviews, offlineCache))).ConfigureAwait(false);
                WriteUpdatableWorkloadsFile();
            }
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
                await UpdateAdvertisingManifestsAsync(false, ShouldUseWorkloadSetMode(_sdkFeatureBand, _userProfileDir));
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

        public static bool ShouldUseWorkloadSetMode(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        {
            string path = Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, dotnetDir), "default.json");
            var installStateContents = File.Exists(path) ? InstallStateContents.FromString(File.ReadAllText(path)) : new InstallStateContents();
            return installStateContents.UseWorkloadSets ?? false;
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

        public string GetAdvertisedWorkloadSetVersion()
        {
            var advertisedPath = GetAdvertisingManifestPath(_sdkFeatureBand, new ManifestId(WorkloadSetManifestId));
            var workloadSetVersionFilePath = Path.Combine(advertisedPath, Constants.workloadSetVersionFileName);
            if (File.Exists(workloadSetVersionFilePath))
            {
                return File.ReadAllText(workloadSetVersionFilePath);
            }
            return null;
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
                    var update = new ManifestVersionUpdate(manifestId, adVersion, adBand.ToString());
                    yield return new(update, adWorkloads);
                }
            }
        }

        public IEnumerable<WorkloadId> GetUpdatableWorkloadsToAdvertise(IEnumerable<WorkloadId> installedWorkloads)
        {
            try
            {
#if !DOT_NET_BUILD_FROM_SOURCE
                if (OperatingSystem.IsWindows())
                {
                    //  Also advertise updates for workloads installed by Visual Studio
                    InstalledWorkloadsCollection installedVSWorkloads = new InstalledWorkloadsCollection();
                    VisualStudioWorkloads.GetInstalledWorkloads(_workloadResolver, installedVSWorkloads, _sdkFeatureBand);
                    installedWorkloads = installedWorkloads.Concat(installedVSWorkloads.AsEnumerable().Select(kvp => new WorkloadId(kvp.Key))).Distinct().ToList();
                }
#endif

                var overlayProvider = new TempDirectoryWorkloadManifestProvider(Path.Combine(_userProfileDir, "sdk-advertising", _sdkFeatureBand.ToString()), _sdkFeatureBand.ToString());
                var advertisingManifestResolver = _workloadResolver.CreateOverlayResolver(overlayProvider);
                return _workloadResolver.GetUpdatedWorkloads(advertisingManifestResolver, installedWorkloads);
            }
            catch
            {
                return Array.Empty<WorkloadId>();
            }
        }

        public IEnumerable<ManifestVersionUpdate> CalculateManifestRollbacks(string rollbackDefinitionFilePath, WorkloadHistoryRecorder recorder = null)
        {
            var currentManifestIds = GetInstalledManifestIds();
            var manifestRollbacks = ParseRollbackDefinitionFile(rollbackDefinitionFilePath, _sdkFeatureBand);

            if (recorder is not null)
            {
                recorder.HistoryRecord.RollbackFileContents = manifestRollbacks.ToDictionary(kvp => kvp.Id.ToString(), kvp => kvp.ManifestWithBand.Version + "/" + kvp.ManifestWithBand.Band);
            }

            var unrecognizedManifestIds = manifestRollbacks.Where(rollbackManifest => !currentManifestIds.Contains(rollbackManifest.Id));
            if (unrecognizedManifestIds.Any())
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.RollbackDefinitionContainsExtraneousManifestIds, rollbackDefinitionFilePath, string.Join(" ", unrecognizedManifestIds)).Yellow());
                manifestRollbacks = manifestRollbacks.Where(rollbackManifest => currentManifestIds.Contains(rollbackManifest.Id));
            }

            return CalculateManifestRollbacks(manifestRollbacks);
        }

        private static IEnumerable<ManifestVersionUpdate> CalculateManifestRollbacks(IEnumerable<(ManifestId Id, ManifestVersionWithBand ManifestWithBand)> versionUpdates)
        {
            return versionUpdates.Select(manifest =>
            {
                var (id, (version, band)) = manifest;
                return new ManifestVersionUpdate(id, version, band.ToString());
            });
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

        private async Task<bool> UpdateManifestWithVersionAsync(string id, bool includePreviews, SdkFeatureBand band, NuGetVersion packageVersion = null, DirectoryPath? offlineCache = null)
        {
            var manifestId = new ManifestId(id);
            string packagePath = null;
            try
            {
                var manifestPackageId = _workloadManifestInstaller.GetManifestPackageId(manifestId, band);
                try
                {
                    // If an offline cache is present, use that. Otherwise, try to acquire the package online.
                    packagePath = offlineCache != null ?
                        Directory.GetFiles(offlineCache.Value.Value)
                            .Where(path =>
                            path.EndsWith(".nupkg") &&
                            Path.GetFileName(path).StartsWith(manifestPackageId.ToString(), StringComparison.OrdinalIgnoreCase) &&
                            (packageVersion == null || path.Contains(packageVersion.ToString())))
                            .Max() :
                        await _nugetPackageDownloader.DownloadPackageAsync(manifestPackageId, packageVersion: packageVersion, packageSourceLocation: _packageSourceLocation, includePreview: includePreviews);
                }
                catch (NuGetPackageNotFoundException)
                {
                }

                if (packagePath is null)
                {
                    return false;
                }

                var adManifestPath = GetAdvertisingManifestPath(_sdkFeatureBand, manifestId);
                await _workloadManifestInstaller.ExtractManifestAsync(packagePath, adManifestPath);

                // add file that contains the advertised manifest feature band so GetAdvertisingManifestVersionAndWorkloads will use correct feature band, regardless of if rollback occurred or not
                File.WriteAllText(Path.Combine(adManifestPath, "AdvertisedManifestFeatureBand.txt"), band.ToString());

                if (id.Equals(WorkloadSetManifestId))
                {
                    // Create version file later used as part of installing the workload set in the file-based installer and in the msi-based installer
                    using PackageArchiveReader packageReader = new(packagePath);
                    var downloadedPackageVersion = packageReader.NuspecReader.GetVersion();
                    if (packageVersion != null && !downloadedPackageVersion.Equals(packageVersion))
                    {
                        throw new NuGetPackageNotFoundException($"Requested workload version {packageVersion} of {id} but found version {downloadedPackageVersion} instead.");
                    }

                    var workloadSetVersion = WorkloadSetVersion.FromWorkloadSetPackageVersion(band, downloadedPackageVersion.ToString());
                    File.WriteAllText(Path.Combine(adManifestPath, Constants.workloadSetVersionFileName), workloadSetVersion);
                }

                if (_displayManifestUpdates)
                {
                    _reporter.WriteLine(LocalizableStrings.AdManifestUpdated, manifestId);
                }

                return true;
            }
            catch (Exception e)
            {
                _reporter.WriteLine(LocalizableStrings.FailedAdManifestUpdate, manifestId, e.Message);
                return false;
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

        private async Task UpdateAdvertisingManifestAsync(WorkloadManifestInfo manifest, bool includePreviews, DirectoryPath? offlineCache = null)
        {
            var fallbackFeatureBand = new SdkFeatureBand(manifest.ManifestFeatureBand);
            // The bands should be checked in the order defined here.
            SdkFeatureBand[] bands = [_sdkFeatureBand, fallbackFeatureBand];
            foreach (var band in bands.Distinct())
            {
                if (await UpdateManifestWithVersionAsync(manifest.Id, includePreviews, band, null, offlineCache))
                {
                    return;
                }
            }

            _reporter.WriteLine(LocalizableStrings.AdManifestPackageDoesNotExist, manifest.Id);
        }

        private (ManifestVersionWithBand ManifestWithBand, WorkloadCollection Workloads)? GetAdvertisingManifestVersionAndWorkloads(ManifestId manifestId)
        {
            var manifestPath = Path.Combine(GetAdvertisingManifestPath(_sdkFeatureBand, manifestId), "WorkloadManifest.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            using FileStream fsSource = new(manifestPath, FileMode.Open, FileAccess.Read);
            var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestId.ToString(), fsSource, manifestPath);
            // we need to know the feature band of the advertised manifest (read it from the AdvertisedManifestFeatureBand.txt file)
            // if we don't find the file then use the current feature band
            var adManifestFeatureBandPath = Path.Combine(GetAdvertisingManifestPath(_sdkFeatureBand, manifestId), "AdvertisedManifestFeatureBand.txt");

            SdkFeatureBand adManifestFeatureBand = _sdkFeatureBand;
            if (File.Exists(adManifestFeatureBandPath))
            {
                adManifestFeatureBand = new SdkFeatureBand(File.ReadAllText(adManifestFeatureBandPath));
            }

            ManifestVersionWithBand manifestWithBand = new(new ManifestVersion(manifest.Version), adManifestFeatureBand);
            var workloads = manifest.Workloads.Values.OfType<WorkloadDefinition>().ToDictionary(w => w.Id);
            return (manifestWithBand, workloads);
        }

        private ManifestVersionWithBand GetInstalledManifestVersion(ManifestId manifestId)
        {
            return new(new ManifestVersion(_workloadResolver.GetManifestVersion(manifestId.ToString())), new SdkFeatureBand(_workloadResolver.GetManifestFeatureBand(manifestId.ToString())));
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
            //  TODO: This doesn't seem to account for differing feature bands
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

        public IEnumerable<ManifestVersionUpdate> CalculateManifestUpdatesForWorkloadSet(WorkloadSet workloadSet)
        {
            return CalculateManifestRollbacks(workloadSet.ManifestVersions.Select(kvp => (kvp.Key, new ManifestVersionWithBand(kvp.Value.Version, kvp.Value.FeatureBand))));
        }

        private static IEnumerable<(ManifestId Id, ManifestVersionWithBand ManifestWithBand)> ParseRollbackDefinitionFile(string rollbackDefinitionFilePath, SdkFeatureBand featureBand)
        {
            string fileContent;

            if (Uri.TryCreate(rollbackDefinitionFilePath, UriKind.Absolute, out var rollbackUri) && !rollbackUri.IsFile)
            {
                using HttpClient httpClient = new();
                fileContent = httpClient.GetStringAsync(rollbackDefinitionFilePath).Result;
            }
            else if (File.Exists(rollbackDefinitionFilePath))
            {
                fileContent = File.ReadAllText(rollbackDefinitionFilePath);
            }
            else
            {
                throw new ArgumentException(string.Format(LocalizableStrings.RollbackDefinitionFileDoesNotExist, rollbackDefinitionFilePath));
            }

            var versions = WorkloadSet.FromJson(fileContent, featureBand).ManifestVersions;
            return versions.Select(kvp => (kvp.Key, new ManifestVersionWithBand(kvp.Value.Version, kvp.Value.FeatureBand)));
        }

        public IEnumerable<ManifestVersionUpdate> CalculateManifestUpdatesFromHistory(WorkloadHistoryState state)
        {
            return state.ManifestVersions.Select(
                m => new ManifestVersionUpdate(
                    new ManifestId(m.Key),
                    new ManifestVersion(m.Value.Split('/')[0]),
                    m.Value.Split('/')[1]));
        }

        private bool BackgroundUpdatesAreDisabled() => bool.TryParse(_getEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_DISABLE), out var disableEnvVar) && disableEnvVar;

        private string GetAdvertisingManifestSentinelPath(SdkFeatureBand featureBand) => Path.Combine(_userProfileDir, $".workloadAdvertisingManifestSentinel{featureBand}");

        private string GetAdvertisingWorkloadsFilePath(SdkFeatureBand featureBand) => GetAdvertisingWorkloadsFilePath(_userProfileDir, featureBand);

        private static string GetAdvertisingWorkloadsFilePath(string userProfileDir, SdkFeatureBand featureBand) => Path.Combine(userProfileDir, $".workloadAdvertisingUpdates{featureBand}");

        private string GetAdvertisingManifestPath(SdkFeatureBand featureBand, ManifestId manifestId) => Path.Combine(_userProfileDir, "sdk-advertising", featureBand.ToString(), manifestId.ToString());

        private record ManifestVersionWithBand(ManifestVersion Version, SdkFeatureBand Band);
    }
}
