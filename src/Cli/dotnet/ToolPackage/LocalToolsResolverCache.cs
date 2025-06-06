// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal class LocalToolsResolverCache : ILocalToolsResolverCache
{
    private readonly DirectoryPath _cacheVersionedDirectory;
    private readonly IFileSystem _fileSystem;
    private const int LocalToolResolverCacheVersion = 1;

    public LocalToolsResolverCache(IFileSystem fileSystem = null,
        DirectoryPath? cacheDirectory = null,
        int version = LocalToolResolverCacheVersion)
    {
        _fileSystem = fileSystem ?? new FileSystemWrapper();
        DirectoryPath appliedCacheDirectory =
            cacheDirectory ?? new DirectoryPath(Path.Combine(CliFolderPathCalculator.ToolsResolverCachePath));
        _cacheVersionedDirectory = appliedCacheDirectory.WithSubDirectories(version.ToString());
    }

    public void Save(
        IDictionary<RestoredCommandIdentifier, ToolCommand> restoredCommandMap)
    {
        EnsureFileStorageExists();

        foreach (var distinctPackageIdAndRestoredCommandMap in restoredCommandMap.GroupBy(x => x.Key.PackageId))
        {
            PackageId distinctPackageId = distinctPackageIdAndRestoredCommandMap.Key;
            string packageCacheFile = GetCacheFile(distinctPackageId);
            if (_fileSystem.File.Exists(packageCacheFile))
            {
                var existingCacheTable = GetCacheTable(packageCacheFile);

                var diffedRow = distinctPackageIdAndRestoredCommandMap
                    .Where(pair => !TryGetMatchingRestoredCommand(
                        pair.Key,
                        existingCacheTable, out _))
                    .Select(pair => ConvertToCacheRow(pair.Key, pair.Value));

                _fileSystem.File.WriteAllText(
                    packageCacheFile,
                    JsonSerializer.Serialize(existingCacheTable.Concat(diffedRow)));
            }
            else
            {
                var rowsToAdd =
                    distinctPackageIdAndRestoredCommandMap
                        .Select(mapWithSamePackageId
                            => ConvertToCacheRow(
                                mapWithSamePackageId.Key,
                                mapWithSamePackageId.Value));

                _fileSystem.File.WriteAllText(
                    packageCacheFile,
                    JsonSerializer.Serialize(rowsToAdd));
            }
        }
    }

    public bool TryLoad(
        RestoredCommandIdentifier restoredCommandIdentifier,
        out ToolCommand toolCommand)
    {
        string packageCacheFile = GetCacheFile(restoredCommandIdentifier.PackageId);
        if (_fileSystem.File.Exists(packageCacheFile))
        {
            if (TryGetMatchingRestoredCommand(
                restoredCommandIdentifier,
                GetCacheTable(packageCacheFile),
                out toolCommand))
            {
                return true;
            }
        }

        toolCommand = null;
        return false;
    }

    private CacheRow[] GetCacheTable(string packageCacheFile)
    {
        CacheRow[] cacheTable = [];

        try
        {
            cacheTable =
                JsonSerializer.Deserialize<CacheRow[]>(_fileSystem.File.ReadAllText(packageCacheFile));
        }
        catch (JsonException)
        {
            // if file is corrupted, treat it as empty since it is not the source of truth
        }

        return cacheTable;
    }

    private string GetCacheFile(PackageId packageId)
    {
        return _cacheVersionedDirectory.WithFile(packageId.ToString()).Value;
    }

    private void EnsureFileStorageExists()
    {
        _fileSystem.Directory.CreateDirectory(_cacheVersionedDirectory.Value);
    }

    private static CacheRow ConvertToCacheRow(
        RestoredCommandIdentifier restoredCommandIdentifier,
        ToolCommand toolCommand)
    {
        return new CacheRow
        {
            Version = restoredCommandIdentifier.Version.ToNormalizedString(),
            TargetFramework = restoredCommandIdentifier.TargetFramework.GetShortFolderName(),
            RuntimeIdentifier = restoredCommandIdentifier.RuntimeIdentifier.ToLowerInvariant(),
            Name = restoredCommandIdentifier.CommandName.Value,
            Runner = toolCommand.Runner,
            PathToExecutable = toolCommand.Executable.Value
        };
    }

    private static
        (RestoredCommandIdentifier restoredCommandIdentifier,
        ToolCommand toolCommand)
        Convert(
            PackageId packageId,
            CacheRow cacheRow)
    {
        RestoredCommandIdentifier restoredCommandIdentifier =
            new(
                packageId,
                NuGetVersion.Parse(cacheRow.Version),
                NuGetFramework.Parse(cacheRow.TargetFramework),
                cacheRow.RuntimeIdentifier,
                new ToolCommandName(cacheRow.Name));

        ToolCommand toolCommand =
            new(
                new ToolCommandName(cacheRow.Name),
                cacheRow.Runner,
                new FilePath(cacheRow.PathToExecutable));

        return (restoredCommandIdentifier, toolCommand);
    }

    private static bool TryGetMatchingRestoredCommand(
        RestoredCommandIdentifier restoredCommandIdentifier,
        CacheRow[] cacheTable,
        out ToolCommand toolCommandList)
    {
        (RestoredCommandIdentifier restoredCommandIdentifier, ToolCommand toolCommand)[] matchingRow =
            [.. cacheTable
                .Select(c => Convert(restoredCommandIdentifier.PackageId, c))
                .Where(candidate => candidate.restoredCommandIdentifier == restoredCommandIdentifier)];

        if (matchingRow.Length >= 2)
        {
            throw new ResolverCacheInconsistentException(
                $"more than one row for {restoredCommandIdentifier.DebugToString()}");
        }

        if (matchingRow.Length == 1)
        {
            toolCommandList = matchingRow[0].toolCommand;
            return true;
        }

        toolCommandList = null;
        return false;
    }

    private class CacheRow
    {
        public string Version { get; set; }
        public string TargetFramework { get; set; }
        public string RuntimeIdentifier { get; set; }
        public string Name { get; set; }
        public string Runner { get; set; }
        public string PathToExecutable { get; set; }
    }
}
