// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;

namespace Microsoft.DotNet.Cli.Commands.Workload;

/// <summary>
/// Base class for workload related commands.
/// </summary>
internal abstract class WorkloadCommandBase<TDefinition> : CommandBase<TDefinition>
    where TDefinition : WorkloadCommandDefinitionBase
{
    protected static WorkloadId ParseWorkloadId(string workloadId)
    {
        try
        {
            return new WorkloadId(workloadId);
        }
        catch (ArgumentException)
        {
            throw new GracefulException(
                string.Format(CliCommandStrings.WorkloadNotRecognized, workloadId),
                isUserError: false);
        }
    }

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
    /// <remarks>
    /// Controls both NuGet package signature verification (passed to
    /// <see cref="NuGetPackageDownloader.NuGetPackageDownloader.CreateForWorkloads"/>) and
    /// MSI Authenticode verification (passed to
    /// <see cref="Install.WorkloadInstallerFactory.GetWorkloadInstaller"/>).
    /// Set by <see cref="WorkloadUtilities.ShouldVerifySignatures(bool)"/>.
    /// </remarks>
    protected bool VerifySignatures
    {
        get;
    }

    protected bool IsPackageDownloaderProvided { get; }

    /// <summary>
    /// The temporary directory this command created for <see cref="TempDirectoryPath"/>, or <see langword="null"/> when the
    /// directory was supplied by the caller or by the <c>--temp-dir</c> option. Only a directory the command created is
    /// deleted when it completes.
    /// </summary>
    private readonly TemporaryDirectory? _ownedTempDirectory;

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

        if (!string.IsNullOrWhiteSpace(tempDirPath))
        {
            TempDirectoryPath = tempDirPath;
        }
        else if (Definition.TempDirOption != null && !string.IsNullOrWhiteSpace(parseResult.GetValue(Definition.TempDirOption)))
        {
            TempDirectoryPath = parseResult.GetValue(Definition.TempDirOption)!;
        }
        else
        {
            _ownedTempDirectory = new TemporaryDirectory();
            TempDirectoryPath = _ownedTempDirectory.DirectoryPath;
        }

        TempPackagesDirectory = new DirectoryPath(Path.Combine(TempDirectoryPath, "dotnet-sdk-advertising-temp"));

        if (nugetPackageDownloader is not null)
        {
            IsPackageDownloaderProvided = true;
            PackageDownloader = nugetPackageDownloader;
        }
        else
        {
            IsPackageDownloaderProvided = false;
            PackageDownloader = NuGetPackageDownloader.NuGetPackageDownloader.CreateForWorkloads(
                TempPackagesDirectory,
                VerifySignatures,
                nugetLogger,
                Reporter,
                RestoreActionConfiguration);
        }
    }

    /// <summary>
    /// Runs the command, then deletes the temporary directory if the command created it, so downloaded packages and
    /// extracted manifests don't accumulate in the user's temp folder.
    /// </summary>
    public sealed override int Execute()
    {
        try
        {
            return ExecuteCore();
        }
        finally
        {
            _ownedTempDirectory?.Dispose();
        }
    }

    /// <summary>
    /// Runs the command. When the command created <see cref="TempDirectoryPath"/>, it is deleted once this call returns,
    /// so nothing written there may be relied on afterwards.
    /// </summary>
    protected abstract int ExecuteCore();
}
