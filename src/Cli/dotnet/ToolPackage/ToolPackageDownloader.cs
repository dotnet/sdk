// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        var expectedIdentity = new PackageIdentity(packageId.ToString(), packageVersion);
        string targetNupkgPath = versionFolderPathResolver.GetPackageFilePath(expectedIdentity.Id, expectedIdentity.Version);
        string hashPath = versionFolderPathResolver.GetHashPath(expectedIdentity.Id, expectedIdentity.Version);
        string metadataPath = versionFolderPathResolver.GetNupkgMetadataPath(expectedIdentity.Id, expectedIdentity.Version);
        string packageDirectory = versionFolderPathResolver.GetInstallPath(expectedIdentity.Id, expectedIdentity.Version);

        ConcurrencyUtilities.ExecuteWithFileLocked(targetNupkgPath, () =>
        {
            if (File.Exists(metadataPath))
            {
                return;
            }

            if (File.Exists(hashPath))
            {
                string packageHash = File.ReadAllText(hashPath);
                using FileStream packageStream = File.OpenRead(targetNupkgPath);
                using PackageArchiveReader reader = new(packageStream);
                string contentHash = reader.GetContentHash(CancellationToken.None, () => packageHash);
                PublishPackageMetadata(metadataPath, contentHash);
                return;
            }

            try
            {
                var _downloadActivity = Activities.Source.StartActivity("download-tool");
                _downloadActivity?.DisplayName = $"Downloading tool {packageId}@{packageVersion}";
                string packagePath = nugetPackageDownloader.DownloadPackageAsync(
                    packageId,
                    packageVersion,
                    packageSourceLocation,
                    includeUnlisted: includeUnlisted,
                    downloadFolder: new DirectoryPath(packagesRootPath)).ConfigureAwait(false).GetAwaiter().GetResult();
                _downloadActivity?.Stop();

                string packageHash;
                string contentHash;
                using (FileStream packageStream = File.OpenRead(packagePath))
                {
                    packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(packageStream));
                    packageStream.Position = 0;
                    using PackageArchiveReader reader = new(packageStream);
                    PackageIdentity actualIdentity = reader.GetIdentity();
                    if (!PackageIdentityComparer.Default.Equals(expectedIdentity, actualIdentity))
                    {
                        throw new PackagingException(
                            $"The downloaded package identity '{actualIdentity}' does not match the requested package identity '{expectedIdentity}'.");
                    }

                    contentHash = reader.GetContentHash(CancellationToken.None, () => packageHash);
                }

                if (verbosity.IsDetailedOrDiagnostic())
                {
                    Reporter.Output.WriteLine($"Extracting package {packageId}@{packageVersion} to {packagePath}");
                }

                var _extractActivity = Activities.Source.StartActivity("extract-tool");
                nugetPackageDownloader.ExtractPackageAsync(packagePath, new DirectoryPath(packageDirectory)).ConfigureAwait(false).GetAwaiter().GetResult();
                _extractActivity?.Stop();

                // NuGet readers treat these files as completion markers, so publish them only after extraction succeeds.
                PublishPackageHash(hashPath, packageHash);
                PublishPackageMetadata(metadataPath, contentHash);
            }
            catch
            {
                // A completion marker can expose the package to other installs. Preserve it so NuGet can repair missing metadata.
                if (!File.Exists(hashPath) && !File.Exists(metadataPath) && Directory.Exists(packageDirectory))
                {
                    Directory.Delete(packageDirectory, true);
                }

                throw;
            }
        });

        return packageVersion;
    }

    /// <summary>
    /// Makes the package completion marker visible only after its complete contents have been written to a closed file.
    /// </summary>
    /// <remarks>The temporary file must be a sibling so the final move is atomic on the package file system.</remarks>
    private void PublishPackageHash(string hashPath, string packageHash)
    {
        string hashDirectory = Path.GetDirectoryName(hashPath)!;
        Directory.CreateDirectory(hashDirectory);
        string temporaryHashPath = Path.Combine(hashDirectory, Path.GetRandomFileName());

        try
        {
            File.WriteAllText(temporaryHashPath, packageHash);
            MovePackageHash(temporaryHashPath, hashPath);
        }
        finally
        {
            File.Delete(temporaryHashPath);
        }
    }

    /// <summary>
    /// Defines the atomic publication point so tests can pause while only the completed temporary marker is visible.
    /// </summary>
    /// <remarks>The source and destination are sibling files; overrides must preserve that same-file-system move.</remarks>
    protected virtual void MovePackageHash(string temporaryHashPath, string hashPath)
    {
        File.Move(temporaryHashPath, hashPath, overwrite: true);
    }

    /// <summary>
    /// Publishes NuGet's canonical completion marker so other NuGet writers preserve the completed package.
    /// </summary>
    /// <remarks>The metadata is written to a sibling temporary file and moved last while the NuGet package lock is held.</remarks>
    private void PublishPackageMetadata(string metadataPath, string contentHash)
    {
        string metadataDirectory = Path.GetDirectoryName(metadataPath)!;
        Directory.CreateDirectory(metadataDirectory);
        string temporaryMetadataPath = Path.Combine(metadataDirectory, Path.GetRandomFileName());

        try
        {
            NupkgMetadataFileFormat.Write(
                temporaryMetadataPath,
                new NupkgMetadataFile
                {
                    ContentHash = contentHash
                });
            MovePackageMetadata(temporaryMetadataPath, metadataPath);
        }
        finally
        {
            File.Delete(temporaryMetadataPath);
        }
    }

    protected virtual void MovePackageMetadata(string temporaryMetadataPath, string metadataPath)
    {
        File.Move(temporaryMetadataPath, metadataPath, overwrite: true);
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
        // We verify the package has been installed correctly prior to calling this method.
        var package = localRepository.FindPackage(packageId.ToString(), version)!;

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

        //  Use legacy lock file format (v3) because this synthetic assets file uses framework-based target keys,
        //  not the alias-based keys required by the v4 format (https://github.com/NuGet/NuGet.Client/pull/6972).
        var lockFile = new LockFile() { Version = 3 };
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
