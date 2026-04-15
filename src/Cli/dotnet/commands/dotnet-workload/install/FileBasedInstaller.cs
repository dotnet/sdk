// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.History;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;
using PathUtility = Microsoft.DotNet.Tools.Common.PathUtility;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class FileBasedInstaller : IInstaller
    {
        private readonly IReporter _reporter;
        private readonly string _workloadMetadataDir;
        private const string InstalledPacksDir = "InstalledPacks";
        private const string InstalledManifestsDir = "InstalledManifests";
        private const string InstalledWorkloadSetsDir = "InstalledWorkloadSets";
        private const string HistoryDir = "history";
        protected readonly string _dotnetDir;
        protected readonly string _userProfileDir;
        protected readonly string _workloadRootDir;
        protected readonly DirectoryPath _tempPackagesDir;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private IWorkloadResolver _workloadResolver;
        private readonly SdkFeatureBand _sdkFeatureBand;
        private readonly FileBasedInstallationRecordRepository _installationRecordRepository;
        private readonly PackageSourceLocation _packageSourceLocation;
        private readonly RestoreActionConfig _restoreActionConfig;

        public int ExitCode => 0;

        public FileBasedInstaller(IReporter reporter,
            SdkFeatureBand sdkFeatureBand,
            IWorkloadResolver workloadResolver,
            string userProfileDir,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir = null,
            string tempDirPath = null,
            VerbosityOptions verbosity = VerbosityOptions.normal,
            PackageSourceLocation packageSourceLocation = null,
            RestoreActionConfig restoreActionConfig = null,
            VerbosityOptions nugetPackageDownloaderVerbosity = VerbosityOptions.normal)
        {
            _userProfileDir = userProfileDir;
            _dotnetDir = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _tempPackagesDir = new DirectoryPath(tempDirPath ?? PathUtilities.CreateTempSubdirectory());
            ILogger logger = verbosity.IsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger();
            _restoreActionConfig = restoreActionConfig;
            _nugetPackageDownloader = nugetPackageDownloader ??
                                      new NuGetPackageDownloader(_tempPackagesDir, filePermissionSetter: null,
                                          new FirstPartyNuGetPackageSigningVerifier(), logger,
                                          restoreActionConfig: _restoreActionConfig,
                                          verbosityOptions: nugetPackageDownloaderVerbosity);
            bool userLocal = WorkloadFileBasedInstall.IsUserLocal(_dotnetDir, sdkFeatureBand.ToString());
            _workloadRootDir = userLocal ? _userProfileDir : _dotnetDir;
            _workloadMetadataDir = Path.Combine(_workloadRootDir, "metadata", "workloads");
            _reporter = reporter;
            _sdkFeatureBand = sdkFeatureBand;
            _workloadResolver = workloadResolver;
            _installationRecordRepository = new FileBasedInstallationRecordRepository(_workloadMetadataDir);
            _packageSourceLocation = packageSourceLocation;
        }

        public IWorkloadInstallationRecordRepository GetWorkloadInstallationRecordRepository()
        {
            return _installationRecordRepository;
        }

        public void ReplaceWorkloadResolver(IWorkloadResolver workloadResolver)
        {
            _workloadResolver = workloadResolver;
        }

        IEnumerable<PackInfo> GetPacksInWorkloads(IEnumerable<WorkloadId> workloadIds)
        {
            var packs = workloadIds
                .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId))
                .Distinct()
                .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                .Where(pack => pack != null);

            return packs;
        }

        public WorkloadSet InstallWorkloadSet(ITransactionContext context, string workloadSetVersion, DirectoryPath? offlineCache = null)
        {
            string workloadSetPackageVersion = WorkloadSet.WorkloadSetVersionToWorkloadSetPackageVersion(workloadSetVersion, out SdkFeatureBand workloadSetFeatureBand);
            var workloadSetPackageId = GetManifestPackageId(new ManifestId(WorkloadManifestUpdater.WorkloadSetManifestId), workloadSetFeatureBand);

            var workloadSetPath = Path.Combine(_workloadRootDir, "sdk-manifests", _sdkFeatureBand.ToString(), "workloadsets", workloadSetVersion);

            try
            {
                InstallPackage(workloadSetPackageId, workloadSetPackageVersion, workloadSetPath, context, offlineCache);
                context.Run(
                    action: () =>
                    {
                        WriteWorkloadSetInstallationRecord(workloadSetVersion, workloadSetFeatureBand, _sdkFeatureBand);
                    },
                    rollback: () =>
                    {
                        RemoveWorkloadSetInstallationRecord(workloadSetVersion, workloadSetFeatureBand, _sdkFeatureBand);
                    });
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format(LocalizableStrings.FailedToInstallWorkloadSet, workloadSetVersion, ex.Message), ex);
            }

            return WorkloadSet.FromWorkloadSetFolder(workloadSetPath, workloadSetVersion, _sdkFeatureBand);
        }

        public WorkloadSet GetWorkloadSetContents(string workloadSetVersion) => GetWorkloadSetContentsAsync(workloadSetVersion).GetAwaiter().GetResult();

        public async Task<WorkloadSet> GetWorkloadSetContentsAsync(string workloadSetVersion)
        {
            string workloadSetPackageVersion = WorkloadSet.WorkloadSetVersionToWorkloadSetPackageVersion(workloadSetVersion, out var workloadSetFeatureBand);
            var packagePath = await _nugetPackageDownloader.DownloadPackageAsync(GetManifestPackageId(new ManifestId(WorkloadManifestUpdater.WorkloadSetManifestId), workloadSetFeatureBand),
                                new NuGetVersion(workloadSetPackageVersion), _packageSourceLocation);
            var tempExtractionDir = Path.Combine(_tempPackagesDir.Value, $"{WorkloadManifestUpdater.WorkloadSetManifestId}-{workloadSetPackageVersion}-extracted");
            await ExtractManifestAsync(packagePath, tempExtractionDir);
            return WorkloadSet.FromWorkloadSetFolder(tempExtractionDir, workloadSetVersion, _sdkFeatureBand);
        }

        public void InstallWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, ITransactionContext transactionContext, DirectoryPath? offlineCache = null)
        {
            var packInfos = GetPacksInWorkloads(workloadIds);

            foreach (var packInfo in packInfos)
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.InstallingPackVersionMessage, packInfo.ResolvedPackageId, packInfo.Version));
                var tempDirsToDelete = new List<string>();
                var tempFilesToDelete = new List<string>();
                bool shouldRollBackPack = false;

                transactionContext.Run(
                    action: () =>
                    {
                        if (!PackIsInstalled(packInfo))
                        {
                            shouldRollBackPack = true;
                            string packagePath;
                            if (offlineCache == null || !offlineCache.HasValue)
                            {
                                packagePath = _nugetPackageDownloader
                                    .DownloadPackageAsync(new PackageId(packInfo.ResolvedPackageId),
                                        new NuGetVersion(packInfo.Version),
                                        _packageSourceLocation).GetAwaiter().GetResult();
                                tempFilesToDelete.Add(packagePath);
                            }
                            else
                            {
                                _reporter.WriteLine(string.Format(LocalizableStrings.UsingCacheForPackInstall, packInfo.ResolvedPackageId, packInfo.Version, offlineCache));
                                packagePath = Path.Combine(offlineCache.Value.Value, $"{packInfo.ResolvedPackageId}.{packInfo.Version}.nupkg");
                                if (!File.Exists(packagePath))
                                {
                                    throw new Exception(string.Format(LocalizableStrings.CacheMissingPackage, packInfo.ResolvedPackageId, packInfo.Version, offlineCache));
                                }
                            }

                            if (!Directory.Exists(Path.GetDirectoryName(packInfo.Path)))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(packInfo.Path));
                            }

                            if (IsSingleFilePack(packInfo))
                            {
                                File.Copy(packagePath, packInfo.Path);
                            }
                            else
                            {
                                var tempExtractionDir = Path.Combine(_tempPackagesDir.Value, $"{packInfo.ResolvedPackageId}-{packInfo.Version}-extracted");
                                tempDirsToDelete.Add(tempExtractionDir);
                                Directory.CreateDirectory(tempExtractionDir);
                                var packFiles = _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(tempExtractionDir)).GetAwaiter().GetResult();

                                FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(tempExtractionDir, packInfo.Path));
                            }
                        }
                        else
                        {
                            _reporter.WriteLine(string.Format(LocalizableStrings.WorkloadPackAlreadyInstalledMessage, packInfo.ResolvedPackageId, packInfo.Version));
                        }

                        WritePackInstallationRecord(packInfo, sdkFeatureBand);
                    },
                    rollback: () =>
                    {
                        try
                        {
                            if (shouldRollBackPack)
                            {
                                _reporter.WriteLine(string.Format(LocalizableStrings.RollingBackPackInstall, packInfo.ResolvedPackageId));
                                DeletePackInstallationRecord(packInfo, sdkFeatureBand);
                                if (!PackHasInstallRecords(packInfo))
                                {
                                    DeletePack(packInfo);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // Don't hide the original error if roll back fails
                            _reporter.WriteLine(string.Format(LocalizableStrings.RollBackFailedMessage, e.Message));
                        }
                    },
                    cleanup: () =>
                    {
                        // Delete leftover dirs and files
                        foreach (var file in tempFilesToDelete)
                        {
                            if (File.Exists(file))
                            {
                                File.Delete(file);
                            }
                        }
                        foreach (var dir in tempDirsToDelete)
                        {
                            if (Directory.Exists(dir))
                            {
                                Directory.Delete(dir, true);
                            }
                        }
                    });
            }
        }

        public void RepairWorkloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, DirectoryPath? offlineCache = null)
        {
            // TODO: Actually re-extract the packs to fix any corrupted files.
            CliTransaction.RunNew(context => InstallWorkloads(workloadIds, sdkFeatureBand, context, offlineCache));
        }

        string GetManifestInstallDirForFeatureBand(string sdkFeatureBand)
        {
            return Path.Combine(_workloadRootDir, "sdk-manifests", sdkFeatureBand);
        }

        public void InstallWorkloadManifest(ManifestVersionUpdate manifestUpdate, ITransactionContext transactionContext, DirectoryPath? offlineCache = null)
        {
            var newManifestPath = Path.Combine(GetManifestInstallDirForFeatureBand(manifestUpdate.NewFeatureBand), manifestUpdate.ManifestId.ToString(), manifestUpdate.NewVersion.ToString());

            _reporter.WriteLine(string.Format(LocalizableStrings.InstallingWorkloadManifest, manifestUpdate.ManifestId, manifestUpdate.NewVersion));

            try
            {
                var newManifestPackageId = GetManifestPackageId(manifestUpdate.ManifestId, new SdkFeatureBand(manifestUpdate.NewFeatureBand));

                InstallPackage(newManifestPackageId, manifestUpdate.NewVersion.ToString(), newManifestPath, transactionContext, offlineCache);

                transactionContext.Run(
                    action: () =>
                    {
                        WriteManifestInstallationRecord(manifestUpdate.ManifestId, manifestUpdate.NewVersion, new SdkFeatureBand(manifestUpdate.NewFeatureBand), _sdkFeatureBand);
                    },
                    rollback: () =>
                    {
                        RemoveManifestInstallationRecord(manifestUpdate.ManifestId, manifestUpdate.NewVersion, new SdkFeatureBand(manifestUpdate.NewFeatureBand), _sdkFeatureBand);
                    });
            }
            catch (Exception e)
            {
                throw new Exception(string.Format(LocalizableStrings.FailedToInstallWorkloadManifest, manifestUpdate.ManifestId, manifestUpdate.NewVersion, e.Message), e);
            }
        }

        void InstallPackage(PackageId packageId, string packageVersion, string targetFolder, ITransactionContext transactionContext, DirectoryPath? offlineCache)
        {
            string packagePath = null;
            string tempBackupDir = null;

            transactionContext.Run(
                action: () =>
                {
                    if (offlineCache == null || !offlineCache.HasValue)
                    {
                        packagePath = _nugetPackageDownloader.DownloadPackageAsync(packageId,
                            new NuGetVersion(packageVersion), _packageSourceLocation).GetAwaiter().GetResult();
                    }
                    else
                    {
                        packagePath = Path.Combine(offlineCache.Value.Value, $"{packageId}.{packageVersion}.nupkg");
                        if (!File.Exists(packagePath))
                        {
                            throw new Exception(string.Format(LocalizableStrings.CacheMissingPackage, packageId, packageVersion, offlineCache));
                        }
                    }

                    //  If target directory already exists, back it up in case we roll back
                    if (Directory.Exists(targetFolder) && Directory.GetFileSystemEntries(targetFolder).Any())
                    {
                        tempBackupDir = Path.Combine(_tempPackagesDir.Value, $"{packageId} - {packageVersion}-backup");
                        if (Directory.Exists(tempBackupDir))
                        {
                            Directory.Delete(tempBackupDir, true);
                        }
                        FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(targetFolder, tempBackupDir));
                    }

                    ExtractManifestAsync(packagePath, targetFolder).GetAwaiter().GetResult();

                },
                rollback: () =>
                {
                    if (!string.IsNullOrEmpty(tempBackupDir) && Directory.Exists(tempBackupDir))
                    {
                        FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(tempBackupDir, targetFolder));
                    }
                },
                cleanup: () =>
                {
                    // Delete leftover dirs and files
                    if (!string.IsNullOrEmpty(packagePath) && File.Exists(packagePath) && (offlineCache == null || !offlineCache.HasValue))
                    {
                        File.Delete(packagePath);
                    }

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

                    if (!string.IsNullOrEmpty(tempBackupDir) && Directory.Exists(tempBackupDir))
                    {
                        Directory.Delete(tempBackupDir, true);
                    }
                });
        }

        public IEnumerable<WorkloadDownload> GetDownloads(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand, bool includeInstalledItems)
        {
            var packs = GetPacksInWorkloads(workloadIds);
            if (!includeInstalledItems)
            {
                packs = packs.Where(p => !PackIsInstalled(p));
            }

            return packs.Select(p => new WorkloadDownload(p.Id, p.ResolvedPackageId, p.Version)).ToList();
        }

        public void GarbageCollect(Func<string, IWorkloadResolver> getResolverForWorkloadSet, DirectoryPath? offlineCache = null, bool cleanAllPacks = false)
        {
            var globalJsonWorkloadSetVersions = GetGlobalJsonWorkloadSetVersions(_sdkFeatureBand);

            var garbageCollector = new WorkloadGarbageCollector(_workloadRootDir,
                _sdkFeatureBand,
                _installationRecordRepository.GetInstalledWorkloads(_sdkFeatureBand),
                getResolverForWorkloadSet,
                globalJsonWorkloadSetVersions,
                Reporter.Verbose);
            garbageCollector.Collect();

            var featureBandsWithWorkloadInstallRecords = _installationRecordRepository.GetFeatureBandsWithInstallationRecords();

            var installedSdkFeatureBands = NETCoreSdkResolverNativeWrapper.GetAvailableSdks(_dotnetDir).Select(sdkDir => new SdkFeatureBand(Path.GetFileName(sdkDir))).ToHashSet();

            //  Tests will often use a dotnet folder without any SDKs installed.  To work around this, always add the current feature band to the list of installed feature bands
            installedSdkFeatureBands.Add(_sdkFeatureBand);

            _reporter.WriteLine(string.Format(LocalizableStrings.GarbageCollectingSdkFeatureBandsMessage, string.Join(" ", installedSdkFeatureBands)));

            //  Garbage collect workload sets
            var installedWorkloadSets = _workloadResolver.GetWorkloadManifestProvider().GetAvailableWorkloadSets();
            Dictionary<(string workloadSetVersion, SdkFeatureBand workloadSetFeatureBand), List<SdkFeatureBand>> workloadSetInstallRecords = GetAllWorkloadSetInstallRecords();
            foreach ((string workloadSetVersion, _) in installedWorkloadSets)
            {
                //  Get the feature band of the workload set
                WorkloadSet.WorkloadSetVersionToWorkloadSetPackageVersion(workloadSetVersion, out var workloadSetFeatureBand);

                List<SdkFeatureBand> referencingFeatureBands;
                if (!workloadSetInstallRecords.TryGetValue((workloadSetVersion, workloadSetFeatureBand), out referencingFeatureBands))
                {
                    //  If there are no install records for a workload set that is on disk, then ignore it.  It is probably a baseline workload set.
                    continue;
                }
                List<SdkFeatureBand> featureBandsToRemove = new();
                foreach (var referencingFeatureBand in referencingFeatureBands)
                {
                    if (!installedSdkFeatureBands.Contains(referencingFeatureBand))
                    {
                        //  If an SDK feature band is no longer installed, manifests it references can be garbage collected
                        featureBandsToRemove.Add(referencingFeatureBand);
                    }

                    if (referencingFeatureBand.Equals(_sdkFeatureBand) && !garbageCollector.WorkloadSetsToKeep.Contains(workloadSetVersion))
                    {
                        //  For current feature band, garbage collect workload sets that the garbage collector didn't mark as ones to keep
                        featureBandsToRemove.Add(referencingFeatureBand);
                    }
                }

                foreach (var featureBandToRemove in featureBandsToRemove)
                {
                    RemoveWorkloadSetInstallationRecord(workloadSetVersion, workloadSetFeatureBand, featureBandToRemove);
                }

                if (featureBandsToRemove.Count == referencingFeatureBands.Count)
                {
                    //  All installation records for the workload set were removed, so it can be deleted
                    string workloadSetDirectory = Path.Combine(GetManifestInstallDirForFeatureBand(workloadSetFeatureBand.ToString()), SdkDirectoryWorkloadManifestProvider.WorkloadSetsFolderName, workloadSetVersion);
                    if (Directory.Exists(workloadSetDirectory))
                    {
                        //  If the directory doesn't exist, the workload set is probably from a directory specified via the DOTNETSDK_WORKLOAD_MANIFEST_ROOTS environment variable
                        //  In that case just ignore it, as the CLI doesn't manage that install
                        _reporter.WriteLine(string.Format(LocalizableStrings.DeletingWorkloadSet, workloadSetVersion));
                        Directory.Delete(workloadSetDirectory, true);
                    }
                }
            }

            //  Garbage collect workload manifests
            Dictionary<(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand manifestFeatureBand), List<SdkFeatureBand>> manifestInstallRecords = GetAllManifestInstallRecords();
            foreach (var (manifestId, manifestVersion, manifestFeatureBand) in manifestInstallRecords.Keys)
            {
                var referencingFeatureBands = manifestInstallRecords[(manifestId, manifestVersion, manifestFeatureBand)];
                List<SdkFeatureBand> featureBandsToRemove = new();
                foreach (var referencingFeatureBand in referencingFeatureBands)
                {
                    if (!installedSdkFeatureBands.Contains(referencingFeatureBand))
                    {
                        //  If an SDK feature band is no longer installed, manifests it references can be garbage collected
                        featureBandsToRemove.Add(referencingFeatureBand);
                    }

                    if (referencingFeatureBand.Equals(_sdkFeatureBand) && !garbageCollector.ManifestsToKeep.Contains((manifestId, manifestVersion, manifestFeatureBand)))
                    {
                        //  For current feature band, garbage collect manifests that the garbage collector didn't mark as ones to keep
                        featureBandsToRemove.Add(referencingFeatureBand);
                    }
                }

                foreach (var featureBandToRemove in featureBandsToRemove)
                {
                    RemoveManifestInstallationRecord(manifestId, manifestVersion, manifestFeatureBand, featureBandToRemove);
                }

                if (featureBandsToRemove.Count == referencingFeatureBands.Count)
                {
                    //  All installation records for the manifest were removed, so we can delete the manifest
                    _reporter.WriteLine(string.Format(LocalizableStrings.DeletingWorkloadManifest, manifestId, $"{manifestVersion}/{manifestFeatureBand}"));
                    var manifestPath = Path.Combine(GetManifestInstallDirForFeatureBand(manifestFeatureBand.ToString()), manifestId.ToString(), manifestVersion.ToString());
                    Directory.Delete(manifestPath, true);
                }
            }

            // Garbage collect workload packs
            var featureBandsWithWorkloadInstallationRecords = _installationRecordRepository.GetFeatureBandsWithInstallationRecords();

            Dictionary<(WorkloadPackId packId, string packVersion), List<SdkFeatureBand>> packInstallRecords = GetAllPackInstallRecords();
            foreach (var (packId, packVersion) in packInstallRecords.Keys)
            {
                var referencingFeatureBands = packInstallRecords[(packId, packVersion)];
                List<SdkFeatureBand> featureBandsToRemove = new();
                foreach (var referencingFeatureBand in referencingFeatureBands)
                {
                    if (cleanAllPacks)
                    {
                        //  Remove pack record if we're cleaning all packs
                        featureBandsToRemove.Add(referencingFeatureBand);
                    }
                    else if (!installedSdkFeatureBands.Contains(referencingFeatureBand) ||
                             !featureBandsWithWorkloadInstallationRecords.Contains(referencingFeatureBand))
                    {
                        //  Remove pack record if corresponding feature band is not installed, or does not have any workloads installed
                        featureBandsToRemove.Add(referencingFeatureBand);
                    }
                    else if (referencingFeatureBand.Equals(_sdkFeatureBand) && !garbageCollector.PacksToKeep.Contains((packId, packVersion)))
                    {
                        //  For current feature band, garbage collect packs that the garbage collector didn't mark as ones to keep
                        featureBandsToRemove.Add(referencingFeatureBand);
                    }
                }

                if (!featureBandsToRemove.Any())
                {
                    continue;
                }

                // Save the pack info in case we need to delete the pack (the pack type is needed to know the path to the pack and how to delete it)
                var jsonPackInfo = File.ReadAllText(GetPackInstallRecordPath(packId, packVersion, featureBandsToRemove.First()));

                foreach (var featureBand in featureBandsToRemove)
                {
                    File.Delete(GetPackInstallRecordPath(packId, packVersion, featureBand));
                }

                var installationRecordDirectory = Path.GetDirectoryName(GetPackInstallRecordPath(packId, packVersion, featureBandsToRemove.First()));
                if (!Directory.GetFileSystemEntries(installationRecordDirectory).Any())
                {
                    //  There are no installation records for the workload pack anymore, so we can delete the pack
                    var packToDelete = JsonSerializer.Deserialize<PackInfo>(jsonPackInfo);
                    DeletePack(packToDelete);

                    //  Delete now-empty pack installation record directory
                    Directory.Delete(installationRecordDirectory);

                    //  And delete the parent directory if it's also empty
                    string packIdDirectory = Path.GetDirectoryName(installationRecordDirectory);
                    if (!Directory.GetFileSystemEntries(packIdDirectory).Any())
                    {
                        Directory.Delete(packIdDirectory);
                    }
                }
            }

            if (cleanAllPacks)
            {
                DeleteAllWorkloadInstallationRecords();
            }

        }

        public void AdjustWorkloadSetInInstallState(SdkFeatureBand sdkFeatureBand, string workloadVersion)
        {
            UpdateInstallState(sdkFeatureBand, contents => contents.WorkloadVersion = workloadVersion);
        }

        public void RemoveManifestsFromInstallState(SdkFeatureBand sdkFeatureBand)
        {
            UpdateInstallState(sdkFeatureBand, contents => contents.Manifests = null);
        }

        public void SaveInstallStateManifestVersions(SdkFeatureBand sdkFeatureBand, Dictionary<string, string> manifestContents)
        {
            UpdateInstallState(sdkFeatureBand, contents => contents.Manifests = manifestContents);
        }

        public void UpdateInstallMode(SdkFeatureBand sdkFeatureBand, bool? newMode)
        {
            UpdateInstallState(sdkFeatureBand, contents => contents.UseWorkloadSets = newMode);

            var newModeString = newMode == null ? "<null>" : (newMode.Value ? WorkloadConfigCommandParser.UpdateMode_WorkloadSet : WorkloadConfigCommandParser.UpdateMode_Manifests);
            _reporter.WriteLine(string.Format(LocalizableStrings.UpdatedWorkloadMode, newModeString));
        }

        private void UpdateInstallState(SdkFeatureBand sdkFeatureBand, Action<InstallStateContents> update)
        {
            string path = Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, _workloadRootDir), "default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var installStateContents = InstallStateContents.FromPath(path);
            update(installStateContents);
            File.WriteAllText(path, installStateContents.ToString());
        }

        public void RecordWorkloadSetInGlobalJson(SdkFeatureBand sdkFeatureBand, string globalJsonPath, string workloadSetVersion)
        {
            new GlobalJsonWorkloadSetsFile(sdkFeatureBand, _workloadRootDir).RecordWorkloadSetInGlobalJson(globalJsonPath, workloadSetVersion);
        }

        public Dictionary<string, string> GetGlobalJsonWorkloadSetVersions(SdkFeatureBand sdkFeatureBand)
        {
            return new GlobalJsonWorkloadSetsFile(sdkFeatureBand, _workloadRootDir).GetGlobalJsonWorkloadSetVersions();
        }

        /// <summary>
        /// Remove all workload installation records that aren't from Visual Studio.
        /// </summary>
        private void DeleteAllWorkloadInstallationRecords()
        {
            FileBasedInstallationRecordRepository workloadRecordRepository = new(_workloadMetadataDir);
            var allFeatureBands = workloadRecordRepository.GetFeatureBandsWithInstallationRecords();

            foreach (SdkFeatureBand potentialBandToClean in allFeatureBands)
            {
                var workloadInstallationRecordIds = workloadRecordRepository.GetInstalledWorkloads(potentialBandToClean);
                foreach (WorkloadId workloadInstallationRecordId in workloadInstallationRecordIds)
                {
                    workloadRecordRepository.DeleteWorkloadInstallationRecord(workloadInstallationRecordId, potentialBandToClean);
                }
            }
        }

        string GetWorkloadHistoryDirectory()
        {
            return Path.Combine(_workloadMetadataDir, RuntimeInformation.ProcessArchitecture.ToString(), _sdkFeatureBand.ToString(), HistoryDir);
        }

        public void WriteWorkloadHistoryRecord(WorkloadHistoryRecord workloadHistoryRecord, string sdkFeatureBand)
        {
            var historyDirectory = GetWorkloadHistoryDirectory();
            Directory.CreateDirectory(historyDirectory);
            string logFile = Path.Combine(historyDirectory, $"{workloadHistoryRecord.TimeStarted:yyyy'-'MM'-'dd'T'HHmmss}_{workloadHistoryRecord.CommandName}.json");
            File.WriteAllText(logFile, JsonSerializer.Serialize(workloadHistoryRecord, new JsonSerializerOptions() { WriteIndented = true }));
        }

        public IEnumerable<WorkloadHistoryRecord> GetWorkloadHistoryRecords(string sdkFeatureBand)
        {
            return WorkloadFileBasedInstall.GetWorkloadHistoryRecords(GetWorkloadHistoryDirectory());
        }

        public void Shutdown()
        {
            // Perform any additional cleanup here that's intended to run at the end of the command, regardless
            // of success or failure. For file based installs, there shouldn't be any additional work to 
            // perform.
        }

        public PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand)
        {
            if (manifestId.ToString().Equals("Microsoft.NET.Workloads", StringComparison.OrdinalIgnoreCase))
            {
                return new PackageId($"{manifestId}.{featureBand}");
            }
            else
            {
                return new PackageId($"{manifestId}.Manifest-{featureBand}");
            }
        }

        public async Task ExtractManifestAsync(string nupkgPath, string targetPath)
        {
            var extractionPath = Path.Combine(_tempPackagesDir.Value, "dotnet-sdk-advertising-temp", $"{Path.GetFileName(nupkgPath)}-extracted");
            if (Directory.Exists(extractionPath))
            {
                Directory.Delete(extractionPath, true);
            }

            try
            {
                Directory.CreateDirectory(extractionPath);
                await _nugetPackageDownloader.ExtractPackageAsync(nupkgPath, new DirectoryPath(extractionPath));
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(Path.Combine(extractionPath, "data"), targetPath));
            }
            finally
            {
                if (!string.IsNullOrEmpty(extractionPath) && Directory.Exists(extractionPath))
                {
                    Directory.Delete(extractionPath, true);
                }
            }
        }

        private bool PackIsInstalled(PackInfo packInfo)
        {
            if (IsSingleFilePack(packInfo))
            {
                return File.Exists(packInfo.Path);
            }
            else
            {
                return Directory.Exists(packInfo.Path);
            }
        }

        private void DeletePack(PackInfo packInfo)
        {
            if (PackIsInstalled(packInfo))
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.DeletingWorkloadPack, packInfo.Id, packInfo.Version));
                if (IsSingleFilePack(packInfo))
                {
                    File.Delete(packInfo.Path);
                }
                else
                {
                    Directory.Delete(packInfo.Path, true);
                    var packIdDir = Path.GetDirectoryName(packInfo.Path);
                    if (!Directory.EnumerateFileSystemEntries(packIdDir).Any())
                    {
                        Directory.Delete(packIdDir, true);
                    }
                }
            }
        }


        //  Workload sets and workload manifests have a feature band which is essentially part of their version, and may be installed by a later feature band of the SDK.
        //  So there are two potentially different feature bands as part of the installation record
        string GetWorkloadSetInstallRecordPath(string workloadSetVersion, SdkFeatureBand workloadSetFeatureBand, SdkFeatureBand referencingFeatureBand) =>
            Path.Combine(_workloadMetadataDir, InstalledWorkloadSetsDir, "v1", workloadSetVersion, workloadSetFeatureBand.ToString(), referencingFeatureBand.ToString());

        void WriteWorkloadSetInstallationRecord(string workloadSetVersion, SdkFeatureBand workloadSetFeatureBand, SdkFeatureBand referencingFeatureBand)
        {
            var path = GetWorkloadSetInstallRecordPath(workloadSetVersion, workloadSetFeatureBand, referencingFeatureBand);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var _ = File.Create(path);
        }

        void RemoveWorkloadSetInstallationRecord(string workloadSetVersion, SdkFeatureBand workloadSetFeatureBand, SdkFeatureBand referencingFeatureBand)
        {
            var path = GetWorkloadSetInstallRecordPath(workloadSetVersion, workloadSetFeatureBand, referencingFeatureBand);
            PathUtility.DeleteFileAndEmptyParents(path, maxDirectoriesToDelete: 2);
        }

        private Dictionary<(string workloadSetVersion, SdkFeatureBand workloadSetFeatureBand), List<SdkFeatureBand>> GetAllWorkloadSetInstallRecords()
        {
            Dictionary<(string workloadSetVersion, SdkFeatureBand workloadSetFeatureBand), List<SdkFeatureBand>> records = new();

            var installedWorkloadSetsDir = Path.Combine(_workloadMetadataDir, InstalledWorkloadSetsDir, "v1");

            if (!Directory.Exists(installedWorkloadSetsDir))
            {
                return records;
            }

            foreach (var workloadSetVersionDir in Directory.GetDirectories(installedWorkloadSetsDir))
            {
                var workloadSetVersion = Path.GetFileName(workloadSetVersionDir);
                foreach (var workloadSetFeatureBandDir in Directory.GetDirectories(workloadSetVersionDir))
                {
                    var workloadSetFeatureBand = new SdkFeatureBand(Path.GetFileName(workloadSetFeatureBandDir));
                    foreach (var featureBandInstallationRecord in Directory.GetFileSystemEntries(workloadSetFeatureBandDir))
                    {
                        var referencingFeatureBand = new SdkFeatureBand(Path.GetFileName(featureBandInstallationRecord));
                        if (!records.TryGetValue((workloadSetVersion, workloadSetFeatureBand), out var referencingFeatureBands))
                        {
                            referencingFeatureBands = new List<SdkFeatureBand>();
                            records[(workloadSetVersion, workloadSetFeatureBand)] = referencingFeatureBands;
                        }

                        referencingFeatureBands.Add(referencingFeatureBand);
                    }
                }
            }

            return records;
        }

        private string GetManifestInstallRecordPath(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand featureBand, SdkFeatureBand referencingFeatureBand) =>
            Path.Combine(_workloadMetadataDir, InstalledManifestsDir, "v1", manifestId.ToString(), manifestVersion.ToString(), featureBand.ToString(), referencingFeatureBand.ToString());

        void WriteManifestInstallationRecord(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand featureBand, SdkFeatureBand referencingFeatureBand)
        {
            var path = GetManifestInstallRecordPath(manifestId, manifestVersion, featureBand, referencingFeatureBand);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var _ = File.Create(path);
        }

        void RemoveManifestInstallationRecord(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand featureBand, SdkFeatureBand referencingFeatureBand)
        {
            var installationRecordPath = GetManifestInstallRecordPath(manifestId, manifestVersion, featureBand, referencingFeatureBand);
            PathUtility.DeleteFileAndEmptyParents(installationRecordPath, maxDirectoriesToDelete: 3);
        }

        private Dictionary<(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand manifestFeatureBand), List<SdkFeatureBand>> GetAllManifestInstallRecords()
        {
            Dictionary<(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand manifestFeatureBand), List<SdkFeatureBand>> records = new();

            var installedManifestsDir = Path.Combine(_workloadMetadataDir, InstalledManifestsDir, "v1");

            if (!Directory.Exists(installedManifestsDir))
            {
                return records;
            }

            foreach (var manifestIdDir in Directory.GetDirectories(installedManifestsDir))
            {
                var manifestId = new ManifestId(Path.GetFileName(manifestIdDir));
                foreach (var manifestVersionDir in Directory.GetDirectories(manifestIdDir))
                {
                    var manifestVersion = new ManifestVersion(Path.GetFileName(manifestVersionDir));
                    foreach (var manifestFeatureBandDir in Directory.GetDirectories(manifestVersionDir))
                    {
                        var manifestFeatureBand = new SdkFeatureBand(Path.GetFileName(manifestFeatureBandDir));
                        foreach (var featureBandInstallationRecord in Directory.GetFileSystemEntries(manifestFeatureBandDir))
                        {
                            var referencingFeatureBand = new SdkFeatureBand(Path.GetFileName(featureBandInstallationRecord));

                            if (!records.TryGetValue((manifestId, manifestVersion, manifestFeatureBand), out var referencingFeatureBands))
                            {
                                referencingFeatureBands = new List<SdkFeatureBand>();
                                records[(manifestId, manifestVersion, manifestFeatureBand)] = referencingFeatureBands;
                            }

                            referencingFeatureBands.Add(referencingFeatureBand);
                        }
                    }
                }
            }

            return records;
        }

        private string GetPackInstallRecordPath(WorkloadPackId packId, string packVersion, SdkFeatureBand featureBand) =>
            Path.Combine(_workloadMetadataDir, InstalledPacksDir, "v1", packId, packVersion, featureBand.ToString());

        private Dictionary<(WorkloadPackId packId, string packVersion), List<SdkFeatureBand>> GetAllPackInstallRecords()
        {
            Dictionary<(WorkloadPackId packId, string packVersion), List<SdkFeatureBand>> records = new();

            var installedPacksDir = Path.Combine(_workloadMetadataDir, InstalledPacksDir, "v1");

            if (!Directory.Exists(installedPacksDir))
            {
                return records;
            }

            foreach (var packIdDir in Directory.GetDirectories(installedPacksDir))
            {
                var packId = new WorkloadPackId(Path.GetFileName(packIdDir));
                foreach (var packVersionDir in Directory.GetDirectories(packIdDir))
                {
                    var packVersion = Path.GetFileName(packVersionDir);
                    foreach (var bandRecord in Directory.GetFileSystemEntries(packVersionDir))
                    {
                        var referencingFeatureBand = new SdkFeatureBand(Path.GetFileName(bandRecord));

                        if (!records.TryGetValue((packId, packVersion), out var referencingFeatureBands))
                        {
                            referencingFeatureBands = new List<SdkFeatureBand>();
                            records[(packId, packVersion)] = referencingFeatureBands;
                        }
                        referencingFeatureBands.Add(referencingFeatureBand);
                    }
                }
            }

            return records;
        }

        private void WritePackInstallationRecord(PackInfo packInfo, SdkFeatureBand featureBand)
        {
            _reporter.WriteLine(string.Format(LocalizableStrings.WritingPackInstallRecordMessage, packInfo.ResolvedPackageId, packInfo.Version));
            var path = GetPackInstallRecordPath(new WorkloadPackId(packInfo.ResolvedPackageId), packInfo.Version, featureBand);
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            File.WriteAllText(path, JsonSerializer.Serialize(packInfo));
        }

        private void DeletePackInstallationRecord(PackInfo packInfo, SdkFeatureBand featureBand)
        {
            var packInstallRecord = GetPackInstallRecordPath(new WorkloadPackId(packInfo.ResolvedPackageId), packInfo.Version, featureBand);
            if (File.Exists(packInstallRecord))
            {
                File.Delete(packInstallRecord);

                var packRecordVersionDir = Path.GetDirectoryName(packInstallRecord);
                if (!Directory.GetFileSystemEntries(packRecordVersionDir).Any())
                {
                    Directory.Delete(packRecordVersionDir);

                    var packRecordIdDir = Path.GetDirectoryName(packRecordVersionDir);
                    if (!Directory.GetFileSystemEntries(packRecordIdDir).Any())
                    {
                        Directory.Delete(packRecordIdDir);
                    }
                }
            }
        }

        private bool PackHasInstallRecords(PackInfo packInfo)
        {
            var packInstallRecordDir = Path.Combine(_workloadMetadataDir, InstalledPacksDir, "v1", packInfo.Id, packInfo.Version);
            return Directory.Exists(packInstallRecordDir) && Directory.GetFiles(packInstallRecordDir).Any();
        }

        private bool IsSingleFilePack(PackInfo packInfo) => packInfo.Kind.Equals(WorkloadPackKind.Library) || packInfo.Kind.Equals(WorkloadPackKind.Template);
    }
}
