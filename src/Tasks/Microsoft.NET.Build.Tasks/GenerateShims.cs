﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class GenerateShims : TaskBase
    {
        private NuGetPackageResolver _packageResolver;

        /// <summary>
        /// Path to assets.json.
        /// </summary>
        [Required]
        public string ProjectAssetsFile { get; set; }

        /// <summary>
        /// The file name of Apphost asset.
        /// </summary>
        [Required]
        public string DotNetAppHostExecutableNameWithoutExtension { get; set; }

        /// <summary>
        /// Path to project file (.csproj|.vbproj|.fsproj)
        /// </summary>
        [Required]
        public string ProjectPath { get; set; }

        /// <summary>
        /// TFM to use for compile-time assets.
        /// </summary>
        [Required]
        public string TargetFrameworkMoniker { get; set; }

        /// <summary>
        /// PackageId of the dotnet tool NuGet Package.
        /// </summary>
        [Required]
        public string PackageId { get; set; }

        /// <summary>
        /// Package Version of the dotnet tool NuGet Package.
        /// </summary>
        [Required]
        public string PackageVersion { get; set; }

        /// <summary>
        /// The command name of the dotnet tool.
        /// </summary>
        [Required]
        public string ToolCommandName { get; set; }

        /// <summary>
        /// The entry point of the dotnet tool which will be run by Apphost
        /// </summary>
        [Required]
        public string ToolEntryPoint { get; set; }

        /// <summary>
        /// The output directory path of generated shims.
        /// </summary>
        [Required]
        public string PackagedShimOutputDirectory { get; set; }

        /// <summary>
        /// The RuntimeIdentifiers that shims will be generated for.
        /// </summary>
        [Required]
        public ITaskItem[] ShimRuntimeIdentifiers { get; set; }

        /// <summary>
        /// Path of generated shims. metadata "ShimRuntimeIdentifier" is used to map back to input ShimRuntimeIdentifiers.
        /// </summary>
        [Output]
        public ITaskItem[] EmbeddedApphostPaths { get; private set; }

        protected override void ExecuteCore()
        {
            NuGetFramework targetFramework = NuGetUtils.ParseFrameworkName(TargetFrameworkMoniker);
            LockFile lockFile = new LockFileCache(this).GetLockFile(ProjectAssetsFile);
            _packageResolver = NuGetPackageResolver.CreateResolver(lockFile, ProjectPath);

            var embeddedApphostPaths = new List<ITaskItem>();
            foreach (var runtimeIdentifier in ShimRuntimeIdentifiers.Select(r => r.ItemSpec))
            {
                var resolvedApphostAssetPath = GetApphostAsset(targetFramework, lockFile, runtimeIdentifier);

                var packagedShimOutputDirectoryAndRid = Path.Combine(
                        PackagedShimOutputDirectory,
                        runtimeIdentifier);

                var appHostDestinationFilePath = Path.Combine(
                        packagedShimOutputDirectoryAndRid,
                        ToolCommandName + Path.GetExtension(resolvedApphostAssetPath));

                Directory.CreateDirectory(packagedShimOutputDirectoryAndRid);

                // This is the embedded string. We should normalize it on forward slash, so the file won't be different according to
                // build machine.
                var appBinaryFilePath = string.Join("/",
                    new[] {
                        ".store",
                        PackageId.ToLowerInvariant(),
                        PackageVersion,
                        PackageId.ToLowerInvariant(),
                        PackageVersion,
                        "tools",
                        targetFramework.GetShortFolderName(),
                        "any",
                        ToolEntryPoint});

                AppHost.Create(
                    resolvedApphostAssetPath,
                    appHostDestinationFilePath,
                    appBinaryFilePath,
                    overwriteExisting: true
                );

                var item = new TaskItem(appHostDestinationFilePath);
                item.SetMetadata("ShimRuntimeIdentifier", runtimeIdentifier);
                embeddedApphostPaths.Add(item);
            }

            EmbeddedApphostPaths = embeddedApphostPaths.ToArray();
        }

        private string GetApphostAsset(NuGetFramework targetFramework, LockFile lockFile, string runtimeIdentifier)
        {
            var apphostName = DotNetAppHostExecutableNameWithoutExtension;

            if (runtimeIdentifier.StartsWith("win"))
            {
                apphostName += ".exe";
            }

            LockFileTarget runtimeTarget = lockFile.GetTargetAndThrowIfNotFound(targetFramework, runtimeIdentifier);

            return FindApphostInRuntimeTarget(apphostName, runtimeTarget);
        }

        private string FindApphostInRuntimeTarget(string apphostName, LockFileTarget runtimeTarget)
        {
            foreach (LockFileTargetLibrary library in runtimeTarget.Libraries)
            {
                if (!library.IsPackage())
                {
                    continue;
                }

                foreach (LockFileItem asset in library.NativeLibraries)
                {
                    if (asset.IsPlaceholderFile())
                    {
                        continue;
                    }

                    var resolvedPackageAssetPath = _packageResolver.ResolvePackageAssetPath(library, asset.Path);

                    if (Path.GetFileName(resolvedPackageAssetPath) == apphostName)
                    {
                        return resolvedPackageAssetPath;
                    }
                }
            }

            throw new BuildErrorException(Strings.CannotFindApphostForRid, runtimeTarget.RuntimeIdentifier);
        }
    }
}
