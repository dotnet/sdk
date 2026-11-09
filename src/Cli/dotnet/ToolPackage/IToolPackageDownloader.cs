// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal interface IToolPackageDownloader
{
    IToolPackage InstallPackage(PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange? versionRange = null,
        string? targetFramework = null,
        bool isGlobalTool = false,
        bool isGlobalToolRollForward = false,
        bool verifySignatures = true,
        RestoreActionConfig? restoreActionConfig = null
    );

    Task<(NuGetVersion version, PackageSource source)> GetNuGetVersionAsync(
        PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange? versionRange = null,
        RestoreActionConfig? restoreActionConfig = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the best complete cached tool that matches the specified version range.</summary>
    bool TryGetBestDownloadedTool(
        PackageId packageId,
        VersionRange versionRange,
        string? targetFramework,
        VerbosityOptions verbosity,
        [NotNullWhen(true)]
        out IToolPackage? toolPackage);

    /// <summary>Gets the complete cached tool for the specified exact version.</summary>
    bool TryGetDownloadedTool(
        PackageId packageId,
        NuGetVersion packageVersion,
        string? targetFramework,
        VerbosityOptions verbosity,
        [NotNullWhen(true)]
        out IToolPackage? toolPackage);
}
