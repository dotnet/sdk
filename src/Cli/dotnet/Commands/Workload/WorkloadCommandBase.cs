// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;

namespace Microsoft.DotNet.Cli.Commands.Workload;

/// <summary>
/// Base class for workload related commands.
/// </summary>
internal abstract class WorkloadCommandBase<TDefinition> : CommandBase<TDefinition>
    where TDefinition : WorkloadCommandDefinitionBase
{
    /// <summary>
    /// The package downloader to use for acquiring NuGet packages.
    /// </summary>
    protected INuGetPackageDownloader PackageDownloader
    {
        get;
    }

    /// <summary>
    /// Provides basic output primitives for the command.
    /// </summary>
    protected IReporter Reporter
    {
        get;
    }

    /// <summary>
    /// Configuration options used by NuGet when performing a restore.
    /// </summary>
    protected RestoreActionConfig RestoreActionConfiguration
    {
        get;
    }

    /// <summary>
    /// Temporary directory used for downloading NuGet packages.
    /// </summary>
    protected DirectoryPath TempPackagesDirectory
    {
        get;
    }

    /// <summary>
    /// The path of the temporary temporary directory to use.
    /// </summary>
    protected string TempDirectoryPath
    {
        get;
    }

    /// <summary>
    /// The verbosity level to use when reporting output from the command.
    /// </summary>
    protected VerbosityOptions Verbosity
    {
        get;
    }

    /// <summary>
    /// Gets whether signatures for workload packages and installers should be verified.
    /// </summary>
    protected bool VerifySignatures
    {
        get;
    }

    protected bool IsPackageDownloaderProvided { get; }

    /// <summary>
    /// Initializes a new <see cref="WorkloadCommandBase"/> instance.
    /// </summary>
    /// <param name="parseResult">The results of parsing the command line.</param>
    /// <param name="verbosityOptions">The command line option used to define the verbosity level.</param>
    /// <param name="reporter">The reporter to use for output.</param>
    /// <param name="tempDirPath">The directory to use for volatile output. If no value is specified, the commandline
    /// option is used if present, otherwise the default temp directory used.</param>
    /// <param name="nugetPackageDownloader">The package downloader to use for acquiring NuGet packages.</param>
    public WorkloadCommandBase(
        ParseResult parseResult,
        IReporter? reporter = null,
        string? tempDirPath = null,
        INuGetPackageDownloader? nugetPackageDownloader = null) : base(parseResult)
    {
        var skipSignCheck = Definition.SkipSignCheckOption != null && parseResult.GetValue(Definition.SkipSignCheckOption);

        VerifySignatures = WorkloadUtilities.ShouldVerifySignatures(skipSignCheck);

        RestoreActionConfiguration = Definition.RestoreOptions?.ToRestoreActionConfig(_parseResult) ?? new RestoreActionConfig();

        Verbosity = Definition.VerbosityOption != null ? parseResult.GetValue(Definition.VerbosityOption) : VerbosityOptions.normal;

        ILogger nugetLogger = Verbosity.IsDetailedOrDiagnostic() ? new NuGetConsoleLogger() : new NullLogger();

        Reporter = reporter ?? Utils.Reporter.Output;

        TempDirectoryPath = !string.IsNullOrWhiteSpace(tempDirPath)
            ? tempDirPath
            : Definition.TempDirOption != null && !string.IsNullOrWhiteSpace(parseResult.GetValue(Definition.TempDirOption))
                ? parseResult.GetValue(Definition.TempDirOption)!
                : TemporaryDirectory.CreateSubdirectory();

        TempPackagesDirectory = new DirectoryPath(Path.Combine(TempDirectoryPath, "dotnet-sdk-advertising-temp"));

        if (nugetPackageDownloader is not null)
        {
            IsPackageDownloaderProvided = true;
            PackageDownloader = nugetPackageDownloader;
        }
        else
        {
            IsPackageDownloaderProvided = false;
            PackageDownloader = new NuGetPackageDownloader.NuGetPackageDownloader(
                TempPackagesDirectory,
                filePermissionSetter: null,
                new FirstPartyNuGetPackageSigningVerifier(),
                nugetLogger,
                Reporter,
                restoreActionConfig: RestoreActionConfiguration,
                verifySignatures: VerifySignatures,
                shouldUsePackageSourceMapping: true);
        }
    }
}
