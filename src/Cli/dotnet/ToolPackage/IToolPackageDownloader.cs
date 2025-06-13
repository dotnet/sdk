// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal interface IToolPackageDownloader
{
    IToolPackage InstallPackage(PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange versionRange = null,
        string targetFramework = null,
        bool isGlobalTool = false,
        bool isGlobalToolRollForward = false,
        bool verifySignatures = true,
        RestoreActionConfig restoreActionConfig = null
    );

    (NuGetVersion version, PackageSource source) GetNuGetVersion(
        PackageLocation packageLocation,
        PackageId packageId,
        VerbosityOptions verbosity,
        VersionRange versionRange = null,
        RestoreActionConfig restoreActionConfig = null
    );

    bool TryGetDownloadedTool(
        PackageId packageId,
        NuGetVersion packageVersion,
        string targetFramework,
        out IToolPackage toolPackage);
}
