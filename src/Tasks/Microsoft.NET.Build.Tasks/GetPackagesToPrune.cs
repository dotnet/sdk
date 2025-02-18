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
        public ITaskItem[] TargetingPacks { get; set; }

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

            //  Map framework references to runtime frameworks, so we can correctly handle framework references to profiles.
            //  For example, for a framework reference of Microsoft.WindowsDesktop.App.WindowsForms, we map it to the
            //  runtime framework of Microsoft.WindowsDesktop.App, which is what the pruned packages are defined in terms of
            List<string> runtimeFrameworks = new List<string>();

            foreach (var frameworkReference in filteredFrameworkReferences)
            {
                //  Number of framework references is generally low enough that it's not worth putting the targeting packs into a hash set
                var targetingPack = TargetingPacks.FirstOrDefault(tp => tp.ItemSpec.Equals(frameworkReference.ItemSpec, StringComparison.OrdinalIgnoreCase));
                if (targetingPack != null)
                {
                    runtimeFrameworks.Add(targetingPack.GetMetadata("RuntimeFrameworkName"));
                }
            }

            CacheKey key = new()
            {
                TargetFrameworkIdentifier = TargetFrameworkIdentifier,
                TargetFrameworkVersion = TargetFrameworkVersion,
                FrameworkReferences = runtimeFrameworks.ToHashSet()
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

            var targetFrameworkVersion = Version.Parse(key.TargetFrameworkVersion);

            if (key.FrameworkReferences.Count == 0)
            {
                if (key.TargetFrameworkIdentifier.Equals(".NETCoreApp") && targetFrameworkVersion.Major >= 3)
                {
                    //  For .NET Core projects (3.0 and higher), don't prune any packages if there are no framework references
                    return Array.Empty<TaskItem>();
                }
            }

            bool useFrameworkPackageData;
            if (!key.TargetFrameworkIdentifier.Equals(".NETCoreApp") || targetFrameworkVersion.Major < 10)
            {
                //  Use hard-coded / generated "framework package data" for .NET 9 and lower, .NET Framework, and .NET Standard
                useFrameworkPackageData = true;
            }
            else
            {
                //  Use bundled "prune package data" for .NET 10 and higher.  During the redist build, this comes from targeting packs and
                //  is laid out in the PrunePackageData folder.
                useFrameworkPackageData = false;
            }

            //  Call DefaultIfEmpty() so that target frameworks without framework references will load data
            foreach (var frameworkReference in key.FrameworkReferences.DefaultIfEmpty(""))
            {
                Dictionary<string, NuGetVersion> packagesForFrameworkReference;
                if (useFrameworkPackageData)
                {
                    packagesForFrameworkReference = LoadPackagesToPruneFromFrameworkPackages(key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference, targetingPackRoot);
                }
                else
                {
                    packagesForFrameworkReference = LoadPackagesToPruneFromPrunePackageData(key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference, prunePackageDataRoot);

                    //  For the version of the runtime that matches the current SDK version, we don't include the prune package data in the PrunePackageData folder.  Rather,
                    //  we can load it from the targeting packs that are packaged with the SDK.
                    if (packagesForFrameworkReference == null)
                    {
                        packagesForFrameworkReference = LoadPackagesToPruneFromTargetingPack(key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference, targetingPackRoot);
                    }
                }

                if (packagesForFrameworkReference == null)
                {
                    //  We didn't find the data for packages to prune.  This indicates that there's a bug in the SDK construction, so fail hard here so that we fix that
                    //  (rather than a warning that might be missed).
                    //  Since this indicates an error in the SDK build, the message probably doesn't need to be localized.
                    throw new Exception($"Prune package data not found for {key.TargetFrameworkIdentifier} {key.TargetFrameworkVersion} {frameworkReference}");
                }

                AddPackagesToPrune(packagesToPrune, packagesForFrameworkReference.Select(kvp => (kvp.Key, kvp.Value)), log);
            }

            return packagesToPrune.Select(p =>
            {
                var item = new TaskItem(p.Key);

                //  If a given version of a package is included in a framework, assume that any patches
                //  to that package will be included in patches to the framework, and thus should be pruned.
                //  See https://github.com/dotnet/sdk/issues/44566
                //  To do this, we set the patch version for the package to be pruned to 32767, which should be
                //  higher than any actual patch version.
                var maxPatch = new NuGetVersion(p.Value.Major, p.Value.Minor, 32767);

                item.SetMetadata("Version", maxPatch.ToString());
                return item;
            }).ToArray();
        }

        static Dictionary<string, NuGetVersion> LoadPackagesToPruneFromFrameworkPackages(string targetFrameworkIdentifier, string targetFrameworkVersion, string frameworkReference, string targetingPackRoot)
        {
            var nugetFramework = new NuGetFramework(targetFrameworkIdentifier, Version.Parse(targetFrameworkVersion));

            //  FrameworkPackages just has data for .NET Framework 4.6.1 and doesn't handle framework compatibility, so treat anything greater than .NET Framework as if it were 4.6.1
            if (nugetFramework.IsDesktop() && nugetFramework.Version > new Version(4,6,1))
            {
                nugetFramework = NuGetFramework.Parse("net461");
            }

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

        static Dictionary<string, NuGetVersion> LoadPackagesToPruneFromTargetingPack(string targetFrameworkIdentifier, string targetFrameworkVersion, string frameworkReference, string targetingPackRoot)
        {
            var nugetFramework = new NuGetFramework(targetFrameworkIdentifier, Version.Parse(targetFrameworkVersion));

            var frameworkPackages = FrameworkPackages.LoadFrameworkPackagesFromPack(nugetFramework, frameworkReference, targetingPackRoot)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return frameworkPackages;
        }

        static void AddPackagesToPrune(Dictionary<string, NuGetVersion> packagesToPrune, IEnumerable<(string id, NuGetVersion version)> packagesToAdd, Logger log)
        {
            foreach (var package in packagesToAdd)
            {
                if (packagesToPrune.TryGetValue(package.id, out NuGetVersion existingVersion))
                {
                    if (package.version > existingVersion)
                    {
                        //  There are some "inconsistent" versions in the FrameworkPackages data.  This happens because, for example, the ASP.NET Core shared framework for .NET 9 inherits
                        //  from the ASP.NET Core shared framework for .NET 8, but not from the base Microsoft.NETCore.App framework for .NET 9.  So for something like System.IO.Pipelines,
                        //  which was in ASP.NET in .NET 8 but moved to the base shared framework in .NET 9, we will see an 8.0 version from the ASP.NET shared framework and a 9.0 version
                        //  from the base shared framework.  As long as the base shared framework is always referenced together with the ASP.NET shared framework, this shouldn't be a
                        //  problem, and we can just pick the latest version of the package that we see.
                        packagesToPrune[package.id] = package.version;
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
