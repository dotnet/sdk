// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

/// <summary>
///  The <see cref="IWorkloadManifestInstaller"/> behavior for file-based (non-MSI) workload installs:
///  computing the NuGet package ID that carries a manifest/workload-set, and extracting the "data"
///  directory of a downloaded manifest package to the advertising/installed manifest location.
///
///  <para>
///  Extracted out of <see cref="FileBasedInstaller"/> so this narrow, dependency-light behavior can be
///  shared by the full file-based installer and by lightweight consumers - such as the background
///  advertising-manifest updater - that only need to resolve/extract manifest packages and must not
///  pull in the rest of <see cref="FileBasedInstaller"/> (pack installation, transactions, workload
///  records, etc.).
///  </para>
/// </summary>
internal class FileBasedManifestInstaller(INuGetPackageDownloader nugetPackageDownloader, DirectoryPath tempPackagesDir) : IWorkloadManifestInstaller
{
    public PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand)
    {
        if (manifestId.ToString().Equals(WorkloadManifestUpdater.WorkloadSetManifestId, StringComparison.OrdinalIgnoreCase))
        {
            return new PackageId($"{manifestId}.{featureBand}");
        }
        else
        {
            return new PackageId($"{manifestId}.Manifest-{featureBand}");
        }
    }

    public async Task ExtractManifestAsync(string nupkgPath, string targetPath)
    {
        var extractionPath = Path.Combine(tempPackagesDir.Value, "dotnet-sdk-advertising-temp", $"{Path.GetFileName(nupkgPath)}-extracted");
        if (Directory.Exists(extractionPath))
        {
            Directory.Delete(extractionPath, true);
        }

        try
        {
            Directory.CreateDirectory(extractionPath);
            await nugetPackageDownloader.ExtractPackageAsync(nupkgPath, new DirectoryPath(extractionPath));
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            FileAccessRetrier.RetryOnMoveAccessFailure(() => DirectoryPath.MoveDirectory(Path.Combine(extractionPath, "data"), targetPath));
        }
        finally
        {
            if (!string.IsNullOrEmpty(extractionPath) && Directory.Exists(extractionPath))
            {
                Directory.Delete(extractionPath, true);
            }
        }
    }
}
