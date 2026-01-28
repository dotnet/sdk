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

        var toolConfiguration = DeserializeToolConfiguration(library, packageDirectory, ResolvedPackageId, _fileSystem);
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
        return DeserializeToolConfiguration(lockFileTargetLibrary, packageDirectory, id, fileSystem);

    }

    private static ToolConfiguration DeserializeToolConfiguration(LockFileTargetLibrary library, DirectoryPath packageDirectory, PackageId packageId, IFileSystem fileSystem)
    {
        try
        {
            var dotnetToolSettings = FindItemInTargetLibrary(library, ToolSettingsFileName);
            if (dotnetToolSettings == null)
            {
                // Check if this is because of framework incompatibility
                // Load available frameworks from the package to provide better error messages
                var installPath = new VersionFolderPathResolver(packageDirectory.Value).GetInstallPath(library.Name, library.Version);
                var toolsPackagePath = Path.Combine(installPath, "tools");
                
                if (fileSystem.Directory.Exists(toolsPackagePath))
                {
                    var availableFrameworks = fileSystem.Directory.EnumerateDirectories(toolsPackagePath)
                        .Select(path => NuGetFramework.ParseFolder(Path.GetFileName(path)))
                        .Where(f => f.Framework == FrameworkConstants.FrameworkIdentifiers.NetCoreApp)
                        .ToList();

                    if (availableFrameworks.Count > 0)
                    {
                        // Find the minimum framework version required by the tool
                        var minRequiredFramework = availableFrameworks.MinBy(f => f.Version);

                        if (minRequiredFramework != null)
                        {
                            bool hasCompatibleRuntime = false;
                            
                            // First, try to use hostfxr to resolve frameworks using the runtimeconfig.json
                            // This is the most accurate way to check compatibility
                            try
                            {
                                // Try to find the runtimeconfig.json file
                                // It should be in tools/{framework}/{rid}/*.runtimeconfig.json
                                var frameworkPath = Path.Combine(toolsPackagePath, $"net{minRequiredFramework.Version.Major}.{minRequiredFramework.Version.Minor}");
                                if (fileSystem.Directory.Exists(frameworkPath))
                                {
                                    // Search for runtimeconfig.json files with max depth of 3 levels
                                    var runtimeConfigFiles = new List<string>();
                                    SearchForRuntimeConfigFiles(frameworkPath, runtimeConfigFiles, fileSystem, currentDepth: 0, maxDepth: 3);
                                    
                                    if (runtimeConfigFiles.Any())
                                    {
                                        // Use the first runtimeconfig.json we find (sorted for determinism)
                                        // All runtimeconfig.json files in a tool package should have the same framework requirements
                                        var runtimeConfigPath = runtimeConfigFiles.OrderBy(f => f).First();
                                        hasCompatibleRuntime = InstalledRuntimeEnumerator.CanResolveFrameworks(runtimeConfigPath);
                                    }
                                }
                            }
                            catch
                            {
                                // If hostfxr resolution fails, fall back to version-based check
                                // This ensures tool installation continues even if hostfxr is unavailable
                            }
                            
                            // If hostfxr resolution didn't work, fall back to version-based check
                            if (!hasCompatibleRuntime)
                            {
                                hasCompatibleRuntime = InstalledRuntimeEnumerator.IsCompatibleRuntimeAvailable(minRequiredFramework, allowRollForward: false);
                            }
                            
                            if (!hasCompatibleRuntime)
                            {
                                var requiredVersionString = $".NET {minRequiredFramework.Version.Major}.{minRequiredFramework.Version.Minor}";
                                
                                // Check if roll-forward would help
                                bool rollForwardWouldHelp = InstalledRuntimeEnumerator.WouldRollForwardHelp(minRequiredFramework);
                                
                                string errorMessage;
                                string suggestions;
                                
                                if (rollForwardWouldHelp)
                                {
                                    errorMessage = string.Format(
                                        CliStrings.ToolRequiresRuntimeNotInstalledWithRollForward,
                                        packageId,
                                        requiredVersionString);
                                    
                                    suggestions = string.Format(
                                        CliStrings.ToolRequiresRuntimeSuggestions,
                                        requiredVersionString);
                                }
                                else
                                {
                                    errorMessage = string.Format(
                                        CliStrings.ToolRequiresRuntimeNotInstalled,
                                        packageId,
                                        requiredVersionString);
                                    
                                    suggestions = string.Format(
                                        CliStrings.ToolRequiresRuntimeSuggestionsNoRollForward,
                                        requiredVersionString);
                                }
                                
                                throw new GracefulException($"{errorMessage}\n\n{suggestions}", isUserError: false);
                            }
                        }
                    }
                }

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

    private static void SearchForRuntimeConfigFiles(string directory, List<string> results, IFileSystem fileSystem, int currentDepth, int maxDepth)
    {
        if (currentDepth >= maxDepth)
        {
            return;
        }

        foreach (var file in fileSystem.Directory.EnumerateFiles(directory))
        {
            if (file.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(file);
            }
        }

        foreach (var subdir in fileSystem.Directory.EnumerateDirectories(directory))
        {
            SearchForRuntimeConfigFiles(subdir, results, fileSystem, currentDepth + 1, maxDepth);
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
