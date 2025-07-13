// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Configuration;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Configuration.DotnetCli.Services;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal static class WorkloadIntegrityChecker
{
    public static void RunFirstUseCheck(IReporter reporter)
    {
        var creationResult = new WorkloadResolverFactory().Create();
        var sdkFeatureBand = new SdkFeatureBand(creationResult.SdkVersion);
        var verifySignatures = WorkloadCommandBase.ShouldVerifySignatures();
        var tempPackagesDirectory = new DirectoryPath(PathUtilities.CreateTempSubdirectory());
        var packageDownloader = new NuGetPackageDownloader.NuGetPackageDownloader(
            tempPackagesDirectory,
            DotNetConfigurationFactory.Create(),
            verboseLogger: new NullLogger(),
            verifySignatures: verifySignatures);

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
