using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    //  Multiple PackageDownload items for the same package are not supported.  Rather, to download multiple versions of the same
    //  package, the PackageDownload items can have a semicolon-separated list of versions (each in brackets) as the Version metadata.
    //  So this task groups a list of items with PackageVersion metadata into a list of items which can be used as PackageDownloads
    public class CollatePackageDownloads : Task
    {
        [Required]
        public ITaskItem[] Packages { get; set; }
        
        [Output]
        public ITaskItem [] PackageDownloads { get; set; }

        public override bool Execute()
        {
            PackageDownloads = Packages.GroupBy(p => p.ItemSpec)
                .Select(g =>
                {
                    var packageDownloadItem = new TaskItem(g.Key);
                    packageDownloadItem.SetMetadata("Version", string.Join(";",
                        g.Select(p => "[" + p.GetMetadata("PackageVersion") + "]")));
                    return packageDownloadItem;
                }).ToArray();

            return true;
        }
    }
}
