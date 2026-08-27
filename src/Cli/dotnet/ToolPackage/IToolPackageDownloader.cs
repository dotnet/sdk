// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal interface IToolPackageDownloader
{
    Task<IToolPackage> InstallPackageAsync(PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange? versionRange = null,
        string? targetFramework = null,
        bool isGlobalTool = false,
        bool isGlobalToolRollForward = false,
        bool verifySignatures = true,
        RestoreActionConfig? restoreActionConfig = null,
        CancellationToken cancellationToken = default
    );

    Task<(NuGetVersion version, PackageSource source)> GetNuGetVersionAsync(
        PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange? versionRange = null,
        RestoreActionConfig? restoreActionConfig = null,
        CancellationToken cancellationToken = default
    );

    Task<IToolPackage?> TryGetDownloadedToolAsync(
        PackageId packageId,
        NuGetVersion packageVersion,
        string? targetFramework,
        VerbosityOptions verbosity,
        CancellationToken cancellationToken = default);
}

internal static class ToolPackageDownloaderExtensions
{
    public static IToolPackage InstallPackage(
        this IToolPackageDownloader downloader,
        PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange? versionRange = null,
        string? targetFramework = null,
        bool isGlobalTool = false,
        bool isGlobalToolRollForward = false,
        bool verifySignatures = true,
        RestoreActionConfig? restoreActionConfig = null,
        CancellationToken cancellationToken = default)
    {
        return downloader.InstallPackageAsync(
            packageLocation,
            packageId,
            verbosity,
            versionRange,
            targetFramework,
            isGlobalTool,
            isGlobalToolRollForward,
            verifySignatures,
            restoreActionConfig,
            cancellationToken).GetAwaiter().GetResult();
    }

    public static (NuGetVersion version, PackageSource source) GetNuGetVersion(
        this IToolPackageDownloader downloader,
        PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange? versionRange = null,
        RestoreActionConfig? restoreActionConfig = null,
        CancellationToken cancellationToken = default)
    {
        return downloader.GetNuGetVersionAsync(
            packageLocation,
            packageId,
            verbosity,
            versionRange,
            restoreActionConfig,
            cancellationToken).GetAwaiter().GetResult();
    }
}
