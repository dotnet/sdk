// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
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

        // Get the workload set directly from the provider - it was already resolved during construction
        // and doesn't require reading the install state file again
        var provider = _workloadResolver.GetWorkloadManifestProvider() as SdkDirectoryWorkloadManifestProvider;
        var workloadSet = provider?.ResolvedWorkloadSet;

        if (workloadSet is null)
        {
            // No workload set is being used
            return;
        }

        if (!provider?.HasMissingManifests(workloadSet) ?? true)
        {
            return;
        }

        if (failureMode == ManifestCorruptionFailureMode.Throw)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.WorkloadSetHasMissingManifests, workloadSet.Version));
        }

        _reporter.WriteLine($"Repairing workload set {workloadSet.Version}...");
        CliTransaction.RunNew(context => RepairCorruptWorkloadSet(context, workloadSet));
    }



    private void RepairCorruptWorkloadSet(ITransactionContext context, WorkloadSet workloadSet)
    {
        var manifestUpdates = CreateManifestUpdatesFromWorkloadSet(workloadSet);

        foreach (var manifestUpdate in manifestUpdates)
        {
            _workloadInstaller.InstallWorkloadManifest(manifestUpdate, context);
        }

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
