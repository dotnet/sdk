// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.Localization;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal sealed class WorkloadManifestCorruptionRepairer : IWorkloadManifestCorruptionRepairer
{
    private readonly IReporter _reporter;
    private readonly IInstaller _workloadInstaller;
    private readonly IWorkloadResolver _workloadResolver;
    private readonly SdkFeatureBand _sdkFeatureBand;
    private readonly string _dotnetPath;
    private readonly string _userProfileDir;
    private readonly INuGetPackageDownloader? _packageDownloader;
    private readonly PackageSourceLocation? _packageSourceLocation;
    private readonly VerbosityOptions _verbosity;

    private bool _checked;

    public WorkloadManifestCorruptionRepairer(
        IReporter reporter,
        IInstaller workloadInstaller,
        IWorkloadResolver workloadResolver,
        SdkFeatureBand sdkFeatureBand,
        string dotnetPath,
        string userProfileDir,
        INuGetPackageDownloader? packageDownloader,
        PackageSourceLocation? packageSourceLocation,
        VerbosityOptions verbosity)
    {
        _reporter = reporter ?? NullReporter.Instance;
        _workloadInstaller = workloadInstaller;
        _workloadResolver = workloadResolver;
        _sdkFeatureBand = sdkFeatureBand;
        _dotnetPath = dotnetPath;
        _userProfileDir = userProfileDir;
        _packageDownloader = packageDownloader;
        _packageSourceLocation = packageSourceLocation;
        _verbosity = verbosity;
    }

    public void EnsureManifestsHealthy(ManifestCorruptionFailureMode failureMode)
    {
        if (_checked)
        {
            return;
        }

        _checked = true;

        if (failureMode == ManifestCorruptionFailureMode.Ignore)
        {
            return;
        }

        InstallStateContents installState;
        try
        {
            installState = GetInstallState();
        }
        catch (FileNotFoundException)
        {
            return;
        }

        if (!installState.ShouldUseWorkloadSets() || string.IsNullOrEmpty(installState.WorkloadVersion))
        {
            return;
        }

        WorkloadSet workloadSet;
        try
        {
            workloadSet = _workloadInstaller.GetWorkloadSetContents(installState.WorkloadVersion);
        }
        catch
        {
            return;
        }

        if (!HasMissingManifests(workloadSet, _dotnetPath))
        {
            return;
        }

        if (failureMode == ManifestCorruptionFailureMode.Throw)
        {
            throw new InvalidOperationException(string.Format(Strings.WorkloadSetHasMissingManifests, installState.WorkloadVersion));
        }

        _reporter.WriteLine($"Repairing workload set {installState.WorkloadVersion}...");
        CliTransaction.RunNew(context => RepairCorruptWorkloadSet(context, workloadSet));
    }

    private InstallStateContents GetInstallState()
    {
        string installRoot = WorkloadFileBasedInstall.IsUserLocal(_dotnetPath, _sdkFeatureBand.ToString())
            ? _userProfileDir
            : _dotnetPath;

        string installStateFolder = WorkloadInstallType.GetInstallStateFolder(_sdkFeatureBand, installRoot);
        string installStatePath = Path.Combine(installStateFolder, "default.json");

        return InstallStateContents.FromPath(installStatePath);
    }

    public static bool HasMissingManifests(WorkloadSet workloadSet, string dotnetPath)
    {
        string manifestRoot = Path.Combine(dotnetPath, "sdk-manifests");

        foreach (var manifestEntry in workloadSet.ManifestVersions)
        {
            string manifestId = manifestEntry.Key.ToString();
            string manifestVersion = manifestEntry.Value.Version.ToString();
            string manifestFeatureBand = manifestEntry.Value.FeatureBand.ToString();

            string manifestDirectory = Path.Combine(manifestRoot, manifestFeatureBand, manifestId, manifestVersion);
            if (!Directory.Exists(manifestDirectory))
            {
                return true;
            }

            string manifestFile = Path.Combine(manifestDirectory, "WorkloadManifest.json");
            if (!File.Exists(manifestFile))
            {
                return true;
            }
        }

        return false;
    }

    private void RepairCorruptWorkloadSet(ITransactionContext context, WorkloadSet workloadSet)
    {
        var manifestUpdates = CreateManifestUpdatesFromWorkloadSet(workloadSet);

        foreach (var manifestUpdate in manifestUpdates)
        {
            _workloadInstaller.InstallWorkloadManifest(manifestUpdate, context);
        }
        _workloadResolver.RefreshWorkloadManifests();
    }

    [MemberNotNull(nameof(_packageDownloader))]
    private IEnumerable<ManifestVersionUpdate> CreateManifestUpdatesFromWorkloadSet(WorkloadSet workloadSet)
    {
        if (_packageDownloader is null)
        {
            throw new InvalidOperationException("Package downloader is required to repair workload manifests.");
        }

        var manifestUpdater = new WorkloadManifestUpdater(
            _reporter,
            _workloadResolver,
            _packageDownloader,
            _userProfileDir,
            _workloadInstaller.GetWorkloadInstallationRecordRepository(),
            _workloadInstaller,
            _packageSourceLocation,
            displayManifestUpdates: _verbosity >= VerbosityOptions.detailed);

        return manifestUpdater.CalculateManifestUpdatesForWorkloadSet(workloadSet);
    }
}
