// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal partial class WorkloadManifestUpdater : IWorkloadManifestUpdater
{
    private readonly IReporter _reporter;
    private readonly IWorkloadResolver _workloadResolver;
    private readonly INuGetPackageDownloader _nugetPackageDownloader;
    private readonly SdkFeatureBand _sdkFeatureBand;
    private readonly PackageSourceLocation? _packageSourceLocation;
    private readonly IWorkloadManifestInstaller _workloadManifestInstaller;
    private readonly WorkloadAdvertisingManifestUpdater _advertisingUpdater;

    public WorkloadManifestUpdater(
        IReporter reporter,
        IWorkloadResolver workloadResolver,
        INuGetPackageDownloader nugetPackageDownloader,
        string userProfileDir,
        IWorkloadInstallationRecordRepository workloadRecordRepo,
        IWorkloadManifestInstaller workloadManifestInstaller,
        PackageSourceLocation? packageSourceLocation = null,
        Func<string, string?>? getEnvironmentVariable = null,
        bool displayManifestUpdates = true,
        SdkFeatureBand? sdkFeatureBand = null)
    {
        _reporter = reporter;
        _workloadResolver = workloadResolver;
        _nugetPackageDownloader = nugetPackageDownloader;
        _sdkFeatureBand = sdkFeatureBand ?? new SdkFeatureBand(_workloadResolver.GetSdkFeatureBand());
        _packageSourceLocation = packageSourceLocation;
        _workloadManifestInstaller = workloadManifestInstaller;
        _advertisingUpdater = new WorkloadAdvertisingManifestUpdater(
            reporter,
            workloadResolver,
            nugetPackageDownloader,
            userProfileDir,
            workloadRecordRepo,
            workloadManifestInstaller,
            packageSourceLocation,
            getEnvironmentVariable,
            displayManifestUpdates,
            _sdkFeatureBand);
    }

    public Task UpdateAdvertisingManifestsAsync(
        bool includePreviews,
        bool useWorkloadSets = false,
        DirectoryPath? offlineCache = null)
        => _advertisingUpdater.UpdateAdvertisingManifestsAsync(includePreviews, useWorkloadSets, offlineCache);

    public Task BackgroundUpdateAdvertisingManifestsWhenRequiredAsync()
        => _advertisingUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync();

    public void DeleteUpdatableWorkloadsFile()
        => _advertisingUpdater.DeleteUpdatableWorkloadsFile();

    public string? GetAdvertisedWorkloadSetVersion()
        => _advertisingUpdater.GetAdvertisedWorkloadSetVersion();

    public IEnumerable<ManifestUpdateWithWorkloads> CalculateManifestUpdates()
        => _advertisingUpdater.CalculateManifestUpdates();

    public IEnumerable<WorkloadId> GetUpdatableWorkloadsToAdvertise(IEnumerable<WorkloadId> installedWorkloads)
        => _advertisingUpdater.GetUpdatableWorkloadsToAdvertise(installedWorkloads);

    public IEnumerable<ManifestVersionUpdate> CalculateManifestRollbacks(
        string rollbackDefinitionFilePath,
        WorkloadHistoryRecorder? recorder = null)
    {
        var currentManifestIds = GetInstalledManifestIds();
        var manifestRollbacks = ParseRollbackDefinitionFile(rollbackDefinitionFilePath, _sdkFeatureBand);

        if (recorder is not null)
        {
            recorder.HistoryRecord.RollbackFileContents = manifestRollbacks.ToDictionary(
                kvp => kvp.Id.ToString(),
                kvp => kvp.ManifestWithBand.Version + "/" + kvp.ManifestWithBand.Band);
        }

        var unrecognizedManifestIds = manifestRollbacks.Where(rollbackManifest => !currentManifestIds.Contains(rollbackManifest.Id));
        if (unrecognizedManifestIds.Any())
        {
            _reporter.WriteLine(string.Format(
                CliCommandStrings.RollbackDefinitionContainsExtraneousManifestIds,
                rollbackDefinitionFilePath,
                string.Join(" ", unrecognizedManifestIds)).Yellow());
            manifestRollbacks = manifestRollbacks.Where(rollbackManifest => currentManifestIds.Contains(rollbackManifest.Id));
        }

        return CalculateManifestRollbacks(manifestRollbacks);
    }

    private static IEnumerable<ManifestVersionUpdate> CalculateManifestRollbacks(
        IEnumerable<(ManifestId Id, ManifestVersionWithBand ManifestWithBand)> versionUpdates)
    {
        return versionUpdates.Select(manifest =>
        {
            var (id, (version, band)) = manifest;
            return new ManifestVersionUpdate(id, version, band.ToString());
        });
    }

    public async Task<IEnumerable<WorkloadDownload>> GetManifestPackageDownloadsAsync(
        bool includePreviews,
        SdkFeatureBand providedSdkFeatureBand,
        SdkFeatureBand installedSdkFeatureBand)
    {
        var downloads = new List<WorkloadDownload>();
        foreach (var manifest in _workloadResolver.GetInstalledManifests())
        {
            try
            {
                PackageId? providedPackageId = null;
                var fallbackFeatureBand = new SdkFeatureBand(manifest.ManifestFeatureBand);
                SdkFeatureBand[] bands = [providedSdkFeatureBand, installedSdkFeatureBand, fallbackFeatureBand];
                var success = false;

                foreach (var band in bands.Distinct())
                {
                    var packageId = _workloadManifestInstaller.GetManifestPackageId(new ManifestId(manifest.Id), band);
                    providedPackageId ??= packageId;

                    try
                    {
                        var latestVersion = await _nugetPackageDownloader.GetLatestPackageVersion(
                            packageId,
                            _packageSourceLocation,
                            includePreviews);
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

    private IEnumerable<ManifestId> GetInstalledManifestIds()
        => _workloadResolver.GetInstalledManifests().Select(manifest => new ManifestId(manifest.Id));

    public IEnumerable<ManifestVersionUpdate> CalculateManifestUpdatesFromHistory(WorkloadHistoryState state)
    {
        return state.ManifestVersions.Select(
            m => new ManifestVersionUpdate(
                new ManifestId(m.Key),
                new ManifestVersion(m.Value.Split('/')[0]),
                m.Value.Split('/')[1]));
    }

    public IEnumerable<ManifestVersionUpdate> CalculateManifestUpdatesForWorkloadSet(WorkloadSet workloadSet)
    {
        return CalculateManifestRollbacks(
            workloadSet.ManifestVersions.Select(
                kvp => (kvp.Key, new ManifestVersionWithBand(kvp.Value.Version, kvp.Value.FeatureBand))));
    }

    private static IEnumerable<(ManifestId Id, ManifestVersionWithBand ManifestWithBand)> ParseRollbackDefinitionFile(
        string rollbackDefinitionFilePath,
        SdkFeatureBand featureBand)
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
            throw new ArgumentException(string.Format(
                CliCommandStrings.RollbackDefinitionFileDoesNotExist,
                rollbackDefinitionFilePath));
        }

        var versions = WorkloadSet.FromJson(fileContent, featureBand).ManifestVersions;
        return versions.Select(kvp => (kvp.Key, new ManifestVersionWithBand(kvp.Value.Version, kvp.Value.FeatureBand)));
    }
}
