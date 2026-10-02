// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader;

internal interface INuGetPackageDownloader
{
    Task<string> DownloadPackageAsync(PackageId packageId,
        CancellationToken cancellationToken,
        NuGetVersion packageVersion = null,
        PackageSourceLocation packageSourceLocation = null,
        bool includePreview = false,
        bool? includeUnlisted = null,
        DirectoryPath? downloadFolder = null,
        PackageSourceMapping packageSourceMapping = null);

    Task<string> GetPackageUrl(PackageId packageId,
        CancellationToken cancellationToken,
        NuGetVersion packageVersion = null,
        PackageSourceLocation packageSourceLocation = null,
        bool includePreview = false);

    Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder, CancellationToken cancellationToken);

    Task<NuGetVersion> GetLatestPackageVersion(PackageId packageId,
         CancellationToken cancellationToken,
         PackageSourceLocation packageSourceLocation = null,
         bool includePreview = false);

    Task<IEnumerable<NuGetVersion>> GetLatestPackageVersions(PackageId packageId,
         int numberOfResults,
         CancellationToken cancellationToken,
         PackageSourceLocation packageSourceLocation = null,
         bool includePreview = false);

    Task<NuGetVersion> GetBestPackageVersionAsync(PackageId packageId,
        VersionRange versionRange,
        CancellationToken cancellationToken,
         PackageSourceLocation packageSourceLocation = null);

    Task<(NuGetVersion version, PackageSource source)> GetBestPackageVersionAndSourceAsync(PackageId packageId,
        VersionRange versionRange,
        CancellationToken cancellationToken,
        PackageSourceLocation packageSourceLocation = null);
}
