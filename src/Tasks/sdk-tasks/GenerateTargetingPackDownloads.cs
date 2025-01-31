// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Gets version and commit of a dependency by its name
    /// from eng/Version.Details.xml
    /// </summary>
    public class GenerateTargetingPackDownloads : Task
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

            int maxNetVersion = System.Version.Parse(NETCoreAppTargetFrameworkVersion).Major;

            //  We don't download the targeting pack for the maximum .NET version here, as we may still be in preview.
            //  Rather, the GetPackagesToPrune task will load the package prune data for the current version from the
            //  targeting packs that ship with the SDK.

            for (int netVersion = 3; netVersion < maxNetVersion; netVersion++)
            {
                if (netVersion == 4)
                {
                    //  No .NET 4
                    continue;
                }

                AddTargetingPackDownloads($"{netVersion}.0");
                if (netVersion == 3)
                {
                    //  Special case .NET Core 3.1
                    AddTargetingPackDownloads("3.1");
                }
            }

            TargetingPackDownloads = targetingPackDownloads.ToArray();


            return true;
        }
    }
}
