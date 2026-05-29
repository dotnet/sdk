// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    //  Locates the root NuGet package directory for each of the input items that has PackageName and PackageVersion,
    //  but not PackageDirectory metadata specified
    [MSBuildMultiThreadableTask]
    public class GetPackageDirectory : TaskBase, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        public ITaskItem[] Items { get; set; } = Array.Empty<ITaskItem>();

        public string[] PackageFolders { get; set; } = Array.Empty<string>();

        public string AssetsFileWithAdditionalPackageFolders { get; set; }

        [Output]
        public ITaskItem[] Output { get; set; }

        protected override void ExecuteCore()
        {
            if (Items.Length == 0 || (PackageFolders.Length == 0 && string.IsNullOrEmpty(AssetsFileWithAdditionalPackageFolders)))
            {
                Output = Items;
                return;
            }

            if (!string.IsNullOrEmpty(AssetsFileWithAdditionalPackageFolders))
            {
                AbsolutePath assetsFilePath = TaskEnvironment.GetAbsolutePath(AssetsFileWithAdditionalPackageFolders);
                var lockFileCache = new LockFileCache(this);
                var lockFile = lockFileCache.GetLockFile(assetsFilePath);
                PackageFolders = PackageFolders.Concat(lockFile.PackageFolders.Select(p => p.Path)).ToArray();
            }

            // NuGetPackageResolver probes the file system for each folder, so paths must be
            // absolutized relative to the project rather than the process working directory.
            string[] absolutePackageFolders = PackageFolders
                .Select(p => string.IsNullOrEmpty(p) ? p : (string)TaskEnvironment.GetAbsolutePath(p))
                .ToArray();

            var packageResolver = NuGetPackageResolver.CreateResolver(absolutePackageFolders);

            int index = 0;
            var updatedItems = new ITaskItem[Items.Length];

            foreach (var item in Items)
            {
                string packageName = item.GetMetadata(MetadataKeys.NuGetPackageId);
                string packageVersion = item.GetMetadata(MetadataKeys.NuGetPackageVersion);

                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion)
                    || !string.IsNullOrEmpty(item.GetMetadata(MetadataKeys.PackageDirectory)))
                {
                    updatedItems[index++] = item;
                    continue;
                }

                var parsedPackageVersion = NuGetVersion.Parse(packageVersion);
                string packageDirectory = packageResolver.GetPackageDirectory(packageName, parsedPackageVersion, out string packageRoot);

                if (packageDirectory == null)
                {
                    updatedItems[index++] = item;
                    continue;
                }

                // If the resolver matched against a folder we absolutized, rewrite the result to use
                // the caller's original (possibly relative) prefix so PackageDirectory metadata is
                // byte-identical to the pre-migration behavior.
                if (packageRoot != null)
                {
                    int folderIndex = Array.IndexOf(absolutePackageFolders, packageRoot);
                    if (folderIndex >= 0
                        && !string.Equals(PackageFolders[folderIndex], packageRoot, StringComparison.Ordinal))
                    {
                        packageDirectory = PackageFolders[folderIndex] + packageDirectory.Substring(packageRoot.Length);
                    }
                }

                var newItem = new TaskItem(item);
                newItem.SetMetadata(MetadataKeys.PackageDirectory, packageDirectory);
                updatedItems[index++] = newItem;
            }

            Output = updatedItems;
        }
    }
}
