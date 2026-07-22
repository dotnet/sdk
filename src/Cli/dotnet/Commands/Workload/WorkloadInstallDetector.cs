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
        if (dotnetDir is null)
        {
            return false;
        }

        return FileBasedWorkloadInstallationRecordRepositoryFactory
            .Create(dotnetDir, sdkFeatureBand, CliFolderPathCalculator.DotnetUserProfileFolderPath)
            .GetInstalledWorkloads(sdkFeatureBand)
            .Any();
    }

    private static bool HasMsiWorkloadRecords(SdkFeatureBand sdkFeatureBand)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return new ReadOnlyWindowsWorkloadInstallationRecordRepository()
            .GetInstalledWorkloads(sdkFeatureBand)
            .Any();
    }
}

/// <summary>
///  Builds a <see cref="FileBasedInstallationRecordRepository"/> for a given dotnet root without
///  needing the full workload installer.
///
///  <para>
///  Shared by <see cref="WorkloadInstallDetector"/> above and the CLI_AOT construction path of
///  <see cref="WorkloadInfoHelper"/> (both already part of the NativeAOT closure via
///  <c>AotSourceFiles.props</c>, so this factory lives in the same already-linked file rather than a
///  new one), and reused by the managed-only lightweight background advertising-manifest-update
///  construction path in <see cref="Install.WorkloadManifestUpdater"/>, so the file-based-vs-user-local
///  layout logic is defined in exactly one place.
///  </para>
/// </summary>
internal static class FileBasedWorkloadInstallationRecordRepositoryFactory
{
    /// <summary>
    ///  Equivalent to <see cref="WorkloadFileBasedInstall.IsUserLocal(string, string)"/>, inlined here
    ///  (like the call sites that used to duplicate this check) to avoid pulling in that type's
    ///  workload-history (System.Text.Json) helpers, which are not needed for read-only record lookup.
    /// </summary>
    public static bool IsUserLocal(string dotnetDir, SdkFeatureBand sdkFeatureBand)
        => dotnetDir is not null && File.Exists(Path.Combine(dotnetDir, "metadata", "workloads", sdkFeatureBand.ToString(), "userlocal"));

    /// <summary>
    ///  Constructs the file-based installation record repository for <paramref name="sdkFeatureBand"/>,
    ///  choosing between the user-profile and dotnet-root metadata locations the same way
    ///  <see cref="Install.FileBasedInstaller"/> does.
    /// </summary>
    public static FileBasedInstallationRecordRepository Create(string dotnetDir, SdkFeatureBand sdkFeatureBand, string userProfileDir)
    {
        var workloadRootDir = IsUserLocal(dotnetDir, sdkFeatureBand) ? userProfileDir : dotnetDir;
        return new FileBasedInstallationRecordRepository(Path.Combine(workloadRootDir, "metadata", "workloads"));
    }
}
