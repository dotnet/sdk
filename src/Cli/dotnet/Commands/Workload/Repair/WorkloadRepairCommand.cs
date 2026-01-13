// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Repair;

internal class WorkloadRepairCommand : WorkloadCommandBase
{
    private readonly PackageSourceLocation _packageSourceLocation;
    private readonly IInstaller _workloadInstaller;
    protected readonly IWorkloadResolverFactory _workloadResolverFactory;
    private readonly IWorkloadResolver _workloadResolver;
    private readonly ReleaseVersion _sdkVersion;
    private readonly string _dotnetPath;
    private readonly string _userProfileDir;
    private readonly WorkloadHistoryRecorder _recorder;

    public WorkloadRepairCommand(
        ParseResult parseResult,
        IReporter reporter = null,
        IWorkloadResolverFactory workloadResolverFactory = null,
        IInstaller workloadInstaller = null,
        INuGetPackageDownloader nugetPackageDownloader = null)
        : base(parseResult, verbosityOptions: WorkloadRepairCommandParser.VerbosityOption, reporter: reporter, nugetPackageDownloader: nugetPackageDownloader)
    {
        var configOption = parseResult.GetValue(WorkloadRepairCommandParser.ConfigOption);
        var sourceOption = parseResult.GetValue(WorkloadRepairCommandParser.SourceOption);
        _packageSourceLocation = string.IsNullOrEmpty(configOption) && (sourceOption == null || !sourceOption.Any()) ? null :
            new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), sourceFeedOverrides: sourceOption);

        _workloadResolverFactory = workloadResolverFactory ?? new WorkloadResolverFactory();

        if (!string.IsNullOrEmpty(parseResult.GetValue(WorkloadRepairCommandParser.VersionOption)))
        {
            throw new GracefulException(CliCommandStrings.SdkVersionOptionNotSupported);
        }

        var creationResult = _workloadResolverFactory.Create();

        _dotnetPath = creationResult.DotnetPath;
        _sdkVersion = creationResult.SdkVersion;
        _userProfileDir = creationResult.UserProfileDir;
        var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
        _workloadResolver = creationResult.WorkloadResolver;

        _workloadInstaller = workloadInstaller ??
                             WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand,
                                 _workloadResolver, Verbosity, creationResult.UserProfileDir, VerifySignatures, PackageDownloader, _dotnetPath, TempDirectoryPath,
                                 _packageSourceLocation, _parseResult.ToRestoreActionConfig());

        _recorder = new WorkloadHistoryRecorder(_workloadResolver, _workloadInstaller, () => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, null));
        _recorder.HistoryRecord.CommandName = "repair";
    }

    public override int Execute()
    {
        try
        {
            _recorder.Run(() =>
            {
                Reporter.WriteLine();

                var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);

                // Repair missing manifests first if workload set is corrupt
                if (IsCorruptWorkloadSet(sdkFeatureBand))
                {
                    CliTransaction.RunNew(context => RepairCorruptWorkloadSet(context, sdkFeatureBand));
                }

                var workloadIds = _workloadInstaller.GetWorkloadInstallationRecordRepository().GetInstalledWorkloads(sdkFeatureBand);

                if (!workloadIds.Any())
                {
                    Reporter.WriteLine(CliCommandStrings.NoWorkloadsToRepair);
                    return;
                }

                Reporter.WriteLine(string.Format(CliCommandStrings.RepairingWorkloads, string.Join(" ", workloadIds)));

                ReinstallWorkloadsBasedOnCurrentManifests(workloadIds, sdkFeatureBand);

                WorkloadInstallCommand.TryRunGarbageCollection(_workloadInstaller, Reporter, Verbosity, workloadSetVersion => _workloadResolverFactory.CreateForWorkloadSet(_dotnetPath, _sdkVersion.ToString(), _userProfileDir, workloadSetVersion));

                Reporter.WriteLine();
                Reporter.WriteLine(string.Format(CliCommandStrings.RepairSucceeded, string.Join(" ", workloadIds)));
                Reporter.WriteLine();
            });
        }
        catch (Exception e)
        {
            // Don't show entire stack trace
            throw new GracefulException(string.Format(CliCommandStrings.WorkloadRepairFailed, e.Message), e, isUserError: false);
        }
        finally
        {
            _workloadInstaller.Shutdown();
        }

        return _workloadInstaller.ExitCode;
    }

    private void ReinstallWorkloadsBasedOnCurrentManifests(IEnumerable<WorkloadId> workloadIds, SdkFeatureBand sdkFeatureBand)
    {
        _workloadInstaller.RepairWorkloads(workloadIds, sdkFeatureBand);
    }

    /// <summary>
    /// Checks if the workload set installation is corrupt by verifying that all manifests
    /// referenced in the workload set exist on disk.
    /// </summary>
    /// <returns>True if using workload sets and any referenced manifests are missing; false otherwise.</returns>
    private bool IsCorruptWorkloadSet(SdkFeatureBand sdkFeatureBand)
    {
        var installState = GetInstallState(sdkFeatureBand);

        // Only check for corruption if using workload sets
        if (string.IsNullOrEmpty(installState.WorkloadVersion))
        {
            return false;
        }

        try
        {
            var workloadSet = _workloadInstaller.GetWorkloadSetContents(installState.WorkloadVersion);
            return HasMissingManifests(workloadSet);
        }
        catch
        {
            // If we can't read the workload set, consider it not corrupt
            // (it might not be installed at all)
            return false;
        }
    }

    /// <summary>
    /// Checks if any manifests referenced by the workload set are missing from disk.
    /// </summary>
    private bool HasMissingManifests(WorkloadSet workloadSet)
    {
        string manifestRoot = Path.Combine(_dotnetPath, "sdk-manifests");

        foreach (var manifestEntry in workloadSet.ManifestVersions)
        {
            string manifestId = manifestEntry.Key.ToString();
            string manifestVersion = manifestEntry.Value.Version.ToString();
            string manifestFeatureBand = manifestEntry.Value.FeatureBand.ToString();

            // Check if manifest directory exists
            string manifestDirectory = Path.Combine(manifestRoot, manifestFeatureBand, manifestId, manifestVersion);
            if (!Directory.Exists(manifestDirectory))
            {
                return true;
            }

            // Check if WorkloadManifest.json file exists
            string manifestFile = Path.Combine(manifestDirectory, "WorkloadManifest.json");
            if (!File.Exists(manifestFile))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Repairs a corrupt workload set by reinstalling all manifests referenced in the workload set.
    /// </summary>
    private void RepairCorruptWorkloadSet(ITransactionContext context, SdkFeatureBand sdkFeatureBand)
    {
        var installState = GetInstallState(sdkFeatureBand);
        
        Reporter.WriteLine($"Repairing workload set {installState.WorkloadVersion}...");

        var workloadSet = _workloadInstaller.GetWorkloadSetContents(installState.WorkloadVersion);
        var manifestUpdates = CreateManifestUpdatesFromWorkloadSet(workloadSet);

        foreach (var manifestUpdate in manifestUpdates)
        {
            _workloadInstaller.InstallWorkloadManifest(manifestUpdate, context);
        }

        // Refresh resolver to pick up newly installed manifests
        _workloadResolver.RefreshWorkloadManifests();
    }

    /// <summary>
    /// Reads the install state for the current SDK feature band.
    /// </summary>
    private InstallStateContents GetInstallState(SdkFeatureBand sdkFeatureBand)
    {
        string manifestRoot = Path.Combine(_dotnetPath, "sdk-manifests");
        string installStateFolder = WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, Path.GetDirectoryName(manifestRoot));
        string installStatePath = Path.Combine(installStateFolder, "default.json");
        
        return InstallStateContents.FromPath(installStatePath);
    }

    /// <summary>
    /// Creates ManifestVersionUpdate objects from a workload set for installation.
    /// </summary>
    private IEnumerable<ManifestVersionUpdate> CreateManifestUpdatesFromWorkloadSet(WorkloadSet workloadSet)
    {
        var manifestUpdater = new WorkloadManifestUpdater(
            Reporter, 
            _workloadResolver, 
            PackageDownloader, 
            _userProfileDir, 
            _workloadInstaller.GetWorkloadInstallationRecordRepository(), 
            _workloadInstaller, 
            _packageSourceLocation, 
            displayManifestUpdates: Verbosity.IsDetailedOrDiagnostic());

        return manifestUpdater.CalculateManifestUpdatesForWorkloadSet(workloadSet);
    }

