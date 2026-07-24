// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.SdkVulnerability;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class WorkloadUpdateTests
{
    private const string ManifestId = "microsoft.net.sdk.test";
    private static readonly SdkFeatureBand s_featureBand = new("11.0.100");

    [TestMethod]
    public async Task DisabledBackgroundUpdateDoesNotQueryNuGet()
    {
        using var testDirectory = new TestDirectory();
        var downloader = CreateUpdater(testDirectory.Path, out WorkloadAdvertisingManifestUpdater updater,
            name => name == EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_DISABLE ? "true" : null);

        await updater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync();

        Assert.AreEqual(0, downloader.LatestVersionQueries);
        Assert.AreEqual(0, downloader.Downloads);
        Assert.IsFalse(File.Exists(GetSentinelPath(testDirectory.Path)));
    }

    [TestMethod]
    public async Task DueBackgroundUpdateDownloadsManifestAndWritesState()
    {
        using var testDirectory = new TestDirectory();
        var downloader = CreateUpdater(testDirectory.Path, out WorkloadAdvertisingManifestUpdater updater, _ => null);

        await updater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync();

        Assert.AreEqual(1, downloader.LatestVersionQueries);
        Assert.AreEqual(1, downloader.Downloads);
        Assert.AreEqual(1, downloader.Extractions);
        Assert.AreEqual($"{ManifestId}.manifest-{s_featureBand}", downloader.LastPackageId?.ToString());
        Assert.IsTrue(File.Exists(GetSentinelPath(testDirectory.Path)));

        string updatesFile = WorkloadAdvertisingManifestUpdater.GetAdvertisingWorkloadsFilePath(testDirectory.Path, s_featureBand);
        Assert.IsTrue(File.Exists(updatesFile));
        Assert.AreSequenceEqual(
            Array.Empty<string>(),
            JsonSerializer.Deserialize(File.ReadAllText(updatesFile), WorkloadManifestUpdaterJsonSerializerContext.Default.StringArray)!);

        string advertisingManifest = Path.Combine(
            testDirectory.Path, "sdk-advertising", s_featureBand.ToString(), ManifestId, "WorkloadManifest.json");
        Assert.IsTrue(File.Exists(advertisingManifest));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task WindowsMsiAdvertisingInstallerExtractsPreExpandedManifest()
    {
        using var testDirectory = new TestDirectory();
        Directory.CreateDirectory(testDirectory.Path);
        string sourceManifestPath = WriteManifest(testDirectory.Path, "source", "2.0.0");
        var downloader = new RecordingNuGetPackageDownloader(
            testDirectory.Path,
            sourceManifestPath,
            useMsiLayout: true);
        WindowsMsiManifestInstaller installer =
            WindowsMsiManifestInstaller.CreateForAdvertisingManifestUpdates(downloader, out var records);
        string targetPath = Path.Combine(testDirectory.Path, "target", ManifestId);

        await installer.ExtractManifestAsync("manifest.nupkg", targetPath);

        Assert.IsInstanceOfType<ReadOnlyWindowsWorkloadInstallationRecordRepository>(records);
        Assert.AreEqual(
            $"{ManifestId}.Manifest-{s_featureBand}.Msi.{RuntimeInformation.ProcessArchitecture}".ToLowerInvariant(),
            installer.GetManifestPackageId(new ManifestId(ManifestId), s_featureBand).ToString());
        Assert.IsTrue(File.Exists(Path.Combine(targetPath, "WorkloadManifest.json")));
    }

    [TestMethod]
    public void VulnerabilityCacheReadsSourceGeneratedJsonAndHonorsSentinel()
    {
        using var testDirectory = new TestDirectory();
        Directory.CreateDirectory(testDirectory.Path);
        var expected = new SdkVulnerabilityInfo
        {
            IsEol = true,
            EolDate = new DateTime(2026, 1, 13, 0, 0, 0, DateTimeKind.Utc),
            LatestSdkVersion = "11.0.102",
            Cves = [new SdkCveInfo { Id = "CVE-2026-0001", Url = "https://example.test/CVE-2026-0001" }],
        };
        File.WriteAllText(
            Path.Combine(testDirectory.Path, "sdk-status-11.0.100.json"),
            JsonSerializer.Serialize(expected, SdkVulnerabilityJsonContext.Default.SdkVulnerabilityInfo));
        File.WriteAllText(Path.Combine(testDirectory.Path, ".sentinel"), "");

        var cache = new SdkReleaseMetadataCache(testDirectory.Path, _ => null);
        SdkVulnerabilityInfo? actual = cache.ReadCachedSummary("11.0.100");

        Assert.IsNotNull(actual);
        Assert.IsTrue(actual.IsEol);
        Assert.IsTrue(actual.HasVulnerabilities);
        Assert.AreEqual("CVE-2026-0001", actual.Cves[0].Id);
        Assert.AreEqual("11.0.102", actual.LatestSdkVersion);
        Assert.IsFalse(cache.IsDueForUpdate());
    }

    private static RecordingNuGetPackageDownloader CreateUpdater(
        string testRoot,
        out WorkloadAdvertisingManifestUpdater updater,
        Func<string, string?> getEnvironmentVariable)
    {
        Directory.CreateDirectory(testRoot);
        string installStateDirectory = Path.Combine(
            testRoot,
            "metadata",
            "workloads",
            RuntimeInformation.ProcessArchitecture.ToString(),
            s_featureBand.ToString(),
            "InstallState");
        Directory.CreateDirectory(installStateDirectory);
        File.WriteAllText(Path.Combine(installStateDirectory, "default.json"), """{"useWorkloadSets":false}""");
        string installedManifestPath = WriteManifest(testRoot, "installed", "1.0.0");
        string advertisingManifestPath = WriteManifest(testRoot, "advertising", "2.0.0");
        var provider = new TestManifestProvider(installedManifestPath);
        WorkloadResolver resolver = WorkloadResolver.CreateForTests(provider, testRoot);
        var downloader = new RecordingNuGetPackageDownloader(testRoot, advertisingManifestPath);
        var installer = new FileBasedManifestInstaller(downloader, new DirectoryPath(testRoot));
        var records = new FileBasedInstallationRecordRepository(Path.Combine(testRoot, "metadata", "workloads"));
        updater = new WorkloadAdvertisingManifestUpdater(
            new NullReporter(),
            resolver,
            downloader,
            testRoot,
            records,
            installer,
            getEnvironmentVariable: getEnvironmentVariable,
            displayManifestUpdates: false,
            sdkFeatureBand: s_featureBand);
        return downloader;
    }

    private static string WriteManifest(string testRoot, string directoryName, string version)
    {
        string directory = Path.Combine(testRoot, directoryName, ManifestId);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "WorkloadManifest.json");
        File.WriteAllText(path, $$"""
            {
              "version": "{{version}}",
              "workloads": {},
              "packs": {}
            }
            """);
        return path;
    }

    private static string GetSentinelPath(string testRoot) =>
        Path.Combine(testRoot, $".workloadAdvertisingManifestSentinel{s_featureBand}");

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aot-workload-update-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class TestManifestProvider(string manifestPath) : IWorkloadManifestProvider
    {
        public IEnumerable<ReadableWorkloadManifest> GetManifests()
        {
            yield return new(
                ManifestId,
                System.IO.Path.GetDirectoryName(manifestPath)!,
                manifestPath,
                s_featureBand.ToString(),
                "1.0.0",
                () => File.OpenRead(manifestPath),
                () => null);
        }

        public string GetSdkFeatureBand() => s_featureBand.ToString();

        public IWorkloadManifestProvider.WorkloadVersionInfo GetWorkloadVersion() =>
            new(s_featureBand + ".1");

        public Dictionary<string, WorkloadSet> GetAvailableWorkloadSets() => [];

        public void RefreshWorkloadManifests()
        {
        }
    }

    private sealed class RecordingNuGetPackageDownloader(
        string testRoot,
        string advertisingManifestPath,
        bool useMsiLayout = false)
        : INuGetPackageDownloader
    {
        public int LatestVersionQueries { get; private set; }
        public int Downloads { get; private set; }
        public int Extractions { get; private set; }
        public PackageId? LastPackageId { get; private set; }

        public Task<string> DownloadPackageAsync(
            PackageId packageId,
            NuGetVersion? packageVersion = null,
            PackageSourceLocation? packageSourceLocation = null,
            bool includePreview = false,
            bool? includeUnlisted = null,
            DirectoryPath? downloadFolder = null,
            PackageSourceMapping? packageSourceMapping = null)
        {
            Downloads++;
            LastPackageId = packageId;
            string path = System.IO.Path.Combine(testRoot, $"{packageId}.2.0.0.nupkg");
            File.WriteAllText(path, "");
            return Task.FromResult(path);
        }

        public Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder)
        {
            Extractions++;
            string dataDirectory = useMsiLayout
                ? System.IO.Path.Combine(targetFolder.Value, "data", "extractedManifest")
                : System.IO.Path.Combine(targetFolder.Value, "data");
            Directory.CreateDirectory(dataDirectory);
            string destination = System.IO.Path.Combine(dataDirectory, "WorkloadManifest.json");
            File.Copy(advertisingManifestPath, destination);
            return Task.FromResult<IEnumerable<string>>([destination]);
        }

        public Task<NuGetVersion> GetLatestPackageVersion(
            PackageId packageId,
            PackageSourceLocation? packageSourceLocation = null,
            bool includePreview = false)
        {
            LatestVersionQueries++;
            LastPackageId = packageId;
            return Task.FromResult(NuGetVersion.Parse("2.0.0"));
        }

        public Task<string> GetPackageUrl(
            PackageId packageId,
            NuGetVersion? packageVersion = null,
            PackageSourceLocation? packageSourceLocation = null,
            bool includePreview = false) => throw new NotSupportedException();

        public Task<IEnumerable<NuGetVersion>> GetLatestPackageVersions(
            PackageId packageId,
            int numberOfResults,
            PackageSourceLocation? packageSourceLocation = null,
            bool includePreview = false) => throw new NotSupportedException();

        public Task<NuGetVersion> GetBestPackageVersionAsync(
            PackageId packageId,
            VersionRange versionRange,
            PackageSourceLocation? packageSourceLocation = null) => throw new NotSupportedException();

        public Task<(NuGetVersion version, PackageSource source)> GetBestPackageVersionAndSourceAsync(
            PackageId packageId,
            VersionRange versionRange,
            PackageSourceLocation? packageSourceLocation = null) => throw new NotSupportedException();
    }
}
