// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.SourceBuild.Tasks
{
    /// <summary>
    /// For each source-built nupkg info given, ensure that if the package cache contains a package
    /// with the same id and version, the cached nupkg is the same as the source-built one.
    ///
    /// If the package cache contains a package with the same package id and version as a
    /// source-built one, nuget restore short-circuits and doesn't look for the source-built one.
    /// This usually results in prebuilt packages being used, which can either break the build or
    /// end up in the outputs.
    /// </summary>
    public class GetSourceBuiltNupkgCacheConflicts : Task
    {
        /// <summary>
        /// Items containing package id and version of each source-built package.
        /// ReadNuGetPackageInfos is recommended to generate these.
        ///
        /// %(Identity): Path to the original nupkg.
        /// %(PackageId): Identity of the package.
        /// %(PackageVersion): Version of the package.
        /// </summary>
        [Required]
        public ITaskItem[] SourceBuiltPackageInfos { get; set; }

        /// <summary>
        /// Package cache dir containing nupkgs to compare. Path is expected to be like:
        /// 
        /// {PackageCacheDir}/{lowercase id}/{version}/{lowercase id}.{version}.nupkg
        /// </summary>
        [Required]
        public string PackageCacheDir { get; set; }

        /// <summary>
        /// Paths to packages to compare against when conflicts are detected. Knowing where the
        /// package in the cache came from can help diagnose a conflict. For example, is it from
        /// prebuilt/source-built? Or does the build not have the nupkg anywhere else, and
        /// therefore it most likely came from the internet?
        /// </summary>
        public string[] KnownOriginPackagePaths { get; set; }

        [Output]
        public ITaskItem[] ConflictingPackageInfos { get; set; }

        public override bool Execute()
        {
            DateTime startTime = DateTime.Now;

            var knownNupkgs = new Lazy<ILookup<PackageIdentity, string>>(() =>
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    $"Reading all {nameof(KnownOriginPackagePaths)} package identities to search " +
                        "for conflicting package origin...");

                return KnownOriginPackagePaths.NullAsEmpty().ToLookup(
                    ReadNuGetPackageInfos.ReadIdentity,
                    path => path);
            });

            ConflictingPackageInfos = SourceBuiltPackageInfos
                .Where(item =>
                {
                    string sourceBuiltPath = item.ItemSpec;
                    string id = item.GetMetadata("PackageId");
                    string version = item.GetMetadata("PackageVersion");

                    string packageCachePath = Path.Combine(
                        PackageCacheDir,
                        id.ToLowerInvariant(),
                        version,
                        $"{id.ToLowerInvariant()}.{version}.nupkg");

                    if (!File.Exists(packageCachePath))
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            $"OK: Package not found in package cache: {id} {version}");
                        return false;
                    }

                    Log.LogMessage(
                        MessageImportance.Low,
                        $"Package id/version found in package cache, verifying: {id} {version}");

                    byte[] packageCacheBytes = File.ReadAllBytes(packageCachePath);

                    if (packageCacheBytes.SequenceEqual(File.ReadAllBytes(sourceBuiltPath)))
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            $"OK: Package in cache is identical to source-built: {id} {version}");
                        return false;
                    }

                    Log.LogMessage(
                        MessageImportance.Low,
                        "BAD: Source-built nupkg is not byte-for-byte identical " +
                            $"to nupkg in cache: {id} {version}");

                    var ident = new PackageIdentity(id, NuGetVersion.Parse(version));

                    string message = null;

                    foreach (string knownNupkg in knownNupkgs.Value[ident])
                    {
                        if (packageCacheBytes.SequenceEqual(File.ReadAllBytes(knownNupkg)))
                        {
                            Log.LogMessage(
                                MessageImportance.Low,
                                $"Found identity match with identical contents: {knownNupkg}");

                            message = (message ?? "Nupkg found at") + $" '{knownNupkg}'";
                        }
                        else
                        {
                            Log.LogMessage(
                                MessageImportance.Low,
                                $"Package identity match, but contents differ: {knownNupkg}");
                        }
                    }

                    item.SetMetadata(
                        "WarningMessage",
                        message ??
                            "Origin nupkg not found in build directory. It may have been " +
                            "downloaded by NuGet restore.");

                    return true;
                })
                .ToArray();

            // Tell the user about this task, in case it takes a while.
            Log.LogMessage(
                MessageImportance.High,
                "Checked cache for conflicts with source-built nupkgs. " +
                    $"Took {DateTime.Now - startTime}");

            return !Log.HasLoggedErrors;
        }
    }
}
