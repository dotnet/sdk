// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

/// <summary>
///  Shared background advertising-manifest entry points used by both the managed and Native AOT CLI.
///  The managed <see cref="IWorkloadManifestUpdater"/> implementation is in the managed-only partial.
/// </summary>
internal partial class WorkloadManifestUpdater
{
    public static readonly string WorkloadSetManifestId = "Microsoft.NET.Workloads";

    /// <summary>
    ///  Builds the narrow set of dependencies the background advertising-manifest update needs, without
    ///  going through <see cref="WorkloadInstallerFactory"/> (i.e. without constructing the full
    ///  <see cref="FileBasedInstaller"/>/<see cref="NetSdkMsiInstallerClient"/> installer or, on Windows,
    ///  any elevated MSI IPC).
    /// </summary>
    private static WorkloadAdvertisingManifestUpdater GetAdvertisingUpdaterInstance(string userProfileDir)
    {
        var reporter = new NullReporter();
        var dotnetPath = Path.GetDirectoryName(Environment.ProcessPath);
        var sdkVersion = Product.Version;
        var sdkFeatureBand = new SdkFeatureBand(sdkVersion);
        var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, sdkVersion, userProfileDir, SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(Environment.CurrentDirectory));
        var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetPath, sdkVersion, userProfileDir);
        var tempPackagesDir = new DirectoryPath(TemporaryDirectory.CreateSubdirectory());
        // NuGet verification uses ShouldVerifySignatures() (respects registry policy and host
        // signing status, but not --skip-sign-check since this is a background operation).
        // MSI verification is intentionally disabled — this updater only downloads advertising
        // manifests, not installable MSIs.
        var verifySignatures = WorkloadUtilities.ShouldVerifySignatures();
        var nugetPackageDownloader = NuGetPackageDownloader.NuGetPackageDownloader.CreateForWorkloads(
            tempPackagesDir,
            verifySignatures,
            reporter: reporter);

        IWorkloadManifestInstaller manifestInstaller;
        IWorkloadInstallationRecordRepository workloadRecordRepo;

        if (WorkloadInstallType.GetWorkloadInstallType(sdkFeatureBand, dotnetPath) == InstallType.Msi)
        {
#if !TARGET_WINDOWS
            throw new InvalidOperationException(CliCommandStrings.OSDoesNotSupportMsi);
#else
            if (!OperatingSystem.IsWindows())
            {
                throw new InvalidOperationException(CliCommandStrings.OSDoesNotSupportMsi);
            }

            manifestInstaller = WindowsMsiManifestInstaller.CreateForAdvertisingManifestUpdates(nugetPackageDownloader, out workloadRecordRepo);
#endif
        }
        else
        {
            manifestInstaller = new FileBasedManifestInstaller(nugetPackageDownloader, tempPackagesDir);
            workloadRecordRepo = FileBasedWorkloadInstallationRecordRepositoryFactory.Create(dotnetPath, sdkFeatureBand, userProfileDir);
        }

        return new WorkloadAdvertisingManifestUpdater(reporter, workloadResolver, nugetPackageDownloader, userProfileDir, workloadRecordRepo, manifestInstaller, sdkFeatureBand: sdkFeatureBand);
    }

    public static async Task BackgroundUpdateAdvertisingManifestsAsync(string userProfileDir)
    {
        try
        {
            var advertisingUpdater = GetAdvertisingUpdaterInstance(userProfileDir);
            await advertisingUpdater.BackgroundUpdateAdvertisingManifestsWhenRequiredAsync();
        }
        catch (Exception)
        {
            // Never surface messages on background updates
        }
    }

    public static bool ShouldUseWorkloadSetMode(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        => WorkloadAdvertisingManifestUpdater.ShouldUseWorkloadSetMode(sdkFeatureBand, dotnetDir);

    public static void AdvertiseWorkloadUpdates()
    {
        try
        {
            var backgroundUpdatesDisabled = bool.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_UPDATE_NOTIFY_DISABLE), out var disableEnvVar) && disableEnvVar;
            SdkFeatureBand featureBand = new(Product.Version);
            var adUpdatesFile = WorkloadAdvertisingManifestUpdater.GetAdvertisingWorkloadsFilePath(CliFolderPathCalculator.DotnetUserProfileFolderPath, featureBand);
            if (!backgroundUpdatesDisabled && File.Exists(adUpdatesFile))
            {
                var updatableWorkloads = JsonSerializer.Deserialize(File.ReadAllText(adUpdatesFile), WorkloadManifestUpdaterJsonSerializerContext.Default.StringArray);
                if (updatableWorkloads != null && updatableWorkloads.Any())
                {
                    Console.WriteLine();
                    Console.WriteLine(CliCommandStrings.WorkloadInstallWorkloadUpdatesAvailable);
                }
            }
        }
        catch (Exception)
        {
            // Never surface errors
        }
    }

}
