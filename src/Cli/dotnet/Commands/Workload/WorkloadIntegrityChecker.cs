// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal static class WorkloadIntegrityChecker
{
    public static void RunFirstUseCheck(IReporter reporter)
    {
        var creationResult = new WorkloadResolverFactory().Create();
        var sdkFeatureBand = new SdkFeatureBand(creationResult.SdkVersion);
        var verifySignatures = WorkloadUtilities.ShouldVerifySignatures();
        var tempPackagesDirectory = new DirectoryPath(TemporaryDirectory.CreateSubdirectory());
        var packageDownloader = NuGetPackageDownloader.NuGetPackageDownloader.CreateForWorkloads(
            tempPackagesDirectory,
            verifySignatures);

        var installer = WorkloadInstallerFactory.GetWorkloadInstaller(
            reporter,
            sdkFeatureBand,
            creationResult.WorkloadResolver,
            VerbosityOptions.normal,
            creationResult.UserProfileDir,
            verifySignatures,
            packageDownloader,
            creationResult.DotnetPath);
        var repository = installer.GetWorkloadInstallationRecordRepository();
        var installedWorkloads = repository.GetInstalledWorkloads(sdkFeatureBand);

        if (installedWorkloads.Any())
        {
            reporter.WriteLine(CliCommandStrings.WorkloadIntegrityCheck);
            CliTransaction.RunNew(context => installer.InstallWorkloads(installedWorkloads, sdkFeatureBand, context));
            reporter.WriteLine("----------------");
        }
    }
}
