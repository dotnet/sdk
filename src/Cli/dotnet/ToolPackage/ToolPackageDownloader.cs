// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Packaging;
using NuGet.Versioning;


namespace Microsoft.DotNet.Cli.ToolPackage
{
    internal class ToolPackageDownloader
    {
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        protected readonly DirectoryPath _tempPackagesDir;

        public ToolPackageDownloader(
            INuGetPackageDownloader nugetPackageDownloader = null,
            string tempDirPath = null
            )
        {
            _tempPackagesDir = new DirectoryPath(tempDirPath ?? PathUtilities.CreateTempSubdirectory());
            _nugetPackageDownloader = nugetPackageDownloader ??
                                     new NuGetPackageDownloader.NuGetPackageDownloader(_tempPackagesDir);
        }

        public async Task<IToolPackage> InstallPackageAsync(PackageLocation packageLocation, PackageId packageId,
            VersionRange versionRange = null,
            string targetFramework = null,
            string verbosity = null)
        { 
            var tempDirsToDelete = new List<string>();
            var tempFilesToDelete = new List<string>();

            var packagePath = await _nugetPackageDownloader.DownloadPackageAsync(packageId);
            tempFilesToDelete.Add(packagePath);

            // Look fo nuget package on disk and read the version 
            await using FileStream packageStream = File.OpenRead(packagePath);
            PackageArchiveReader reader = new PackageArchiveReader(packageStream);
            var version = new NuspecReader(reader.GetNuspec()).GetVersion();

            var tempExtractionDir = Path.GetDirectoryName(packagePath);
            var tempToolExtractionDir = Path.Combine(tempExtractionDir, packageId.ToString(), version.ToString(), "tools");

            tempDirsToDelete.Add(tempExtractionDir);
            
            var filesInPackage = await _nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(tempToolExtractionDir));
            
            if (Directory.Exists(packagePath))
            {
                throw new ToolPackageException(
                    string.Format(
                        CommonLocalizableStrings.ToolPackageConflictPackageId,
                        packageId,
                        version.ToNormalizedString()));
            }

            return new ToolPackageInstance(id: packageId,
                            version: version,
                            packageDirectory: new DirectoryPath(tempExtractionDir),
                            assetsJsonParentDirectory: new DirectoryPath(tempExtractionDir));
            //cleanup
            /*foreach (var dir in tempDirsToDelete)
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }*/
        
        }
    }
}
