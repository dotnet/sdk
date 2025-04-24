// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System.Reflection;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;
using NuGet.Client;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal class ToolPackageDownloader : ToolPackageDownloaderBase
{
    public ToolPackageDownloader(
        IToolPackageStore store,
        string runtimeJsonPathForTests = null,
        string currentWorkingDirectory = null
    ) : base(store, runtimeJsonPathForTests, currentWorkingDirectory)
    {
    }

    protected override INuGetPackageDownloader CreateNuGetPackageDownloader(
        bool verifySignatures,
        VerbosityOptions verbosity,
        RestoreActionConfig restoreActionConfig)
    {
        ILogger verboseLogger = new NullLogger();
        if (verbosity.IsDetailedOrDiagnostic())
        {
            verboseLogger = new NuGetConsoleLogger();
        }
        
        return new NuGetPackageDownloader.NuGetPackageDownloader(
            new DirectoryPath(),
            verboseLogger: verboseLogger,
            verifySignatures: verifySignatures,
            shouldUsePackageSourceMapping: true,
            restoreActionConfig: restoreActionConfig,
            verbosityOptions: verbosity,
            currentWorkingDirectory: _currentWorkingDirectory);
    }

    protected override async Task<NuGetVersion> DownloadAndExtractPackage(
        PackageId packageId,
        INuGetPackageDownloader nugetPackageDownloader,
        string packagesRootPath,
        NuGetVersion packageVersion,
        PackageSourceLocation packageSourceLocation,
        bool includeUnlisted = false
        )
    {
        var versionFolderPathResolver = new VersionFolderPathResolver(packagesRootPath);

        string folderToDeleteOnFailure = null;

        try
        {
            var packagePath = await nugetPackageDownloader.DownloadPackageAsync(packageId, packageVersion, packageSourceLocation,
                        includeUnlisted: includeUnlisted, downloadFolder: new DirectoryPath(packagesRootPath)).ConfigureAwait(false);

            folderToDeleteOnFailure = Path.GetDirectoryName(packagePath);

            // look for package on disk and read the version
            NuGetVersion version;

            using (FileStream packageStream = File.OpenRead(packagePath))
            {
                PackageArchiveReader reader = new(packageStream);
                version = new NuspecReader(reader.GetNuspec()).GetVersion();

                var packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(reader.GetNuspec()));
                var hashPath = versionFolderPathResolver.GetHashPath(packageId.ToString(), version);

                Directory.CreateDirectory(Path.GetDirectoryName(hashPath));
                File.WriteAllText(hashPath, packageHash);
            }

            // Extract the package
            var nupkgDir = versionFolderPathResolver.GetInstallPath(packageId.ToString(), version);
            await nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(nupkgDir));

            return version;
        }
        catch
        {
            //  If something fails, don't leave a folder with partial contents (such as a .nupkg but no hash or extracted contents)
            if (folderToDeleteOnFailure != null && Directory.Exists(folderToDeleteOnFailure))
            {
                Directory.Delete(folderToDeleteOnFailure, true);
            }

            throw;
        }
    }
}
