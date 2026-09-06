// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload;

/// <summary>
///  Builds a <see cref="FileBasedInstallationRecordRepository"/> for a given dotnet root without
///  needing the full workload installer.
///
///  <para>
///  Shared by <see cref="WorkloadInstallDetector"/>, the CLI_AOT construction path of
///  <see cref="WorkloadInfoHelper"/>, and the lightweight background advertising-manifest updater,
///  so the file-based-versus-user-local layout logic is defined in exactly one place.
///  </para>
/// </summary>
internal static class FileBasedWorkloadInstallationRecordRepositoryFactory
{
    /// <summary>
    ///  Equivalent to <see cref="WorkloadFileBasedInstall.IsUserLocal(string, string)"/>, inlined here
    ///  to avoid pulling in that type's workload-history helpers, which are not needed for read-only
    ///  record lookup.
    /// </summary>
    public static bool IsUserLocal(string dotnetDir, SdkFeatureBand sdkFeatureBand)
        => File.Exists(Path.Combine(dotnetDir, "metadata", "workloads", sdkFeatureBand.ToString(), "userlocal"));

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
