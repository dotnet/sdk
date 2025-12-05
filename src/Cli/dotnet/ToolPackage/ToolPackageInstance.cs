// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

// This is named "ToolPackageInstance" because "ToolPackage" would conflict with the namespace
internal class ToolPackageInstance : IToolPackage
{
    private const string PackagedShimsDirectoryConvention = "shims";

    public IEnumerable<string> Warnings { get; private set; }

    public PackageId Id { get; private set; }

    public NuGetVersion Version { get; private set; }

    public PackageId ResolvedPackageId { get; private set; }

    public NuGetVersion ResolvedPackageVersion { get; private set; }

    public DirectoryPath PackageDirectory { get; private set; }

    public ToolCommand Command { get; private set; }

    public IReadOnlyList<FilePath> PackagedShims { get; private set; }

    public IEnumerable<NuGetFramework> Frameworks { get; private set; }

    private IFileSystem _fileSystem;

    public const string AssetsFileName = "project.assets.json";
    public const string RidSpecificPackageAssetsFileName = "project.assets.ridpackage.json";
    private const string ToolSettingsFileName = "DotnetToolSettings.xml";

    public ToolPackageInstance(PackageId id,
        NuGetVersion version,
        DirectoryPath packageDirectory,
        DirectoryPath assetsJsonParentDirectory,
        IFileSystem fileSystem = null)
    {
        Id = id;
        Version = version ?? throw new ArgumentNullException(nameof(version));
        PackageDirectory = packageDirectory;
        _fileSystem = fileSystem ?? new FileSystemWrapper();

        bool usingRidSpecificPackage = _fileSystem.File.Exists(assetsJsonParentDirectory.WithFile(RidSpecificPackageAssetsFileName).Value);

        string resolvedAssetsFileNameFullPath;
        if (usingRidSpecificPackage)
        {
            resolvedAssetsFileNameFullPath = assetsJsonParentDirectory.WithFile(RidSpecificPackageAssetsFileName).Value;
        }
        else
        {
            resolvedAssetsFileNameFullPath = assetsJsonParentDirectory.WithFile(AssetsFileName).Value;
        }

        LockFile lockFile;

        try
        {
            using (var stream = _fileSystem.File.OpenRead(resolvedAssetsFileNameFullPath))
            {
                lockFile = new LockFileFormat().Read(stream, resolvedAssetsFileNameFullPath);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            throw new ToolPackageException(string.Format(CliStrings.FailedToReadNuGetLockFile, Id, ex.Message), ex);
        }

        var library = FindLibraryInLockFile(lockFile);
        if (library == null)
        {
            throw new ToolPackageException(
                string.Format(CliStrings.FailedToFindLibraryInAssetsFile, Id, resolvedAssetsFileNameFullPath));
        }


        if (usingRidSpecificPackage)
        {
            ResolvedPackageId = new PackageId(library.Name);
            ResolvedPackageVersion = library.Version;
        }
        else
        {
            ResolvedPackageId = Id;
            ResolvedPackageVersion = Version;
        }

        var toolConfiguration = DeserializeToolConfiguration(library, packageDirectory, _fileSystem);
        Warnings = toolConfiguration.Warnings;

        var installPath = new VersionFolderPathResolver(PackageDirectory.Value).GetInstallPath(ResolvedPackageId.ToString(), ResolvedPackageVersion);
        var toolsPackagePath = Path.Combine(installPath, "tools");
        Frameworks = _fileSystem.Directory.EnumerateDirectories(toolsPackagePath)
            .Select(path => NuGetFramework.ParseFolder(Path.GetFileName(path))).ToList();

        LockFileItem entryPointFromLockFile = FindItemInTargetLibrary(library, toolConfiguration.ToolAssemblyEntryPoint);
        if (entryPointFromLockFile == null)
        {
            throw new ToolConfigurationException(
                string.Format(
                    CliStrings.MissingToolEntryPointFile,
                    toolConfiguration.ToolAssemblyEntryPoint,
                    toolConfiguration.CommandName));
        }

        Command = new ToolCommand(
                new ToolCommandName(toolConfiguration.CommandName),
                toolConfiguration.Runner,
                LockFileRelativePathToFullFilePath(entryPointFromLockFile.Path, library));

        IEnumerable<LockFileItem> filesUnderShimsDirectory = library
            ?.ToolsAssemblies
            ?.Where(t => LockFileMatcher.MatchesDirectoryPath(t, PackagedShimsDirectoryConvention));

        if (filesUnderShimsDirectory == null)
        {
            PackagedShims = [];
        }
        else
        {
            IEnumerable<string> allAvailableShimRuntimeIdentifiers = filesUnderShimsDirectory
               .Select(f => f.Path.Split('\\', '/')?[4]) // ex: "tools/netcoreapp2.1/any/shims/osx-x64/demo" osx-x64 is at [4]
               .Where(f => !string.IsNullOrEmpty(f));

            if (new FrameworkDependencyFile().TryGetMostFitRuntimeIdentifier(
                DotnetFiles.VersionFileObject.BuildRid,
                [.. allAvailableShimRuntimeIdentifiers],
                out var mostFitRuntimeIdentifier))
            {
                PackagedShims = library?.ToolsAssemblies?.Where(l => LockFileMatcher.MatchesDirectoryPath(l, $"{PackagedShimsDirectoryConvention}/{mostFitRuntimeIdentifier}"))
                    .Select(l => LockFileRelativePathToFullFilePath(l.Path, library)).ToArray() ?? [];
            }
            else
            {
                PackagedShims = [];
            }
        }
    }

    private FilePath LockFileRelativePathToFullFilePath(string lockFileRelativePath, LockFileTargetLibrary library)
    {
        return PackageDirectory
                    .WithSubDirectories(
                        library.Name,
                        library.Version.ToNormalizedString().ToLowerInvariant())
                    .WithFile(lockFileRelativePath);
    }


    public static ToolConfiguration GetToolConfiguration(PackageId id,
        DirectoryPath packageDirectory,
        DirectoryPath assetsJsonParentDirectory, IFileSystem fileSystem)
    {
        var lockFile = new LockFileFormat().Read(assetsJsonParentDirectory.WithFile(AssetsFileName).Value);
        var lockFileTargetLibrary = FindLibraryInLockFile(lockFile);
        return DeserializeToolConfiguration(lockFileTargetLibrary, packageDirectory, fileSystem);

    }

    private static ToolConfiguration DeserializeToolConfiguration(LockFileTargetLibrary library, DirectoryPath packageDirectory, IFileSystem fileSystem)
    {
        try
        {
            var dotnetToolSettings = FindItemInTargetLibrary(library, ToolSettingsFileName);
            if (dotnetToolSettings == null)
            {
                throw new ToolConfigurationException(
                    CliStrings.MissingToolSettingsFile);
            }

            var toolConfigurationPath =
                packageDirectory
                    .WithSubDirectories(
                        new PackageId(library.Name).ToString(),
                        library.Version.ToNormalizedString().ToLowerInvariant())
                    .WithFile(dotnetToolSettings.Path);

            var configuration = ToolConfigurationDeserializer.Deserialize(toolConfigurationPath.Value, fileSystem);
            return configuration;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            throw new ToolConfigurationException(
                string.Format(
                    CliStrings.FailedToRetrieveToolConfiguration,
                    ex.Message),
                ex);
        }
    }

    private static LockFileTargetLibrary FindLibraryInLockFile(LockFile lockFile)
    {
        return lockFile
            ?.Targets?.SingleOrDefault(t => t.RuntimeIdentifier != null)
            ?.Libraries?.SingleOrDefault();
    }

    private static LockFileItem FindItemInTargetLibrary(LockFileTargetLibrary library, string targetRelativeFilePath)
    {
        return library
            ?.ToolsAssemblies
            ?.SingleOrDefault(t => LockFileMatcher.MatchesFile(t, targetRelativeFilePath));
    }

}
