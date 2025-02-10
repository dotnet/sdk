using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.ComponentDetection.Detectors.NuGet;
using Microsoft.NET.Build.Tasks.ConflictResolution;
using NuGet.Frameworks;
using NuGet.Versioning;


//  Use FrameworkPackages data for .NET Core 3.1 through 9.0, as well as .NET Standard and .NET Framework
//  Use targeting pack data for .NET 10 and higher
//  Issues to fix in targeting packs:
//  - Add PackageOverrides to WPF targeting pack
//  - Add Microsoft.AspNetCore.App package to PackageOverrides.txt for corresponding shared framework - File issue in https://github.com/dotnet/aspnetcore for this

//  .NET Standard?

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

        [Required]
        public string TargetingPackRoot { get; set; }

        [Required]
        public string PrunePackageDataRoot { get; set; }

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

            //  Filter out transitive framework references.  Normally they wouldn't be passed to this task, but in Visual Studio design-time builds
            //  the ResolvePackageAssets and AddTransitiveFrameworkReferences targets may have already run.  Filtering these references out should
            //  avoid a bug similar to https://github.com/dotnet/sdk/issues/14641
            var filteredFrameworkReferences = FrameworkReferences.Where(
                i => i.GetMetadata("IsTransitiveFrameworkReference") is string transitiveVal && !transitiveVal.Equals("true", StringComparison.OrdinalIgnoreCase)).ToList();

            CacheKey key = new()
            {
                TargetFrameworkIdentifier = TargetFrameworkIdentifier,
                TargetFrameworkVersion = TargetFrameworkVersion,
                FrameworkReferences = filteredFrameworkReferences.Select(i => i.ItemSpec).ToHashSet()
            };

            //  Cache framework package values per build
            var existingResult = BuildEngine4.GetRegisteredTaskObject(key, RegisteredTaskObjectLifetime.Build);
            if (existingResult != null)
            {
                PackagesToPrune = (TaskItem[])existingResult;
                return;
            }

            PackagesToPrune = LoadPackagesToPrune(key, TargetingPackRoot, PrunePackageDataRoot, Log);

            BuildEngine4.RegisterTaskObject(key, PackagesToPrune, RegisteredTaskObjectLifetime.Build, true);
        }

        static TaskItem[] LoadPackagesToPrune(CacheKey key, string targetingPackRoot, string prunePackageDataRoot, Logger log)
        {
            Dictionary<string, NuGetVersion> packagesToPrune = new();

            foreach (var frameworkReference in key.FrameworkReferences)
            {
                var packagesFromFrameworkPackages = LoadPackagesToPruneFromFrameworkPackages(key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference, targetingPackRoot);
                var packagesFromPrunePackageData = LoadPackagesToPruneFromPrunePackageData(key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference, prunePackageDataRoot);

                //  TODO: What about the WindowsDesktop profiles?
                if (packagesFromPrunePackageData == null && !frameworkReference.Equals("Microsoft.WindowsDesktop.App", StringComparison.OrdinalIgnoreCase))
                {
                    log.LogError("NETSDK9999: Prune package data not found for {0}", frameworkReference);
                }

                if (packagesFromPrunePackageData != null)
                {

                    foreach (var missingPackage in packagesFromFrameworkPackages.Keys.Except(packagesFromPrunePackageData.Keys))
                    {
                        log.LogError("NETSDK9999: Prune package data mismatch: Package {0} was listed in framework packages data but not in prune package data for {1} {2}", missingPackage, frameworkReference, key.TargetFrameworkVersion);
                    }

                    foreach (var missingPackage in packagesFromPrunePackageData.Keys.Except(packagesFromFrameworkPackages.Keys))
                    {
                        log.LogError("NETSDK9999: Prune package data mismatch: Package {0} was listed in prune package data but not in framework packages data for {1} {2}", missingPackage, frameworkReference, key.TargetFrameworkVersion);
                    }

                    foreach (var package in packagesFromFrameworkPackages)
                    {
                        if (packagesFromPrunePackageData.TryGetValue(package.Key, out var other) && package.Value != other)
                        {
                            log.LogError($"NETSDK9999: Prune package data mismatch: Package {package.Key} had version {package.Value} in framework packages data but version {other} in prune package data.");
                        }
                    }
                }

                AddPackagesToPrune(packagesToPrune, packagesFromFrameworkPackages.Select(kvp => (kvp.Key, kvp.Value)), log);
            }

            return packagesToPrune.Select(p =>
            {
                var item = new TaskItem(p.Key);
                item.SetMetadata("Version", p.Value.ToString());
                return item;
            }).ToArray();
        }

        static Dictionary<string, NuGetVersion> LoadPackagesToPruneFromFrameworkPackages(string targetFrameworkIdentifier, string targetFrameworkVersion, string frameworkReference, string targetingPackRoot)
        {
            var nugetFramework = new NuGetFramework(targetFrameworkIdentifier, Version.Parse(targetFrameworkVersion));

            var frameworkPackages = FrameworkPackages.GetFrameworkPackages(nugetFramework, [frameworkReference], targetingPackRoot)
                .SelectMany(packages => packages)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return frameworkPackages;
        }

        static Dictionary<string, NuGetVersion> LoadPackagesToPruneFromPrunePackageData(string targetFrameworkIdentifier, string targetFrameworkVersion, string frameworkReference, string prunePackageDataRoot)
        {
            if (frameworkReference.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase))
            {
                string packageOverridesPath = Path.Combine(prunePackageDataRoot, targetFrameworkVersion, frameworkReference, "PackageOverrides.txt");
                if (File.Exists(packageOverridesPath))
                {
                    var packageOverrideLines = File.ReadAllLines(packageOverridesPath);
                    var overrides = PackageOverride.CreateOverriddenPackages(packageOverrideLines);
                    return overrides.ToDictionary(o => o.id, o => o.version);

                }
            }

            return null;
        }

        static void AddPackagesToPrune(Dictionary<string, NuGetVersion> packagesToPrune, IEnumerable<(string id, NuGetVersion version)> packagesToAdd, Logger log)
        {
            foreach (var package in packagesToAdd)
            {
                if (packagesToPrune.TryGetValue(package.id, out NuGetVersion existingVersion))
                {
                    if (package.version != existingVersion)
                    {
                        log.LogError($"NETSDK9999: Conflicting prune package data for {package.id}: {package.version}, {existingVersion}");
                    }
                }
                else
                {
                    packagesToPrune[package.id] = package.version;
                }
            }
        }
    }
}
