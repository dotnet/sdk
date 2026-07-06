// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload;

/// <summary>
///  Read-only, dependency-light detection of whether any workloads are installed for the running
///  SDK's feature band.
///
///  <para>
///  <see cref="WorkloadIntegrityChecker.RunFirstUseCheck"/> only performs an (expensive) repair when
///  installed workloads exist; the resolver/installer plumbing it uses to discover that fact drags in
///  the NuGet engine (and, on Windows, the MSI installer IPC). This type answers the same "are any
///  workloads installed?" question by reading only the filesystem and registry, so it has no NuGet,
///  MSBuild, cryptography, or installer-IPC dependencies and can run in-process under NativeAOT. The
///  repair itself stays on the managed CLI.
///  </para>
/// </summary>
internal static class WorkloadInstallDetector
{
    /// <summary>
    ///  Returns <see langword="true"/> if at least one workload is recorded as installed for the
    ///  current SDK feature band. File-based installs are read from the workload metadata directory;
    ///  MSI-based installs (Windows) are read from the machine registry.
    /// </summary>
    /// <param name="dotnetDir">
    ///  The dotnet root to inspect. Defaults to the directory of the running process.
    /// </param>
    public static bool HasInstalledWorkloadsForCurrentBand(string? dotnetDir = null)
    {
        dotnetDir = string.IsNullOrWhiteSpace(dotnetDir) ? Path.GetDirectoryName(Environment.ProcessPath) : dotnetDir;
        var sdkFeatureBand = new SdkFeatureBand(Product.Version);

        return WorkloadInstallType.GetWorkloadInstallType(sdkFeatureBand, dotnetDir) switch
        {
            InstallType.Msi => HasMsiWorkloadRecords(sdkFeatureBand),
            _ => HasFileBasedWorkloadRecords(dotnetDir, sdkFeatureBand),
        };
    }

    private static bool HasFileBasedWorkloadRecords(string? dotnetDir, SdkFeatureBand sdkFeatureBand)
    {
        // Records live under {workloadRoot}/metadata/workloads, where workloadRoot is the user profile
        // for user-local installs and the dotnet root otherwise. This mirrors the layout used by
        // FileBasedInstaller / FileBasedInstallationRecordRepository.
        var workloadRootDir = IsUserLocal(dotnetDir, sdkFeatureBand)
            ? CliFolderPathCalculator.DotnetUserProfileFolderPath
            : dotnetDir;
        if (workloadRootDir is null)
        {
            return false;
        }

        var metadataDir = Path.Combine(workloadRootDir, "metadata", "workloads");
        return new FileBasedInstallationRecordRepository(metadataDir)
            .GetInstalledWorkloads(sdkFeatureBand)
            .Any();
    }

    // Equivalent to WorkloadFileBasedInstall.IsUserLocal, inlined here to avoid pulling that type's
    // workload-history (System.Text.Json) helpers into the NativeAOT build.
    private static bool IsUserLocal(string? dotnetDir, SdkFeatureBand sdkFeatureBand)
        => dotnetDir is not null
           && File.Exists(Path.Combine(dotnetDir, "metadata", "workloads", sdkFeatureBand.ToString(), "userlocal"));

    private static bool HasMsiWorkloadRecords(SdkFeatureBand sdkFeatureBand)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

#if CLI_AOT
        return new RegistryWorkloadInstallationRecordRepository()
            .GetInstalledWorkloads(sdkFeatureBand)
            .Any();
#else
        // This detector is only exercised on the NativeAOT first-run path; the managed build uses
        // WorkloadIntegrityChecker instead. The read-only registry repository constructor used above
        // only exists under CLI_AOT, so there is nothing to do here in the managed build.
        return false;
#endif
    }
}
