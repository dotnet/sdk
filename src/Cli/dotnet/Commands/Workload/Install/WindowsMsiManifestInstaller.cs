// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.Installer.Windows;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32.Msi;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

/// <summary>
///  The <see cref="IWorkloadManifestInstaller"/> behavior for MSI-based workload installs: computing
///  the architecture-qualified NuGet package ID that carries a manifest/workload-set MSI, and
///  extracting it via an administrative MSI install (<c>ACTION=ADMIN</c>), which unpacks the MSI's
///  file layout to the target path without installing/registering anything on the machine.
///
///  <para>
///  Extracted out of <see cref="NetSdkMsiInstallerClient"/> so this narrow behavior can be shared by
///  the full MSI installer and by lightweight consumers - such as the background advertising-manifest
///  updater - that only need to resolve/extract manifest packages and must not pull in the rest of
///  <see cref="NetSdkMsiInstallerClient"/> (pack installation, elevation-driven IPC, garbage collection,
///  etc.). Notably, extraction here never calls <c>Elevate()</c>/the IPC dispatcher: the admin install
///  below runs in-process using whatever privileges the current process already has.
///  </para>
/// </summary>
[SupportedOSPlatform("windows")]
internal class WindowsMsiManifestInstaller(
    INuGetPackageDownloader nugetPackageDownloader,
    ISetupLogger? log = null,
    Action<uint, string>? logError = null) : IWorkloadManifestInstaller
{
    private static readonly object s_msiAdminInstallLock = new();

    /// <summary>
    ///  Builds a <see cref="WindowsMsiManifestInstaller"/> (and a matching read-only-oriented
    ///  <see cref="IWorkloadInstallationRecordRepository"/>) for lightweight consumers - such as the
    ///  background advertising-manifest updater - that need MSI-based manifest package resolution and
    ///  extraction without the full <see cref="NetSdkMsiInstallerClient"/> (pack installation, elevated
    ///  garbage collection, etc.).
    ///
    ///  <para>
    ///  This path constructs no logger, package cache, elevation context, or IPC client. It reads the
    ///  MSI payload description directly from the downloaded package and uses the in-process
    ///  administrative extraction path when necessary.
    ///  </para>
    /// </summary>
    public static WindowsMsiManifestInstaller CreateForAdvertisingManifestUpdates(
        INuGetPackageDownloader nugetPackageDownloader,
        out IWorkloadInstallationRecordRepository workloadRecordRepo)
    {
        workloadRecordRepo = new ReadOnlyWindowsWorkloadInstallationRecordRepository();
        return new WindowsMsiManifestInstaller(nugetPackageDownloader);
    }

    /// <summary>
    ///  <see langword="true"/> if the most recent <see cref="ExtractManifestAsync(string, string)"/>
    ///  call observed an MSI reboot-required/initiated result while configuring install logging. Only
    ///  updated when no <paramref name="logError"/> callback was supplied (i.e. for standalone callers
    ///  with no restart state of their own to update); callers that pass their own <c>LogError</c>
    ///  method (like <see cref="NetSdkMsiInstallerClient"/>) update their own restart state instead, to
    ///  preserve the previous in-class behavior exactly.
    /// </summary>
    public bool RestartRequired { get; private set; }

    public PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand)
    {
        if (manifestId.ToString().Equals(WorkloadManifestUpdater.WorkloadSetManifestId, StringComparison.OrdinalIgnoreCase))
        {
            return new PackageId($"{manifestId}.{featureBand}.Msi.{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}");
        }
        else
        {
            return new PackageId($"{manifestId}.Manifest-{featureBand}.Msi.{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}");
        }
    }

    public async Task ExtractManifestAsync(string nupkgPath, string targetPath)
    {
        log?.LogMessage($"ExtractManifestAsync: Extracting '{nupkgPath}' to '{targetPath}'");
        string extractionPath = TemporaryDirectory.CreateSubdirectory();

        try
        {
            log?.LogMessage($"ExtractManifestAsync: Temporary extraction path: '{extractionPath}'");
            await nugetPackageDownloader.ExtractPackageAsync(nupkgPath, new DirectoryPath(extractionPath));
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }

            string extractedManifestPath = Path.Combine(extractionPath, "data", "extractedManifest");
            if (Directory.Exists(extractedManifestPath))
            {
                log?.LogMessage($"ExtractManifestAsync: Copying manifest from '{extractionPath}' to '{targetPath}'");
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(extractedManifestPath, targetPath));
            }
            else
            {
                string packageDataPath = Path.Combine(extractionPath, "data");
                if (!MsiPackageData.TryGetMsiPath(packageDataPath, out string? msiPath, out _, message => log?.LogMessage(message)))
                {
                    throw new FileNotFoundException(string.Format(CliCommandStrings.ManifestMsiNotFoundInNuGetPackage, extractionPath));
                }
                string resolvedMsiPath = msiPath!;
                string msiExtractionPath = Path.Combine(extractionPath, "msi");

                lock (s_msiAdminInstallLock)
                {
                    string adminInstallLog = GetMsiLogNameForAdminInstall(resolvedMsiPath);

                    log?.LogMessage($"ExtractManifestAsync: Running admin install for '{msiExtractionPath}'.  Log file: '{adminInstallLog}'");

                    ConfigureInstall(adminInstallLog);

                    var result = WindowsInstaller.InstallProduct(resolvedMsiPath, $"TARGETDIR={msiExtractionPath} ACTION=ADMIN");

                    if (result != Error.SUCCESS)
                    {
                        log?.LogMessage($"ExtractManifestAsync: Admin install failed: {result}");
                        throw new GracefulException(string.Format(CliCommandStrings.FailedToExtractMsi, resolvedMsiPath));
                    }
                }

                var manifestsFolder = Path.Combine(msiExtractionPath, "dotnet", "sdk-manifests");

                string? manifestFolder = null;
                string? manifestsFeatureBandFolder = Directory.GetDirectories(manifestsFolder).SingleOrDefault();
                if (manifestsFeatureBandFolder != null)
                {
                    manifestFolder = Directory.GetDirectories(manifestsFeatureBandFolder).SingleOrDefault();
                }

                if (manifestFolder == null)
                {
                    throw new GracefulException(string.Format(CliCommandStrings.ExpectedSingleManifest, nupkgPath));
                }

                FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(manifestFolder, targetPath));
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(extractionPath) && Directory.Exists(extractionPath))
            {
                Directory.Delete(extractionPath, true);
            }
        }
    }

    /// <summary>
    ///  Equivalent to <see cref="MsiInstallerBase.GetMsiLogNameForAdminInstall(string)"/>.
    /// </summary>
    private string GetMsiLogNameForAdminInstall(string msiPath)
    {
        if (string.IsNullOrWhiteSpace(log?.LogPath))
        {
            return Path.Combine(
                Path.GetTempPath(),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Microsoft.NET.Workload_{Environment.ProcessId}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Path.GetFileNameWithoutExtension(msiPath)}_AdminInstall.log"));
        }

        return Path.Combine(Path.GetDirectoryName(log!.LogPath)!,
            Path.GetFileNameWithoutExtension(log.LogPath) + $"_{Path.GetFileNameWithoutExtension(msiPath)}_AdminInstall.log");
    }

    /// <summary>
    ///  Equivalent to <see cref="MsiInstallerBase.ConfigureInstall(string)"/>.
    /// </summary>
    private void ConfigureInstall(string logFile)
    {
        string validatedLogFile = WindowsUtils.ValidateLogFilePath(logFile);

        // Turn off the MSI UI.
        _ = WindowsInstaller.SetInternalUI(InstallUILevel.None);

        // The log file must be created before calling MsiEnableLog and we should avoid having active handles
        // against it.
        FileStream logFileStream = File.Create(validatedLogFile);
        logFileStream.Close();
        uint error = WindowsInstaller.EnableLog(InstallLogMode.DEFAULT | InstallLogMode.VERBOSE, validatedLogFile, InstallLogAttributes.NONE);

        // We can report issues with the log file creation, but shouldn't fail the workload operation.
        // Prefer the caller-supplied LogError (e.g. NetSdkMsiInstallerClient's, which updates its own
        // Restart property) so behavior is identical to before extraction; fall back to the local
        // bookkeeping below for standalone callers that have no restart state of their own.
        if (logError is not null)
        {
            logError(error, $"Failed to configure log file: {validatedLogFile}");
        }
        else
        {
            LogError(error, $"Failed to configure log file: {validatedLogFile}");
        }
    }

    /// <summary>
    ///  Equivalent to <see cref="MsiInstallerBase.LogError(uint, string)"/>, tracking restart state
    ///  locally in <see cref="RestartRequired"/> instead of a shared installer-wide property. Only used
    ///  when no <paramref name="logError"/> callback was supplied to the constructor.
    /// </summary>
    private void LogError(uint error, string message)
    {
        if (!Error.Success(error))
        {
            log?.LogMessage($"{message} Error: 0x{error:x8}.");
        }

        RestartRequired |= error == Error.SUCCESS_REBOOT_INITIATED || error == Error.SUCCESS_REBOOT_REQUIRED;
    }
}
