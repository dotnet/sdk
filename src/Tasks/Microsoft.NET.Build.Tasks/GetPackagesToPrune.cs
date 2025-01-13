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

        public string TargetingPackRoot { get; set; }

        [Output]
        public ITaskItem[] PackagesToPrune { get; set; }

        class CacheKey
        {
            public string TargetFrameworkIdentifier { get; set; }
            public string TargetFrameworkVersion { get; set; }
            public HashSet<string> FrameworkReferences { get; set; }

            public override bool Equals(object? obj) => obj is CacheKey key &&
                TargetFrameworkIdentifier == key.TargetFrameworkIdentifier &&
                TargetFrameworkVersion == key.TargetFrameworkVersion &&
                FrameworkReferences.SetEquals(key.FrameworkReferences);
            public override int GetHashCode()
            {
#if NET
                var hashCode = new HashCode();
                hashCode.Add(TargetFrameworkIdentifier);
                hashCode.Add(TargetFrameworkVersion);
                foreach (var frameworkReference in FrameworkReferences)
                {
                    hashCode.Add(frameworkReference);
                }
                return hashCode.ToHashCode();
#else
                int hashCode = 1436330440;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TargetFrameworkIdentifier);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TargetFrameworkVersion);

                foreach (var frameworkReference in FrameworkReferences)
                {
                    hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(frameworkReference);
                }
                return hashCode;
#endif
            }
        }


        protected override void ExecuteCore()
        {
            CacheKey key = new()
            {
                TargetFrameworkIdentifier = TargetFrameworkIdentifier,
                TargetFrameworkVersion = TargetFrameworkVersion,
                FrameworkReferences = FrameworkReferences.Select(i => i.ItemSpec).ToHashSet()
            };

            //  Cache framework package values per build
            var existingResult = BuildEngine4.GetRegisteredTaskObject(key, RegisteredTaskObjectLifetime.Build);
            if (existingResult != null)
            {
                PackagesToPrune = (ITaskItem[])existingResult;
                return;
            }

            var nugetFramework = new NuGetFramework(TargetFrameworkIdentifier, Version.Parse(TargetFrameworkVersion));

            Dictionary<string, NuGetVersion> packagesToPrune = new();

            var frameworkPackages = FrameworkPackages.GetFrameworkPackages(nugetFramework, FrameworkReferences.Select(fr => fr.ItemSpec).ToArray(), TargetingPackRoot)
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

            BuildEngine4.RegisterTaskObject(key, PackagesToPrune, RegisteredTaskObjectLifetime.Build, true);
        }
    }
}
