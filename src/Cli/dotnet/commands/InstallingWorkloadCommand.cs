﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;
using Command = System.CommandLine.Command;
using Product = Microsoft.DotNet.Cli.Utils.Product;
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
        protected readonly ReleaseVersion _installedSdkVersion;
        protected readonly SdkFeatureBand _sdkFeatureBand;
        protected readonly SdkFeatureBand _installedFeatureBand;
        protected readonly string _fromRollbackDefinition;
        protected readonly PackageSourceLocation _packageSourceLocation;
        protected IWorkloadResolver _workloadResolver;
        protected readonly IInstaller _workloadInstallerFromConstructor;
        protected readonly IWorkloadManifestUpdater _workloadManifestUpdaterFromConstructor;
        protected IInstaller _workloadInstaller;
        protected IWorkloadManifestUpdater _workloadManifestUpdater;

        public InstallingWorkloadCommand(
            ParseResult parseResult,
            IReporter reporter,
            IWorkloadResolver workloadResolver,
            IInstaller workloadInstaller,
            INuGetPackageDownloader nugetPackageDownloader,
            IWorkloadManifestUpdater workloadManifestUpdater,
            string dotnetDir,
            string userProfileDir,
            string tempDirPath,
            string version,
            string installedFeatureBand = null)
            : base(parseResult, reporter: reporter, tempDirPath: tempDirPath, nugetPackageDownloader: nugetPackageDownloader)
        {
            _printDownloadLinkOnly = parseResult.GetValue(InstallingWorkloadCommandParser.PrintDownloadLinkOnlyOption);
            _fromCacheOption = parseResult.GetValue(InstallingWorkloadCommandParser.FromCacheOption);
            _includePreviews = parseResult.GetValue(InstallingWorkloadCommandParser.IncludePreviewOption);
            _downloadToCacheOption = parseResult.GetValue(InstallingWorkloadCommandParser.DownloadToCacheOption);
            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            _userProfileDir = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;
            _checkIfManifestExist = !(_printDownloadLinkOnly);      // don't check for manifest existence when print download link is passed
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValue(InstallingWorkloadCommandParser.VersionOption), version, _dotnetPath, _userProfileDir, _checkIfManifestExist);
            _sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _installedSdkVersion = new ReleaseVersion(version ?? Product.Version);
            _installedFeatureBand = new SdkFeatureBand(installedFeatureBand ?? Product.Version);

            _fromRollbackDefinition = parseResult.GetValue(InstallingWorkloadCommandParser.FromRollbackFileOption);
            var configOption = parseResult.GetValue(InstallingWorkloadCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValue(InstallingWorkloadCommandParser.SourceOption);
            _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
                new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: sourceOption);

            var sdkWorkloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _installedSdkVersion.ToString(), userProfileDir);
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(sdkWorkloadManifestProvider, _dotnetPath, _installedSdkVersion.ToString(), _userProfileDir);

            _workloadInstallerFromConstructor = workloadInstaller;
            _workloadManifestUpdaterFromConstructor = workloadManifestUpdater;
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

                    var manifestDownloads = await _workloadManifestUpdater.GetManifestPackageDownloadsAsync(includePreview, _sdkFeatureBand, _installedFeatureBand);

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
                    .Where(featureBand => featureBand.CompareTo(_installedFeatureBand) < 0);
                if (priorFeatureBands.Any())
                {
                    var maxPriorFeatureBand = priorFeatureBands.Max();
                    return _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(maxPriorFeatureBand);
                }
                return new List<WorkloadId>();
            }
            else
            {
                var workloads = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(_installedFeatureBand);

                return workloads ?? Enumerable.Empty<WorkloadId>();
            }
        }
    }

    internal static class InstallingWorkloadCommandParser
    {
        public static readonly Option<bool> PrintDownloadLinkOnlyOption =
            new Option<bool>("--print-download-link-only", Strings.PrintDownloadLinkOnlyDescription)
            {
                IsHidden = true
            };

        public static readonly Option<string> FromCacheOption = new Option<string>("--from-cache", Strings.FromCacheOptionDescription)
        {
            ArgumentHelpName = Strings.FromCacheOptionArgumentName,
            IsHidden = true
        };

        public static readonly Option<bool> IncludePreviewOption =
            new Option<bool>("--include-previews", Strings.IncludePreviewOptionDescription);

        public static readonly Option<string> DownloadToCacheOption = new Option<string>("--download-to-cache", Strings.DownloadToCacheOptionDescription)
        {
            ArgumentHelpName = Strings.DownloadToCacheOptionArgumentName,
            IsHidden = true
        };

        public static readonly Option<string> VersionOption =
            new Option<string>("--sdk-version", Strings.VersionOptionDescription)
            {
                ArgumentHelpName = Strings.VersionOptionName,
                IsHidden = true
            };

        public static readonly Option<string> FromRollbackFileOption = new Option<string>("--from-rollback-file", Update.LocalizableStrings.FromRollbackDefinitionOptionDescription)
        {
            IsHidden = true
        };

        public static readonly Option<string> ConfigOption =
            new Option<string>("--configfile", Strings.ConfigFileOptionDescription)
            {
                ArgumentHelpName = Strings.ConfigFileOptionName
            };

        public static readonly Option<string[]> SourceOption =
            new Option<string[]>(new string[] { "-s", "--source" }, Strings.SourceOptionDescription)
            {
                ArgumentHelpName = Strings.SourceOptionName
            }.AllowSingleArgPerToken();

        internal static void AddWorkloadInstallCommandOptions(Command command)
        {
            command.AddOption(VersionOption);
            command.AddOption(ConfigOption);
            command.AddOption(SourceOption);
            command.AddOption(PrintDownloadLinkOnlyOption);
            command.AddOption(FromCacheOption);
            command.AddOption(DownloadToCacheOption);
            command.AddOption(IncludePreviewOption);
            command.AddOption(FromRollbackFileOption);
        }
    }
}
