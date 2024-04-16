// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;
using Strings = Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal abstract class InstallingWorkloadCommand : WorkloadCommandBase
    {
        protected readonly bool _printDownloadLinkOnly;
        protected readonly string _fromCacheOption;
        protected readonly bool _includePreviews;
        protected readonly string _downloadToCacheOption;
        protected readonly string _dotnetPath;
        protected readonly string _userProfileDir;
        protected readonly bool _checkIfManifestExist;
        protected readonly ReleaseVersion _sdkVersion;
        protected readonly SdkFeatureBand _sdkFeatureBand;
        protected readonly ReleaseVersion _targetSdkVersion;
        protected readonly string _fromRollbackDefinition;
        protected string _workloadSetVersion;
        protected string _workloadSetVersionFromGlobalJson;
        protected readonly PackageSourceLocation _packageSourceLocation;
        protected readonly IWorkloadResolverFactory _workloadResolverFactory;
        protected IWorkloadResolver _workloadResolver;
        protected readonly IInstaller _workloadInstallerFromConstructor;
        protected readonly IWorkloadManifestUpdater _workloadManifestUpdaterFromConstructor;
        protected IInstaller _workloadInstaller;
        protected IWorkloadManifestUpdater _workloadManifestUpdater;

        public InstallingWorkloadCommand(
            ParseResult parseResult,
            IReporter reporter,
            IWorkloadResolverFactory workloadResolverFactory,
            IInstaller workloadInstaller,
            INuGetPackageDownloader nugetPackageDownloader,
            IWorkloadManifestUpdater workloadManifestUpdater,
            string tempDirPath)
            : base(parseResult, reporter: reporter, tempDirPath: tempDirPath, nugetPackageDownloader: nugetPackageDownloader)
        {
            _printDownloadLinkOnly = parseResult.GetValue(InstallingWorkloadCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.GetValue(InstallingWorkloadCommandParser.FromCacheOption);
            _includePreviews = parseResult.GetValue(InstallingWorkloadCommandParser.IncludePreviewOption);
            _downloadToCacheOption = parseResult.GetValue(InstallingWorkloadCommandParser.DownloadToCacheOption);

            _fromRollbackDefinition = parseResult.GetValue(InstallingWorkloadCommandParser.FromRollbackFileOption);
            var configOption = parseResult.GetValue(InstallingWorkloadCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValue(InstallingWorkloadCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: sourceOption);

            _workloadResolverFactory = workloadResolverFactory ?? new WorkloadResolverFactory();

            if (!string.IsNullOrEmpty(parseResult.GetValue(InstallingWorkloadCommandParser.VersionOption)))
            {
                //  Specifying a different SDK version to operate on is only supported for --print-download-link-only and --download-to-cache
                if (_printDownloadLinkOnly || !string.IsNullOrEmpty(_downloadToCacheOption))
                {
                    _targetSdkVersion = new ReleaseVersion(parseResult.GetValue(InstallingWorkloadCommandParser.VersionOption));
                }
                else
                {
                    throw new GracefulException(Strings.SdkVersionOptionNotSupported);
                }
            }

            var creationResult = _workloadResolverFactory.Create();

            _dotnetPath = creationResult.DotnetPath;
            _userProfileDir = creationResult.UserProfileDir;
            _sdkVersion = creationResult.SdkVersion;
            _sdkFeatureBand = new SdkFeatureBand(creationResult.SdkVersion);
            _workloadResolver = creationResult.WorkloadResolver;
            _targetSdkVersion ??= _sdkVersion;

            _workloadInstallerFromConstructor = workloadInstaller;
            _workloadManifestUpdaterFromConstructor = workloadManifestUpdater;
        }

        protected static Dictionary<string, string> GetInstallStateContents(IEnumerable<ManifestVersionUpdate> manifestVersionUpdates) =>
            WorkloadSet.FromManifests(
                    manifestVersionUpdates.Select(update => new WorkloadManifestInfo(update.ManifestId.ToString(), update.NewVersion.ToString(), /* We don't actually use the directory here */ string.Empty, update.NewFeatureBand))
                    ).ToDictionaryForJson();

        public static bool ShouldUseWorkloadSetMode(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        {
            string path = Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, dotnetDir), "default.json");
            var installStateContents = File.Exists(path) ? InstallStateContents.FromString(File.ReadAllText(path)) : new InstallStateContents();
            return installStateContents.UseWorkloadSets ?? false;
        }

        protected void ErrorIfGlobalJsonAndCommandLineMismatch(string globaljsonPath)
        {
            if (!string.IsNullOrWhiteSpace(_workloadSetVersionFromGlobalJson) && !string.IsNullOrWhiteSpace(_workloadSetVersion) && !_workloadSetVersion.Equals(_workloadSetVersionFromGlobalJson))
            {
                throw new Exception(string.Format(Strings.CannotSpecifyVersionOnCommandLineAndInGlobalJson, globaljsonPath));
            }
        }

        protected bool TryHandleWorkloadUpdateFromVersion(ITransactionContext context, DirectoryPath? offlineCache, out IEnumerable<ManifestVersionUpdate> updates)
        {
            // Ensure workload set mode is set to 'workloadset'
            // Do not skip checking the mode first, as setting it triggers
            // an admin authorization popup for MSI-based installs.
            if (!ShouldUseWorkloadSetMode(_sdkFeatureBand, _dotnetPath))
            {
                _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, true);
            }

            _workloadManifestUpdater.DownloadWorkloadSet(_workloadSetVersionFromGlobalJson ?? _workloadSetVersion, offlineCache);
            return TryInstallWorkloadSet(context, out updates);
        }

        public bool TryInstallWorkloadSet(ITransactionContext context, out IEnumerable<ManifestVersionUpdate> updates)
        {
            var advertisingPackagePath = Path.Combine(_userProfileDir, "sdk-advertising", _sdkFeatureBand.ToString(), "microsoft.net.workloads");
            if (File.Exists(Path.Combine(advertisingPackagePath, Constants.workloadSetVersionFileName)))
            {
                // This file isn't created in tests.
                PrintWorkloadSetTransition(File.ReadAllText(Path.Combine(advertisingPackagePath, Constants.workloadSetVersionFileName)));
            }
            else if (_workloadInstaller is FileBasedInstaller || _workloadInstaller is NetSdkMsiInstallerClient)
            {
                // No workload sets found
                Reporter.WriteLine(Update.LocalizableStrings.NoWorkloadUpdateFound);
                updates = null;
                return false;
            }

            var workloadSetPath = _workloadInstaller.InstallWorkloadSet(context, advertisingPackagePath);
            var files = Directory.EnumerateFiles(workloadSetPath, "*.workloadset.json");
            updates = _workloadManifestUpdater.ParseRollbackDefinitionFiles(files);
            return true;
        }

        private void PrintWorkloadSetTransition(string newVersion)
        {
            var currentVersion = _workloadResolver.GetWorkloadVersion();
            if (currentVersion == null || !string.IsNullOrWhiteSpace(_workloadSetVersionFromGlobalJson))
            {
                Reporter.WriteLine(string.Format(Strings.NewWorkloadSet, newVersion));
            }
            else
            {
                Reporter.WriteLine(string.Format(Strings.WorkloadSetUpgrade, currentVersion, newVersion));
            }
        }

        protected async Task<List<WorkloadDownload>> GetDownloads(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate, bool includePreview, string downloadFolder = null)
        {
            List<WorkloadDownload> ret = new();

            DirectoryPath? tempPath = null;

            try
            {
                if (!skipManifestUpdate)
                {
                    DirectoryPath folderForManifestDownloads;
                    tempPath = new DirectoryPath(Path.Combine(TempDirectoryPath, "dotnet-manifest-extraction"));
                    string extractedManifestsPath = Path.Combine(tempPath.Value.Value, "manifests");

                    if (downloadFolder != null)
                    {
                        folderForManifestDownloads = new DirectoryPath(downloadFolder);
                    }
                    else
                    {
                        folderForManifestDownloads = tempPath.Value;
                    }

                    var manifestDownloads = await _workloadManifestUpdater.GetManifestPackageDownloadsAsync(includePreview, new SdkFeatureBand(_targetSdkVersion), _sdkFeatureBand);

                    if (!manifestDownloads.Any())
                    {
                        Reporter.WriteLine(Strings.SkippingManifestUpdate);
                    }

                    foreach (var download in manifestDownloads)
                    {
                        //  Add package to the list of downloads
                        ret.Add(download);

                        //  Download package                        
                        var downloadedPackagePath = await PackageDownloader.DownloadPackageAsync(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion),
                            _packageSourceLocation, downloadFolder: folderForManifestDownloads);

                        //  Extract manifest from package
                        await _workloadInstaller.ExtractManifestAsync(downloadedPackagePath, Path.Combine(extractedManifestsPath, download.Id));
                    }

                    //  Use updated, extracted manifests to resolve packs
                    var overlayProvider = new TempDirectoryWorkloadManifestProvider(extractedManifestsPath, _sdkFeatureBand.ToString());

                    var newResolver = _workloadResolver.CreateOverlayResolver(overlayProvider);
                    _workloadInstaller.ReplaceWorkloadResolver(newResolver);
                }

                var packDownloads = _workloadInstaller.GetDownloads(workloadIds, _sdkFeatureBand, false);
                ret.AddRange(packDownloads);

                if (downloadFolder != null)
                {
                    DirectoryPath downloadFolderDirectoryPath = new DirectoryPath(downloadFolder);
                    foreach (var packDownload in packDownloads)
                    {
                        Reporter.WriteLine(string.Format(Install.LocalizableStrings.DownloadingPackToCacheMessage, packDownload.NuGetPackageId, packDownload.NuGetPackageVersion, downloadFolder));

                        await PackageDownloader.DownloadPackageAsync(new PackageId(packDownload.NuGetPackageId), new NuGetVersion(packDownload.NuGetPackageVersion),
                            _packageSourceLocation, downloadFolder: downloadFolderDirectoryPath);
                    }
                }
            }
            finally
            {
                if (tempPath != null && Directory.Exists(tempPath.Value.Value))
                {
                    Directory.Delete(tempPath.Value.Value, true);
                }
            }

            return ret;
        }

        protected IEnumerable<WorkloadId> GetInstalledWorkloads(bool fromPreviousSdk)
        {
            if (fromPreviousSdk)
            {
                var priorFeatureBands = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetFeatureBandsWithInstallationRecords()
                    .Where(featureBand => featureBand.CompareTo(_sdkFeatureBand) < 0);
                if (priorFeatureBands.Any())
                {
                    var maxPriorFeatureBand = priorFeatureBands.Max();
                    return _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(maxPriorFeatureBand);
                }
                return new List<WorkloadId>();
            }
            else
            {
                var workloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_sdkFeatureBand);

                return workloads ?? Enumerable.Empty<WorkloadId>();
            }
        }
    }

    internal static class InstallingWorkloadCommandParser
    {
        public static readonly CliOption<string> WorkloadSetVersionOption = new("--version")
        {
            Description = Strings.WorkloadSetVersionOptionDescription
        };

        public static readonly CliOption<bool> PrintDownloadLinkOnlyOption = new("--print-download-link-only")
        {
            Description = Strings.PrintDownloadLinkOnlyDescription,
            Hidden = true
        };

        public static readonly CliOption<string> FromCacheOption = new("--from-cache")
        {
            Description = Strings.FromCacheOptionDescription,
            HelpName = Strings.FromCacheOptionArgumentName,
            Hidden = true
        };

        public static readonly CliOption<bool> IncludePreviewOption =
        new("--include-previews")
        {
            Description = Strings.IncludePreviewOptionDescription
        };

        public static readonly CliOption<string> DownloadToCacheOption = new("--download-to-cache")
        {
            Description = Strings.DownloadToCacheOptionDescription,
            HelpName = Strings.DownloadToCacheOptionArgumentName,
            Hidden = true
        };

        public static readonly CliOption<string> VersionOption = new("--sdk-version")
        {
            Description = Strings.VersionOptionDescription,
            HelpName = Strings.VersionOptionName,
            Hidden = true
        };

        public static readonly CliOption<string> FromRollbackFileOption = new("--from-rollback-file")
        {
            Description = Update.LocalizableStrings.FromRollbackDefinitionOptionDescription,
            Hidden = true
        };

        public static readonly CliOption<string> ConfigOption = new("--configfile")
        {
            Description = Strings.ConfigFileOptionDescription,
            HelpName = Strings.ConfigFileOptionName
        };

        public static readonly CliOption<string[]> SourceOption = new CliOption<string[]>("--source", "-s")
        {
            Description = Strings.SourceOptionDescription,
            HelpName = Strings.SourceOptionName
        }.AllowSingleArgPerToken();

        internal static void AddWorkloadInstallCommandOptions(CliCommand command)
        {
            command.Options.Add(VersionOption);
            command.Options.Add(ConfigOption);
            command.Options.Add(SourceOption);
            command.Options.Add(PrintDownloadLinkOnlyOption);
            command.Options.Add(FromCacheOption);
            command.Options.Add(DownloadToCacheOption);
            command.Options.Add(IncludePreviewOption);
            command.Options.Add(FromRollbackFileOption);
        }
    }
}
