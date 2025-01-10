using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.ComponentDetection.Detectors.NuGet;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    public class GetPackagesToPrune : TaskBase
    {
        [Required]
        public string TargetFrameworkIdentifier { get; set; }

        [Required]
        public string TargetFrameworkVersion { get; set; }

        [Required]
        public ITaskItem[] FrameworkReferences { get; set; }

        [Output]
        public ITaskItem[] PackagesToPrune { get; set; }

        protected override void ExecuteCore()
        {
            var nugetFramework = new NuGetFramework(TargetFrameworkIdentifier, Version.Parse(TargetFrameworkVersion));

            Dictionary<string, NuGetVersion> packagesToPrune = new();

            var frameworkPackages = FrameworkPackages.GetFrameworkPackages(nugetFramework, FrameworkReferences.Select(fr => fr.ItemSpec).ToArray())
                .SelectMany(packages => packages);

            foreach (var kvp in frameworkPackages)
            {
                if (packagesToPrune.TryGetValue(kvp.Key, out NuGetVersion existingVersion))
                {
                    if (kvp.Value > existingVersion)
                    {
                        packagesToPrune[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    packagesToPrune[kvp.Key] = kvp.Value;
                }
            }

            PackagesToPrune = packagesToPrune.Select(p =>
            {
                var item = new TaskItem(p.Key);
                item.SetMetadata("Version", p.Value.ToString());
                return item;
            }).ToArray();
        }
    }
}
