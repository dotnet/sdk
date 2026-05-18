// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Multiple PackageDownload items for the same package aren't supported. Rather, to download multiple versions of the same
    /// package, the PackageDownload items can have a semicolon-separated list of versions (each in brackets) as the Version metadata.
    /// So this task groups a list of items with Version metadata into a list of items which can be used as PackageDownloads.
    /// </summary>
    public class CollatePackageDownloads : TaskBase
    {
        [Required]
        public ITaskItem[] Packages { get; set; }
        
        [Output]
        public ITaskItem [] PackageDownloads { get; set; }

        protected override void ExecuteCore()
        {
            PackageDownloads = Packages.GroupBy(p => p.ItemSpec)
                .Select(g =>
                {
                    var packageDownloadItem = new TaskItem(g.Key);
                    packageDownloadItem.SetMetadata("Version", string.Join(";",
                        g.Select(p => "[" + p.GetMetadata("Version") + "]")));
                    return packageDownloadItem;
                }).ToArray();
        }
    }
}
