// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.Commands.Workload.List;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal class WorkloadInfoHelper : IWorkloadInfoHelper
{
    public readonly SdkFeatureBand _currentSdkFeatureBand;
    private readonly string? _targetSdkVersion;
    public string DotnetPath { get; }
    public string UserLocalPath { get; }

    public WorkloadInfoHelper(
        bool isInteractive,
        VerbosityOptions verbosity = VerbosityOptions.normal,
        string? targetSdkVersion = null,
        bool? verifySignatures = null,
        IReporter? reporter = null,
        IWorkloadInstallationRecordRepository? workloadRecordRepo = null,
        string? currentSdkVersion = null,
        string? dotnetDir = null,
        string? userProfileDir = null,
        IWorkloadResolver? workloadResolver = null)
    {
        DotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath)!;
        ReleaseVersion currentSdkReleaseVersion = new(currentSdkVersion ?? Product.Version);
        _currentSdkFeatureBand = new SdkFeatureBand(currentSdkReleaseVersion);

        _targetSdkVersion = targetSdkVersion;
        userProfileDir ??= CliFolderPathCalculator.DotnetUserProfileFolderPath;
        ManifestProvider =
            new SdkDirectoryWorkloadManifestProvider(DotnetPath,
                string.IsNullOrWhiteSpace(_targetSdkVersion)
                    ? currentSdkReleaseVersion.ToString()
                    : _targetSdkVersion,
                userProfileDir, SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(Environment.CurrentDirectory));
        WorkloadResolver = workloadResolver ?? NET.Sdk.WorkloadManifestReader.WorkloadResolver.Create(
            ManifestProvider, DotnetPath,
            currentSdkReleaseVersion.ToString(), userProfileDir);

        var restoreConfig = new RestoreActionConfig(Interactive: isInteractive);

        Installer = WorkloadInstallerFactory.GetWorkloadInstaller(
            reporter,
            _currentSdkFeatureBand,
            WorkloadResolver,
            verbosity,
            userProfileDir,
            verifySignatures ?? !SignCheck.IsDotNetSigned(),
            restoreActionConfig: restoreConfig,
            elevationRequired: false,
            shouldLog: false);

        WorkloadRecordRepo = workloadRecordRepo ?? Installer.GetWorkloadInstallationRecordRepository();

        UserLocalPath = dotnetDir ?? (WorkloadFileBasedInstall.IsUserLocal(DotnetPath, _currentSdkFeatureBand.ToString()) ? userProfileDir : DotnetPath);
    }

    public IInstaller Installer { get; private init; }
    public SdkDirectoryWorkloadManifestProvider ManifestProvider { get; }
    public IWorkloadInstallationRecordRepository WorkloadRecordRepo { get; private init; }
    public IWorkloadResolver WorkloadResolver { get; private init; }

    public IEnumerable<WorkloadId> InstalledSdkWorkloadIds => WorkloadRecordRepo.GetInstalledWorkloads(_currentSdkFeatureBand);

    public InstalledWorkloadsCollection AddInstalledVsWorkloads(IEnumerable<WorkloadId> sdkWorkloadIds)
    {
        InstalledWorkloadsCollection installedWorkloads = new(sdkWorkloadIds, $"SDK {_currentSdkFeatureBand}");
#if !DOT_NET_BUILD_FROM_SOURCE
        if (OperatingSystem.IsWindows())
        {
            VisualStudioWorkloads.GetInstalledWorkloads(WorkloadResolver, installedWorkloads);
        }
#endif
        return installedWorkloads;
    }

    public void CheckTargetSdkVersionIsValid()
    {
        if (!string.IsNullOrWhiteSpace(_targetSdkVersion))
        {
            if (new SdkFeatureBand(_targetSdkVersion).CompareTo(_currentSdkFeatureBand) < 0)
            {
                throw new ArgumentException(
                    $"Version band of {_targetSdkVersion} --- {new SdkFeatureBand(_targetSdkVersion)} should not be smaller than current version band {_currentSdkFeatureBand}");
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<WorkloadResolver.WorkloadInfo> InstalledAndExtendedWorkloads
    {
        get
        {
            var installed = AddInstalledVsWorkloads(InstalledSdkWorkloadIds);

            return WorkloadResolver.GetExtendedWorkloads(
                installed.AsEnumerable().Select(t => new WorkloadId(t.Key)));
        }
    }

    internal static string GetWorkloadsVersion(WorkloadInfoHelper? workloadInfoHelper = null)
    {
        workloadInfoHelper ??= new WorkloadInfoHelper(false);

        var versionInfo = workloadInfoHelper.ManifestProvider.GetWorkloadVersion();

        // The explicit space here is intentional, as it's easy to miss in localization and crucial for parsing
        return versionInfo.Version + (versionInfo.IsInstalled ? string.Empty : ' ' + CliCommandStrings.WorkloadVersionNotInstalledShort);
    }

    internal void ShowWorkloadsInfo(IReporter? reporter = null, string? dotnetDir = null, bool showVersion = true)
    {
        reporter ??= Reporter.Output;
        var versionInfo = ManifestProvider.GetWorkloadVersion();

        void WriteUpdateModeAndAnyError(string indent = "")
        {
            var useWorkloadSets = InstallStateContents.FromPath(Path.Combine(WorkloadInstallType.GetInstallStateFolder(_currentSdkFeatureBand, UserLocalPath), "default.json")).ShouldUseWorkloadSets();
            var configurationMessage = useWorkloadSets
                ? CliCommandStrings.WorkloadManifestInstallationConfigurationWorkloadSets
                : CliCommandStrings.WorkloadManifestInstallationConfigurationLooseManifests;
            reporter.WriteLine(indent + configurationMessage);

            if (!versionInfo.IsInstalled)
            {
                reporter.WriteLine(indent + string.Format(CliCommandStrings.WorkloadSetFromGlobalJsonNotInstalled, versionInfo.Version, versionInfo.GlobalJsonPath));
            }
            else if (versionInfo.WorkloadSetsEnabledWithoutWorkloadSet)
            {
                reporter.WriteLine(indent + CliCommandStrings.ShouldInstallAWorkloadSet);
            }
        }

        if (showVersion)
        {
            reporter.WriteLine($" Workload version: {GetWorkloadsVersion()}");

            WriteUpdateModeAndAnyError(indent: " ");
            reporter.WriteLine();
        }

        if (versionInfo.IsInstalled)
        {
            var installedList = InstalledSdkWorkloadIds;
            var installedWorkloads = AddInstalledVsWorkloads(installedList);
            var dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);

            if (installedWorkloads.Count == 0)
            {
                reporter.WriteLine(CliCommandStrings.NoWorkloadsInstalledInfoWarning);
            }
            else
            {
                var manifestInfoDict = WorkloadResolver.GetInstalledManifests().ToDictionary(info => info.Id, StringComparer.OrdinalIgnoreCase);

                foreach (var workload in installedWorkloads.AsEnumerable())
                {
                    var workloadManifest = WorkloadResolver.GetManifestFromWorkload(new WorkloadId(workload.Key));
                    var workloadFeatureBand = manifestInfoDict[workloadManifest.Id].ManifestFeatureBand;

                    const int align = 10;
                    const string separator = "   ";

                    reporter.WriteLine($" [{workload.Key}]");

                    reporter.Write($"{separator}{CliCommandStrings.WorkloadSourceColumn}:");
                    reporter.WriteLine($" {workload.Value,align}");

                    reporter.Write($"{separator}{CliCommandStrings.WorkloadManifestVersionColumn}:");
                    reporter.WriteLine($"    {workloadManifest.Version + '/' + workloadFeatureBand,align}");

                    reporter.Write($"{separator}{CliCommandStrings.WorkloadManifestPathColumn}:");
                    reporter.WriteLine($"       {workloadManifest.ManifestPath,align}");

                    reporter.Write($"{separator}{CliCommandStrings.WorkloadInstallTypeColumn}:");
                    reporter.WriteLine($"       {WorkloadInstallType.GetWorkloadInstallType(new SdkFeatureBand(Product.Version), dotnetPath).ToString(),align}"
                    );
                    reporter.WriteLine("");
                }
            }
        }

        if (!showVersion)
        {
            WriteUpdateModeAndAnyError();
        }
    }
}
