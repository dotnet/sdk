// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.TemplateEngine.Utils;
using NuGet.Client;
using NuGet.Common;
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
        string? runtimeJsonPathForTests = null,
        string? currentWorkingDirectory = null
    ) : base(store, runtimeJsonPathForTests, currentWorkingDirectory, FileSystemWrapper.Default)
    {
    }

    protected override INuGetPackageDownloader CreateNuGetPackageDownloader(
        bool verifySignatures,
        VerbosityOptions verbosity,
        RestoreActionConfig? restoreActionConfig)
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

    protected override NuGetVersion DownloadAndExtractPackage(
        PackageId packageId,
        INuGetPackageDownloader nugetPackageDownloader,
        string packagesRootPath,
        NuGetVersion packageVersion,
        PackageSourceLocation packageSourceLocation,
        VerbosityOptions verbosity,
        bool includeUnlisted = false
        )
    {
        var versionFolderPathResolver = new VersionFolderPathResolver(packagesRootPath);

        string? folderToDeleteOnFailure = null;
        return TransactionalAction.Run(() =>
        {
            var _downloadActivity = Activities.Source.StartActivity("download-tool");
            _downloadActivity?.DisplayName = $"Downloading tool {packageId}@{packageVersion}";
            var packagePath = nugetPackageDownloader.DownloadPackageAsync(packageId, packageVersion, packageSourceLocation,
                        includeUnlisted: includeUnlisted, downloadFolder: new DirectoryPath(packagesRootPath)).ConfigureAwait(false).GetAwaiter().GetResult();
            _downloadActivity?.Stop();
            folderToDeleteOnFailure = Path.GetDirectoryName(packagePath);

            // look for package on disk and read the version
            NuGetVersion version;

            using (FileStream packageStream = File.OpenRead(packagePath))
            {
                PackageArchiveReader reader = new(packageStream);
                version = new NuspecReader(reader.GetNuspec()).GetVersion();

                var packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(reader.GetNuspec()));
                var hashPath = versionFolderPathResolver.GetHashPath(packageId.ToString(), version);

                Directory.CreateDirectory(Path.GetDirectoryName(hashPath)!);
                File.WriteAllText(hashPath, packageHash);
            }

            if (verbosity.IsDetailedOrDiagnostic())
            {
                Reporter.Output.WriteLine($"Extracting package {packageId}@{packageVersion} to {packagePath}");
            }
            // Extract the package
            var _extractActivity = Activities.Source.StartActivity("extract-tool");
            var nupkgDir = versionFolderPathResolver.GetInstallPath(packageId.ToString(), version);
            nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(nupkgDir)).ConfigureAwait(false).GetAwaiter().GetResult();
            _extractActivity?.Stop();

            return version;
        }, rollback: () =>
        {
            //  If something fails, don't leave a folder with partial contents (such as a .nupkg but no hash or extracted contents)
            if (folderToDeleteOnFailure != null && Directory.Exists(folderToDeleteOnFailure))
            {
                Directory.Delete(folderToDeleteOnFailure, true);
            }
        });
    }

    protected override bool IsPackageInstalled(PackageId packageId, NuGetVersion packageVersion, string packagesRootPath)
    {
        NuGetv3LocalRepository nugetLocalRepository = new(packagesRootPath);

        var package = nugetLocalRepository.FindPackage(packageId.ToString(), packageVersion);
        return package != null;
    }

    protected override ToolConfiguration GetToolConfiguration(PackageId id, DirectoryPath packageDirectory, DirectoryPath assetsJsonParentDirectory)
    {
        return ToolPackageInstance.GetToolConfiguration(id, packageDirectory, assetsJsonParentDirectory, _fileSystem);
    }

    protected override void CreateAssetFile(
        PackageId packageId,
        NuGetVersion version,
        DirectoryPath packagesRootPath,
        string assetFilePath,
        string runtimeJsonGraph,
        VerbosityOptions verbosity,
        string? targetFramework = null
        )
    {
        // To get runtimeGraph:
        var runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(runtimeJsonGraph);

        // Create ManagedCodeConventions:
        var conventions = new ManagedCodeConventions(runtimeGraph);

        //  Create NuGetv3LocalRepository
        NuGetv3LocalRepository localRepository = new(packagesRootPath.Value);
        var package = localRepository.FindPackage(packageId.ToString(), version);

        if (verbosity.IsDetailedOrDiagnostic())
        {
            Reporter.Output.WriteLine($"Locating package {packageId}@{version} in package store {packagesRootPath.Value}");
            Reporter.Output.WriteLine($"The package has {string.Join(',', package.Nuspec.GetPackageTypes().Select(p => $"{p.Name},{p.Version}"))} package types");
        }
        if (!package.Nuspec.GetPackageTypes().Any(pt => pt.Name.Equals(PackageType.DotnetTool.Name, StringComparison.OrdinalIgnoreCase) ||
                                                        pt.Name.Equals("DotnetToolRidPackage", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ToolPackageException(string.Format(CliStrings.ToolPackageNotATool, packageId));
        }

        //  Create LockFileTargetLibrary
        var lockFileLib = new LockFileTargetLibrary()
        {
            Name = packageId.ToString(),
            Version = version,
            Type = LibraryType.Package,
            //  Actual package type might be DotnetToolRidPackage but for asset file processing we treat them the same
            PackageType = [PackageType.DotnetTool]
        };

        var collection = new ContentItemCollection();
        collection.Load(package.Files);

        //  Create criteria
        var managedCriteria = new List<SelectionCriteria>(1);
        // Use major.minor version of currently running version of .NET
        NuGetFramework currentTargetFramework;
        if (targetFramework != null)
        {
            currentTargetFramework = NuGetFramework.Parse(targetFramework);
        }
        else
        {
            currentTargetFramework = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, new Version(Environment.Version.Major, Environment.Version.Minor));
        }

        var standardCriteria = conventions.Criteria.ForFrameworkAndRuntime(
            currentTargetFramework,
            RuntimeInformation.RuntimeIdentifier);
        managedCriteria.Add(standardCriteria);

        //  Create asset file
        //  Note that we know that the package type for the lock file is DotnetTool because we just set it to that.
        //  This if statement is still here because this mirrors code in NuGet for restore so maybe it will be easier to keep in sync if need be
        if (lockFileLib.PackageType.Contains(PackageType.DotnetTool))
        {
            AddToolsAssets(conventions, lockFileLib, collection, managedCriteria);
        }

        var lockFile = new LockFile();
        var lockFileTarget = new LockFileTarget()
        {
            TargetFramework = currentTargetFramework,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier
        };
        lockFileTarget.Libraries.Add(lockFileLib);
        lockFile.Targets.Add(lockFileTarget);
        new LockFileFormat().Write(assetFilePath, lockFile);
    }


    // The following methods are copied from the LockFileUtils class in Nuget.Client
    protected static void AddToolsAssets(
        ManagedCodeConventions managedCodeConventions,
        LockFileTargetLibrary lockFileLib,
        ContentItemCollection contentItems,
        IReadOnlyList<SelectionCriteria> orderedCriteria)
    {
        var toolsGroup = GetLockFileItems(
            orderedCriteria,
            contentItems,
            managedCodeConventions.Patterns.ToolsAssemblies);

        lockFileLib.ToolsAssemblies.AddRange(toolsGroup);
    }

    protected static IEnumerable<LockFileItem> GetLockFileItems(
        IReadOnlyList<SelectionCriteria> criteria,
        ContentItemCollection items,
        params PatternSet[] patterns)
    {
        return GetLockFileItems(criteria, items, additionalAction: null, patterns);
    }

    protected static IEnumerable<LockFileItem> GetLockFileItems(
       IReadOnlyList<SelectionCriteria> criteria,
       ContentItemCollection items,
       Action<LockFileItem>? additionalAction,
       params PatternSet[] patterns)
    {
        // Loop through each criteria taking the first one that matches one or more items.
        foreach (var managedCriteria in criteria)
        {
            var group = items.FindBestItemGroup(
                managedCriteria,
                patterns);

            if (group != null)
            {
                foreach (var item in group.Items)
                {
                    var newItem = new LockFileItem(item.Path);
                    if (item.Properties.TryGetValue("locale", out var locale))
                    {
                        newItem.Properties["locale"] = (string)locale;
                    }

                    if (item.Properties.TryGetValue("related", out var related))
                    {
                        newItem.Properties["related"] = (string)related;
                    }
                    additionalAction?.Invoke(newItem);
                    yield return newItem;
                }
                // Take only the first group that has items
                break;
            }
        }

        yield break;
    }
}
