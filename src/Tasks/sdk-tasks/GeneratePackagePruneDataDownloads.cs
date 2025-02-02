// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Generates a list of targeting packs to download.  The package prune data will be extracted from these targeting packs
    /// </summary>
    public class GeneratePackagePruneDataDownloads : Task
    {
        [Required]
        public string NETCoreAppTargetFrameworkVersion { get; set; }

        [Output]
        public ITaskItem[] TargetingPackDownloads { get; set; }

        public override bool Execute()
        {
            List<TaskItem> targetingPackDownloads = new();

            static TaskItem CreateDownload(string frameworkName, string netVersion)
            {
                var item = new TaskItem($"{frameworkName}.Ref");
                item.SetMetadata("PackageVersion", $"{netVersion}.0");
                item.SetMetadata("TargetFrameworkVersion", netVersion);
                item.SetMetadata("FrameworkName", frameworkName);
                return item;
            }

            void AddTargetingPackDownloads(string netVersion)
            {
                targetingPackDownloads.Add(CreateDownload("Microsoft.NETCore.App", netVersion));
                targetingPackDownloads.Add(CreateDownload("Microsoft.AspNetCore.App", netVersion));
                targetingPackDownloads.Add(CreateDownload("Microsoft.WindowsDesktop.App", netVersion));
            }

            int maxNetVersion = NuGetVersion.Parse(NETCoreAppTargetFrameworkVersion).Major;


            //  Add targeting packs for .NET Core 3.0 and 3.1
            AddTargetingPackDownloads("3.0");
            AddTargetingPackDownloads("3.1");

            //  Add targeting packs for .NET 5 and higher.
            //  We don't download the targeting pack for the maximum .NET version here, as we may still be in preview.
            //  Rather, the GetPackagesToPrune task will load the package prune data for the current version from the
            //  targeting packs that ship with the SDK.
            for (int netVersion = 5; netVersion < maxNetVersion; netVersion++)
            {
                AddTargetingPackDownloads($"{netVersion}.0");
            }

            TargetingPackDownloads = targetingPackDownloads.ToArray();


            return true;
        }
    }
}
