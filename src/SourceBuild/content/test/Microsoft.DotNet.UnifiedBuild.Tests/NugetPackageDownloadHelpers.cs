// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.DotNet.UnifiedBuild.Tests;

public static class NugetPackageDownloadHelpers
{
    static string[] NugetIndices = [
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json",
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json",
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json",
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json",
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json",
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json",
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries-transport/nuget/v3/index.json",
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9-transport/nuget/v3/index.json",
        "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10-transport/nuget/v3/index.json",
    ];

    static string DownloadCacheDir = Path.Combine(Config.DownloadCacheDirectory, "NugetPackageBaselines");

    const string PackagesSwitch = Config.RuntimeConfigSwitchPrefix + "Packages";
    static string PackagesConfig { get; } = Config.GetRuntimeConfig(PackagesSwitch);
    static ImmutableArray<string> PackagesPaths { get; } = PackagesConfig.Split(";")
            // Nuget is unable to find fsharp or command-line-api packages for some reason
            .Where(p => !(Path.GetFileName(Path.GetDirectoryName(p)) is "fsharp" or "command-line-api")).ToImmutableArray();

    static string ManifestPath { get; } = Config.GetRuntimeConfig(ManifestSwitch);
    const string ManifestSwitch = Config.RuntimeConfigSwitchPrefix + "AssetManifestPath";

    public static ImmutableArray<string[]> DownloadPackages()
    {
        Directory.CreateDirectory(DownloadCacheDir);

        XmlDocument manifest = new XmlDocument();
        manifest.Load(ManifestPath);
        XmlNodeList packagesInManifest = manifest.SelectNodes("/Build/Package")!;
        List<string[]> packagesInfo = new(packagesInManifest.Count);
        List<Task<string?>> downloadPackageTasks = new(packagesInManifest.Count);
        foreach (XmlNode node in packagesInManifest)
        {
            if (node.Attributes?["DotNetReleaseShipping"]?.Value != "true")
                continue;
            var id = node.Attributes!["Id"]!.Value;
            var version = node.Attributes["Version"]!.Value;
            var path = PackagesPaths.Where(path => Path.GetFileName(path) == $"{id}.{version}.nupkg").Single();
            packagesInfo.Add([id,  path, null!]);
            downloadPackageTasks.Add(DownloadPackage(id, new NuGetVersion(version)));
        }
        var packageDownloadTasks = downloadPackageTasks.ToArray();
        Task.WaitAll(packageDownloadTasks);
        for (int i = 0; i < packageDownloadTasks.Length; i++)
        {
            var task = packageDownloadTasks[i];
            packagesInfo[i][2] = task.Result!;
        }
        return packagesInfo.Where(p => p[2] is not null).ToImmutableArray();
    }

    static async Task<string?> DownloadPackage(string packageId, NuGetVersion packageVersion)
    {
        var packagePath = Path.Combine(DownloadCacheDir, $"{packageId}.{packageVersion.ToFullString()}.nupkg");
        if (File.Exists(packagePath))
        {
            return packagePath;
        }

        bool found = false;
        ILogger logger = NullLogger.Instance;
        CancellationToken cancellationToken = CancellationToken.None;
        SourceCacheContext cache = new SourceCacheContext();
        using (var packageFile = File.Create(packagePath))
        {
            foreach (var nugetRepository in NugetIndices)
            {
                SourceRepository repository = Repository.Factory.GetCoreV3(nugetRepository);
                FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();

                found = await resource.CopyNupkgToStreamAsync(
                    packageId,
                    packageVersion,
                    packageFile,
                    cache,
                    logger,
                    cancellationToken);

                if (found)
                {
                    break;
                }
            }
        }
        if (!found)
        {
            return null;
        }
        return packagePath;
    }
}