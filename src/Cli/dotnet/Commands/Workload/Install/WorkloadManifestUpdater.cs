// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

/// <summary>
///  Shared background advertising-manifest entry points used by both the managed and Native AOT CLI.
///  The managed <see cref="IWorkloadManifestUpdater"/> implementation is in the managed-only partial.
/// </summary>
internal partial class WorkloadManifestUpdater
{
    public static readonly string WorkloadSetManifestId = "Microsoft.NET.Workloads";

    /// <summary>
    ///  Builds the narrow set of dependencies the background advertising-manifest update needs, without
    ///  going through <see cref="WorkloadInstallerFactory"/> (i.e. without constructing the full
    ///  <see cref="FileBasedInstaller"/>/<see cref="NetSdkMsiInstallerClient"/> installer or, on Windows,
    ///  any elevated MSI IPC).
    /// </summary>
    private static WorkloadAdvertisingManifestUpdater GetAdvertisingUpdaterInstance(string userProfileDir)
    {
        var reporter = new NullReporter();
        var dotnetPath = Path.GetDirectoryName(Environment.ProcessPath);
        var sdkVersion = Product.Version;
        var sdkFeatureBand = new SdkFeatureBand(sdkVersion);
        var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, sdkVersion, userProfileDir, SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(Environment.CurrentDirectory));
        var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetPath, sdkVersion, userProfileDir);
        var tempPackagesDir = new DirectoryPath(TemporaryDirectory.CreateSubdirectory());
        // NuGet verification uses ShouldVerifySignatures() (respects registry policy and host
        // signing status, but not --skip-sign-check since this is a background operation).
        // MSI verification is intentionally disabled — this updater only downloads advertising
        // manifests, not installable MSIs.
        var verifySignatures = WorkloadUtilities.ShouldVerifySignatures();
        var nugetPackageDownloader = NuGetPackageDownloader.NuGetPackageDownloader.CreateForWorkloads(
            tempPackagesDir,
            verifySignatures,
            reporter: reporter);

        IWorkloadManifestInstaller manifestInstaller;
        IWorkloadInstallationRecordRepository workloadRecordRepo;

        if (WorkloadInstallType.GetWorkloadInstallType(sdkFeatureBand, dotnetPath) == InstallType.Msi)
        {
#if !TARGET_WINDOWS
            throw new InvalidOperationException(CliCommandStrings.OSDoesNotSupportMsi);
#else
            if (!OperatingSystem.IsWindows())
            {
                throw new InvalidOperationException(CliCommandStrings.OSDoesNotSupportMsi);
            }

            manifestInstaller = WindowsMsiManifestInstaller.CreateForAdvertisingManifestUpdates(nugetPackageDownloader, out workloadRecordRepo);
#endif
        }
        else
        {
            manifestInstaller = new FileBasedManifestInstaller(nugetPackageDownloader, tempPackagesDir);
            workloadRecordRepo = FileBasedWorkloadInstallationRecordRepositoryFactory.Create(dotnetPath, sdkFeatureBand, userProfileDir);
        }

        return new WorkloadAdvertisingManifestUpdater(reporter, workloadResolver, nugetPackageDownloader, userProfileDir, workloadRecordRepo, manifestInstaller, sdkFeatureBand: sdkFeatureBand);
    }

    public static async Task BackgroundUpdateAdvertisingManifestsAsync(string userProfileDir)
    {
        try
        {
            var advertisingUpdater = GetAdvertisingUpdaterInstance(userProfileDir);
            await advertisingUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync();
        }
        catch (Exception)
        {
            // Never surface messages on background updates
        }
    }

    public static bool ShouldUseWorkloadSetMode(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        => WorkloadAdvertisingManifestUpdater.ShouldUseWorkloadSetMode(sdkFeatureBand, dotnetDir);

    public static void AdvertiseWorkloadUpdates()
    {
        try
        {
            var backgroundUpdatesDisabled = bool.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_DISABLE), out var disableEnvVar) && disableEnvVar;
            SdkFeatureBand featureBand = new(Product.Version);
            var adUpdatesFile = WorkloadAdvertisingManifestUpdater.GetAdvertisingWorkloadsFilePath(CliFolderPathCalculator.DotnetUserProfileFolderPath, featureBand);
            if (!backgroundUpdatesDisabled && File.Exists(adUpdatesFile))
            {
                var updatableWorkloads = JsonSerializer.Deserialize(File.ReadAllText(adUpdatesFile), WorkloadManifestUpdaterJsonSerializerContext.Default.StringArray);
                if (updatableWorkloads != null && updatableWorkloads.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine(CliCommandStrings.WorkloadInstallWorkloadUpdatesAvailable);
                }
            }
        }
        catch (Exception)
        {
            // Never surface errors
        }
    }

<<<<<<< HEAD
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
            if (adVersion.CompareTo(installedVersion) > 0 && adBand.Equals(installedBand) ||
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
#if TARGET_WINDOWS
            if (OperatingSystem.IsWindows())
            {
                //  Also advertise updates for workloads installed by Visual Studio
                InstalledWorkloadsCollection installedVSWorkloads = new InstalledWorkloadsCollection();
                VisualStudioWorkloads.GetInstalledWorkloads(_workloadResolver, installedVSWorkloads, _sdkFeatureBand);
                installedWorkloads = [.. installedWorkloads.Concat(installedVSWorkloads.AsEnumerable().Select(kvp => new WorkloadId(kvp.Key))).Distinct()];
            }
#endif

            var overlayProvider = new TempDirectoryWorkloadManifestProvider(Path.Combine(_userProfileDir, "sdk-advertising", _sdkFeatureBand.ToString()), _sdkFeatureBand.ToString());
            var advertisingManifestResolver = _workloadResolver.CreateOverlayResolver(overlayProvider);
            return _workloadResolver.GetUpdatedWorkloads(advertisingManifestResolver, installedWorkloads);
        }
        catch
        {
            return [];
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
            _reporter.WriteLine(string.Format(CliCommandStrings.RollbackDefinitionContainsExtraneousManifestIds, rollbackDefinitionFilePath, string.Join(" ", unrecognizedManifestIds)).Yellow());
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
                    _reporter.WriteLine(CliCommandStrings.ManifestPackageUrlNotResolved, providedPackageId);
                }
            }
            catch
            {
                _reporter.WriteLine(CliCommandStrings.ManifestPackageUrlNotResolved, manifest.Id);
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

                var workloadSetVersion = band.GetWorkloadSetPackageVersion(downloadedPackageVersion.ToString());
                File.WriteAllText(Path.Combine(adManifestPath, Constants.workloadSetVersionFileName), workloadSetVersion);
            }

            if (_displayManifestUpdates)
            {
                _reporter.WriteLine(CliCommandStrings.AdManifestUpdated, manifestId);
            }

            return true;
        }
        catch (Exception e)
        {
            if (_displayManifestUpdates)
            {
                _reporter.WriteLine(CliCommandStrings.FailedAdManifestUpdate, manifestId, e.Message);
            }
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

        if (_displayManifestUpdates)
        {
            _reporter.WriteLine(CliCommandStrings.AdManifestPackageDoesNotExist, manifest.Id);
        }
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
            throw new ArgumentException(string.Format(CliCommandStrings.RollbackDefinitionFileDoesNotExist, rollbackDefinitionFilePath));
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
=======
>>>>>>> origin/main
}
