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

            string[] absolutePackageFolders = PackageFolders
                .Select(p => string.IsNullOrEmpty(p) ? p : (string)TaskEnvironment.GetAbsolutePath(p))
                .ToArray();

            var originalByAbsolute = new Dictionary<string, string>(PackageFolders.Length, StringComparer.Ordinal);
            for (int i = 0; i < PackageFolders.Length; i++)
            {
                if (!string.IsNullOrEmpty(absolutePackageFolders[i]))
                {
                    originalByAbsolute[absolutePackageFolders[i]] = PackageFolders[i];
                }
            }

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

                // Restore the caller's original (possibly relative) folder prefix so the
                // PackageDirectory metadata is unchanged from before absolutization.
                if (packageRoot != null
                    && originalByAbsolute.TryGetValue(packageRoot, out string originalRoot)
                    && !string.Equals(originalRoot, packageRoot, StringComparison.Ordinal))
                {
                    packageDirectory = originalRoot + packageDirectory.Substring(packageRoot.Length);
                }

                var newItem = new TaskItem(item);
                newItem.SetMetadata(MetadataKeys.PackageDirectory, packageDirectory);
                updatedItems[index++] = newItem;
            }

            Output = updatedItems;
        }
    }
}
