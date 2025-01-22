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
using Microsoft.DotNet.Cli.Commands.DotNetWorkloads;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.History;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Update;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;
using Strings = Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal abstract class InstallingWorkloadCommand : WorkloadCommandBase
    {
        protected readonly string[] _arguments;
        protected readonly bool _printDownloadLinkOnly;
        protected readonly string _fromCacheOption;
        protected readonly bool _includePreviews;
        protected readonly string _downloadToCacheOption;
        protected readonly string _dotnetPath;
        protected readonly string _userProfileDir;
        protected readonly string _workloadRootDir;
        protected readonly bool _checkIfManifestExist;
        protected readonly ReleaseVersion _sdkVersion;
        protected readonly SdkFeatureBand _sdkFeatureBand;
        protected readonly ReleaseVersion _targetSdkVersion;
        protected readonly string _fromRollbackDefinition;
        protected int _fromHistorySpecified;
        protected bool _historyManifestOnlyOption;
        protected string _workloadSetVersionFromCommandLine;
        protected string _globalJsonPath;
        protected string _workloadSetVersionFromGlobalJson;
        protected readonly PackageSourceLocation _packageSourceLocation;
        protected readonly IWorkloadResolverFactory _workloadResolverFactory;
        protected IWorkloadResolver _workloadResolver;
        protected readonly IInstaller _workloadInstallerFromConstructor;
        protected readonly IWorkloadManifestUpdater _workloadManifestUpdaterFromConstructor;
        protected IInstaller _workloadInstaller;
        protected IWorkloadManifestUpdater _workloadManifestUpdater;
        private WorkloadHistoryState _workloadHistoryRecord;

        protected bool UseRollback => !string.IsNullOrWhiteSpace(_fromRollbackDefinition);
        protected bool FromHistory => _fromHistorySpecified != 0;
        protected bool SpecifiedWorkloadSetVersionOnCommandLine => !string.IsNullOrWhiteSpace(_workloadSetVersionFromCommandLine);
        protected bool SpecifiedWorkloadSetVersionInGlobalJson => !string.IsNullOrWhiteSpace(_workloadSetVersionFromGlobalJson);
        protected WorkloadHistoryState _WorkloadHistoryRecord
        {
            get
            {
                if (_workloadHistoryRecord is null && FromHistory)
                {
                    var workloadHistoryRecords = _workloadInstaller.GetWorkloadHistoryRecords(_sdkFeatureBand.ToString()).OrderBy(r => r.TimeStarted).ToList();
                    if (workloadHistoryRecords.Count == 0)
                    {
                        throw new GracefulException(Update.LocalizableStrings.NoWorkloadHistoryRecords, isUserError: true);
                    }

                    var displayRecords = WorkloadHistoryDisplay.ProcessWorkloadHistoryRecords(workloadHistoryRecords, out _);

                    if (_fromHistorySpecified < 1 || _fromHistorySpecified > displayRecords.Count)
                    {
                        throw new GracefulException(Update.LocalizableStrings.WorkloadHistoryRecordInvalidIdValue, isUserError: true);
                    }

                    _workloadHistoryRecord = displayRecords[_fromHistorySpecified - 1].HistoryState;
                }

                return _workloadHistoryRecord;
            }
        }

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
            _arguments = parseResult.GetArguments();
            _printDownloadLinkOnly = parseResult.GetValue(InstallingWorkloadCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.GetValue(InstallingWorkloadCommandParser.FromCacheOption);
            _includePreviews = parseResult.GetValue(InstallingWorkloadCommandParser.IncludePreviewOption);
            _downloadToCacheOption = parseResult.GetValue(InstallingWorkloadCommandParser.DownloadToCacheOption);

            _fromRollbackDefinition = parseResult.GetValue(InstallingWorkloadCommandParser.FromRollbackFileOption);
            _workloadSetVersionFromCommandLine = parseResult.GetValue(InstallingWorkloadCommandParser.WorkloadSetVersionOption);

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
            _sdkFeatureBand = new SdkFeatureBand(creationResult.SdkVersion);
            _workloadRootDir = WorkloadFileBasedInstall.IsUserLocal(_dotnetPath, _sdkFeatureBand.ToString()) ? _userProfileDir : _dotnetPath;
            _sdkVersion = creationResult.SdkVersion;
            _workloadResolver = creationResult.WorkloadResolver;
            _targetSdkVersion ??= _sdkVersion;

            _workloadInstallerFromConstructor = workloadInstaller;
            _workloadManifestUpdaterFromConstructor = workloadManifestUpdater;

            _globalJsonPath = SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(Environment.CurrentDirectory);
            _workloadSetVersionFromGlobalJson = SdkDirectoryWorkloadManifestProvider.GlobalJsonReader.GetWorkloadVersionFromGlobalJson(_globalJsonPath);

            if (SpecifiedWorkloadSetVersionInGlobalJson && (SpecifiedWorkloadSetVersionOnCommandLine || UseRollback || FromHistory))
            {
                throw new GracefulException(string.Format(Strings.CannotSpecifyVersionOnCommandLineAndInGlobalJson, _globalJsonPath), isUserError: true);
            }
            else if (SpecifiedWorkloadSetVersionOnCommandLine && UseRollback)
            {
                throw new GracefulException(string.Format(Update.LocalizableStrings.CannotCombineOptions,
                    InstallingWorkloadCommandParser.FromRollbackFileOption.Name,
                    InstallingWorkloadCommandParser.WorkloadSetVersionOption.Name), isUserError: true);
            }
            else if (SpecifiedWorkloadSetVersionOnCommandLine && FromHistory)
            {
                throw new GracefulException(string.Format(Update.LocalizableStrings.CannotCombineOptions,
                    InstallingWorkloadCommandParser.WorkloadSetVersionOption.Name,
                    WorkloadUpdateCommandParser.FromHistoryOption.Name), isUserError: true);
            }

            //  At this point, at most one of SpecifiedWorkloadSetVersionOnCommandLine, UseRollback, FromHistory, and SpecifiedWorkloadSetVersionInGlobalJson is true
        }

        protected static Dictionary<string, string> GetInstallStateContents(IEnumerable<ManifestVersionUpdate> manifestVersionUpdates) =>
            WorkloadSet.FromManifests(
                    manifestVersionUpdates.Select(update => new WorkloadManifestInfo(update.ManifestId.ToString(), update.NewVersion.ToString(), /* We don't actually use the directory here */ string.Empty, update.NewFeatureBand))
                    ).ToDictionaryForJson();

        InstallStateContents GetCurrentInstallState()
        {
            return GetCurrentInstallState(_sdkFeatureBand, _workloadRootDir);
        }

        static InstallStateContents GetCurrentInstallState(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        {
            string path = Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, dotnetDir), "default.json");
            return InstallStateContents.FromPath(path);
        }

        public static bool ShouldUseWorkloadSetMode(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        {
            return GetCurrentInstallState(sdkFeatureBand, dotnetDir).UseWorkloadSets ?? false;
        }

        protected void UpdateWorkloadManifests(WorkloadHistoryRecorder recorder, ITransactionContext context, DirectoryPath? offlineCache)
        {
            var updateToLatestWorkloadSet = ShouldUseWorkloadSetMode(_sdkFeatureBand, _workloadRootDir) && !SpecifiedWorkloadSetVersionInGlobalJson;
            if (FromHistory && !string.IsNullOrWhiteSpace(_WorkloadHistoryRecord.WorkloadSetVersion))
            {
                // This is essentially the same as updating to a specific workload set version, and we're now past the error check,
                // so we can just use the same code path.
                _workloadSetVersionFromCommandLine = _WorkloadHistoryRecord.WorkloadSetVersion;
            }
            else if ((UseRollback || FromHistory) && updateToLatestWorkloadSet)
            {
                // Rollback files are only for loose manifests. Update the mode to be loose manifests.
                Reporter.WriteLine(Update.LocalizableStrings.UpdateFromRollbackSwitchesModeToLooseManifests);
                _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, false);
                updateToLatestWorkloadSet = false;
            }

            if (SpecifiedWorkloadSetVersionOnCommandLine)
            {
                updateToLatestWorkloadSet = false;

                //  If a workload set version is specified, then switch to workload set update mode
                //  Check to make sure the value needs to be changed, as updating triggers a UAC prompt
                //  for MSI-based installs.
                if (!ShouldUseWorkloadSetMode(_sdkFeatureBand, _workloadRootDir))
                {
                    _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, true);
                }

                if (SpecifiedWorkloadSetVersionInGlobalJson && recorder is not null)
                {
                    recorder.HistoryRecord.GlobalJsonVersion = _workloadSetVersionFromGlobalJson;
                }
            }

            string resolvedWorkloadSetVersion = _workloadSetVersionFromGlobalJson ??_workloadSetVersionFromCommandLine;
            if (string.IsNullOrWhiteSpace(resolvedWorkloadSetVersion) && !UseRollback && !FromHistory)
            {
                _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews, updateToLatestWorkloadSet, offlineCache).Wait();
                if (updateToLatestWorkloadSet)
                {
                    resolvedWorkloadSetVersion = _workloadManifestUpdater.GetAdvertisedWorkloadSetVersion();
                    var currentWorkloadVersionInfo = _workloadResolver.GetWorkloadVersion();
                    if (resolvedWorkloadSetVersion != null && currentWorkloadVersionInfo.IsInstalled && !currentWorkloadVersionInfo.WorkloadSetsEnabledWithoutWorkloadSet)
                    {
                        var currentPackageVersion = WorkloadSetVersion.ToWorkloadSetPackageVersion(currentWorkloadVersionInfo.Version, out var currentWorkloadSetSdkFeatureBand);
                        var advertisedPackageVersion = WorkloadSetVersion.ToWorkloadSetPackageVersion(resolvedWorkloadSetVersion, out var advertisedWorkloadSetSdkFeatureBand);

                        if (currentWorkloadSetSdkFeatureBand > advertisedWorkloadSetSdkFeatureBand ||
                            new NuGetVersion(currentPackageVersion) >= new NuGetVersion(advertisedPackageVersion))
                        {
                            resolvedWorkloadSetVersion = null;
                        }
                    }
                }
            }

            if (updateToLatestWorkloadSet && resolvedWorkloadSetVersion == null)
            {
                Reporter.WriteLine(Update.LocalizableStrings.NoWorkloadUpdateFound);
                return;
            }

            IEnumerable<ManifestVersionUpdate> manifestsToUpdate;
            if (resolvedWorkloadSetVersion != null)
            {
                manifestsToUpdate = InstallWorkloadSet(context, resolvedWorkloadSetVersion);
            }
            else
            {
                manifestsToUpdate = UseRollback ? _workloadManifestUpdater.CalculateManifestRollbacks(_fromRollbackDefinition, recorder) :
                                    FromHistory ? _workloadManifestUpdater.CalculateManifestUpdatesFromHistory(_WorkloadHistoryRecord) :
                                                  _workloadManifestUpdater.CalculateManifestUpdates().Select(m => m.ManifestUpdate);
            }

            InstallStateContents oldInstallState = GetCurrentInstallState();

            context.Run(
                action: () =>
                {
                    foreach (var manifestUpdate in manifestsToUpdate)
                    {
                        _workloadInstaller.InstallWorkloadManifest(manifestUpdate, context, offlineCache);
                    }

                    if (!SpecifiedWorkloadSetVersionInGlobalJson)
                    {
                        if (UseRollback || (FromHistory && string.IsNullOrWhiteSpace(_WorkloadHistoryRecord.WorkloadSetVersion)))
                        {
                            _workloadInstaller.SaveInstallStateManifestVersions(_sdkFeatureBand, GetInstallStateContents(manifestsToUpdate));
                            _workloadInstaller.AdjustWorkloadSetInInstallState(_sdkFeatureBand, null);
                        }
                        else if (SpecifiedWorkloadSetVersionOnCommandLine)
                        {
                            _workloadInstaller.AdjustWorkloadSetInInstallState(_sdkFeatureBand, resolvedWorkloadSetVersion);
                        }
                        else if (this is WorkloadUpdateCommand)
                        {
                            //  For workload updates, if you don't specify a rollback file, or a workload version then we should update to a new version of the manifests or workload set, and
                            //  should remove the install state that pins to the other version
                            _workloadInstaller.RemoveManifestsFromInstallState(_sdkFeatureBand);
                            _workloadInstaller.AdjustWorkloadSetInInstallState(_sdkFeatureBand, null);
                        }
                    }

                    _workloadResolver.RefreshWorkloadManifests();

                    if (_workloadSetVersionFromGlobalJson != null)
                    {
                        //  Record GC Root for this global.json file
                        _workloadInstaller.RecordWorkloadSetInGlobalJson(_sdkFeatureBand, _globalJsonPath, _workloadSetVersionFromGlobalJson);
                    }
                },
                rollback: () =>
                {
                    //  Reset install state
                    var currentInstallState = GetCurrentInstallState();
                    if (currentInstallState.UseWorkloadSets != oldInstallState.UseWorkloadSets)
                    {
                        _workloadInstaller.UpdateInstallMode(_sdkFeatureBand, oldInstallState.UseWorkloadSets);
                    }

                    if ((currentInstallState.Manifests == null && oldInstallState.Manifests != null) ||
                        (currentInstallState.Manifests != null && oldInstallState.Manifests == null) ||
                        (currentInstallState.Manifests != null && oldInstallState.Manifests != null &&
                         (currentInstallState.Manifests.Count != oldInstallState.Manifests.Count ||
                         !currentInstallState.Manifests.All(m => oldInstallState.Manifests.TryGetValue(m.Key, out var val) && val.Equals(m.Value)))))
                    {
                        _workloadInstaller.SaveInstallStateManifestVersions(_sdkFeatureBand, oldInstallState.Manifests);
                    }

                    if (currentInstallState.WorkloadVersion != oldInstallState.WorkloadVersion)
                    {
                        _workloadInstaller.AdjustWorkloadSetInInstallState(_sdkFeatureBand, oldInstallState.WorkloadVersion);
                    }

                    //  We will refresh the workload manifests to make sure that the resolver has the updated state after the rollback
                    _workloadResolver.RefreshWorkloadManifests();
                });
        }

        private IEnumerable<ManifestVersionUpdate> InstallWorkloadSet(ITransactionContext context, string workloadSetVersion)
        {
            Reporter.WriteLine(string.Format(Strings.NewWorkloadSet, workloadSetVersion));
            var workloadSet = _workloadInstaller.InstallWorkloadSet(context, workloadSetVersion);

            return _workloadManifestUpdater.CalculateManifestUpdatesForWorkloadSet(workloadSet);
        }

        protected async Task<List<WorkloadDownload>> GetDownloads(IEnumerable<WorkloadId> workloadIds, bool skipManifestUpdate, bool includePreview, string downloadFolder = null,
            IReporter reporter = null, INuGetPackageDownloader packageDownloader = null)
        {
            reporter ??= Reporter;
            packageDownloader ??= PackageDownloader;

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
                        reporter.WriteLine(Strings.SkippingManifestUpdate);
                    }

                    foreach (var download in manifestDownloads)
                    {
                        //  Add package to the list of downloads
                        ret.Add(download);

                        //  Download package
                        var downloadedPackagePath = await packageDownloader.DownloadPackageAsync(new PackageId(download.NuGetPackageId), new NuGetVersion(download.NuGetPackageVersion),
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
                    DirectoryPath downloadFolderDirectoryPath = new(downloadFolder);
                    foreach (var packDownload in packDownloads)
                    {
                        reporter.WriteLine(string.Format(Strings.DownloadingPackToCacheMessage, packDownload.NuGetPackageId, packDownload.NuGetPackageVersion, downloadFolder));

                        await packageDownloader.DownloadPackageAsync(new PackageId(packDownload.NuGetPackageId), new NuGetVersion(packDownload.NuGetPackageVersion),
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

        protected IEnumerable<WorkloadId> WriteSDKInstallRecordsForVSWorkloads(IEnumerable<WorkloadId> workloadsWithExistingInstallRecords)
        {
#if !DOT_NET_BUILD_FROM_SOURCE
            if (OperatingSystem.IsWindows())
            {
                return VisualStudioWorkloads.WriteSDKInstallRecordsForVSWorkloads(_workloadInstaller, _workloadResolver, workloadsWithExistingInstallRecords, Reporter);
            }
#endif
            return workloadsWithExistingInstallRecords;
        }
    }

    internal static class InstallingWorkloadCommandParser
    {
        public static readonly Option<string> WorkloadSetVersionOption = new("--version")
        {
            Description = Strings.WorkloadSetVersionOptionDescription
        };

        public static readonly Option<bool> PrintDownloadLinkOnlyOption = new("--print-download-link-only")
        {
            Description = Strings.PrintDownloadLinkOnlyDescription,
            Hidden = true
        };

        public static readonly Option<string> FromCacheOption = new("--from-cache")
        {
            Description = Strings.FromCacheOptionDescription,
            HelpName = Strings.FromCacheOptionArgumentName,
            Hidden = true
        };

        public static readonly Option<bool> IncludePreviewOption =
        new("--include-previews")
        {
            Description = Strings.IncludePreviewOptionDescription
        };

        public static readonly Option<string> DownloadToCacheOption = new("--download-to-cache")
        {
            Description = Strings.DownloadToCacheOptionDescription,
            HelpName = Strings.DownloadToCacheOptionArgumentName,
            Hidden = true
        };

        public static readonly Option<string> VersionOption = new("--sdk-version")
        {
            Description = Strings.VersionOptionDescription,
            HelpName = Strings.VersionOptionName,
            Hidden = true
        };

        public static readonly Option<string> FromRollbackFileOption = new("--from-rollback-file")
        {
            Description = Update.LocalizableStrings.FromRollbackDefinitionOptionDescription,
            Hidden = true
        };

        public static readonly Option<string> ConfigOption = new("--configfile")
        {
            Description = Strings.ConfigFileOptionDescription,
            HelpName = Strings.ConfigFileOptionName
        };

        public static readonly Option<string[]> SourceOption = new Option<string[]>("--source", "-s")
        {
            Description = Strings.SourceOptionDescription,
            HelpName = Strings.SourceOptionName
        }.AllowSingleArgPerToken();

        internal static void AddWorkloadInstallCommandOptions(Command command)
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
