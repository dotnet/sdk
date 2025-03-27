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
        public string[] TargetingPackRoots { get; set; }

        [Required]
        public string PrunePackageDataRoot { get; set; }

        [Required]
        public bool AllowMissingPrunePackageData { get; set; }

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

            PackagesToPrune = LoadPackagesToPrune(key, TargetingPackRoots, PrunePackageDataRoot, Log, AllowMissingPrunePackageData);

            BuildEngine4.RegisterTaskObject(key, PackagesToPrune, RegisteredTaskObjectLifetime.Build, true);
        }

        static TaskItem[] LoadPackagesToPrune(CacheKey key, string[] targetingPackRoots, string prunePackageDataRoot, Logger log, bool allowMissingPrunePackageData)
        {
            Dictionary<string, NuGetVersion> packagesToPrune = new();

            var targetFrameworkVersion = Version.Parse(key.TargetFrameworkVersion);

            if (key.FrameworkReferences.Count == 0 && key.TargetFrameworkIdentifier.Equals(".NETCoreApp") && targetFrameworkVersion.Major >= 3)
            {
                //  For .NET Core projects (3.0 and higher), don't prune any packages if there are no framework references
                return Array.Empty<TaskItem>();
            }

            // Use hard-coded / generated "framework package data" for .NET 9 and lower, .NET Framework, and .NET Standard
            // Use bundled "prune package data" for .NET 10 and higher.  During the redist build, this comes from targeting packs and is laid out in the PrunePackageData folder.
            bool useFrameworkPackageData = !key.TargetFrameworkIdentifier.Equals(".NETCoreApp") || targetFrameworkVersion.Major < 10;

            //  Call DefaultIfEmpty() so that target frameworks without framework references will load data
            foreach (var frameworkReference in key.FrameworkReferences.DefaultIfEmpty(""))
            {
                //  Filter out framework references we don't expect to have prune data for, such as Microsoft.Windows.SDK.NET.Ref
                if (!frameworkReference.Equals(string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    !frameworkReference.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase) &&
                    !frameworkReference.Equals("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase) &&
                    !frameworkReference.Equals("Microsoft.WindowsDesktop.App", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                log.LogMessage(MessageImportance.Low, $"Loading packages to prune for {key.TargetFrameworkIdentifier} {key.TargetFrameworkVersion} {frameworkReference}");

                Dictionary<string, NuGetVersion> packagesForFrameworkReference;
                if (useFrameworkPackageData)
                {
                    packagesForFrameworkReference = LoadPackagesToPruneFromFrameworkPackages(key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference);
                    if (packagesForFrameworkReference != null)
                    {
                        log.LogMessage("Loaded prune package data from framework packages");
                    }
                    else
                    {
                        log.LogMessage("Failed to load prune package data from framework packages");
                    }
                }
                else
                {
                    log.LogMessage("Loading prune package data from PrunePackageData folder");
                    packagesForFrameworkReference = LoadPackagesToPruneFromPrunePackageData(key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference, prunePackageDataRoot);

                    //  For the version of the runtime that matches the current SDK version, we don't include the prune package data in the PrunePackageData folder.  Rather,
                    //  we can load it from the targeting packs that are packaged with the SDK.
                    if (packagesForFrameworkReference == null)
                    {
                        log.LogMessage("Failed to load prune package data from PrunePackageData folder, loading from targeting packs instead");
                        packagesForFrameworkReference = LoadPackagesToPruneFromTargetingPack(log, key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference, targetingPackRoots);
                    }

                    //  Fall back to framework packages data for older framework for WindowsDesktop if necessary
                    //  https://github.com/dotnet/windowsdesktop/issues/4904
                    if (packagesForFrameworkReference == null && frameworkReference.Equals("Microsoft.WindowsDesktop.App", StringComparison.OrdinalIgnoreCase))
                    {
                        log.LogMessage("Failed to load prune package data for WindowsDesktop from targeting packs, loading from framework packages instead");
                        packagesForFrameworkReference = LoadPackagesToPruneFromFrameworkPackages(key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference,
                            acceptNearestMatch: true);
                    }
                }

                if (packagesForFrameworkReference == null)
                {
                    //  We didn't find the data for packages to prune.  This indicates that there's a bug in the SDK construction, so fail hard here so that we fix that
                    //  (rather than a warning that might be missed).
                    //  Since this indicates an error in the SDK build, the message probably doesn't need to be localized.

                    if (allowMissingPrunePackageData)
                    {
                        log.LogMessage($"Prune package data not found for {key.TargetFrameworkIdentifier} {key.TargetFrameworkVersion} {frameworkReference}");
                    }
                    else
                    {
                        log.LogError(Strings.PrunePackageDataNotFound, key.TargetFrameworkIdentifier, key.TargetFrameworkVersion, frameworkReference);
                    }
                }
                else
                {
                    AddPackagesToPrune(packagesToPrune, packagesForFrameworkReference.Select(kvp => (kvp.Key, kvp.Value)), log);
                }
            }



            return packagesToPrune.Select(p =>
            {
                var item = new TaskItem(p.Key);

                string version;
                if (key.TargetFrameworkIdentifier.Equals(".NETCoreApp", StringComparison.OrdinalIgnoreCase) && !p.Value.IsPrerelease)
                { 
                    //  If a given version of a package is included in a framework, assume that any patches
                    //  to that package will be included in patches to the framework, and thus should be pruned.
                    //  See https://github.com/dotnet/sdk/issues/44566
                    //  To do this, we set the patch version for the package to be pruned to 32767, which should be
                    //  higher than any actual patch version.
                    var maxPatch = new NuGetVersion(p.Value.Major, p.Value.Minor, 32767);
                    version = maxPatch.ToString();
                }
                else
                {
                    version = p.Value.ToString();
                }

                item.SetMetadata("Version", version.ToString());
                return item;
            }).ToArray();
        }

        static Dictionary<string, NuGetVersion> LoadPackagesToPruneFromFrameworkPackages(string targetFrameworkIdentifier, string targetFrameworkVersion, string frameworkReference, bool acceptNearestMatch = false)
        {
            var nugetFramework = new NuGetFramework(targetFrameworkIdentifier, Version.Parse(targetFrameworkVersion));

            //  FrameworkPackages just has data for .NET Framework 4.6.1, so turn on fallback for anything greater than that so it will resolve to the .NET Framework 4.6.1 data
            if (!acceptNearestMatch && nugetFramework.IsDesktop() && nugetFramework.Version > new Version(4,6,1))
            {
                acceptNearestMatch = true;
            }

            var frameworkPackages = FrameworkPackages.GetFrameworkPackages(nugetFramework, [frameworkReference], acceptNearestMatch)
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

        static Dictionary<string, NuGetVersion> LoadPackagesToPruneFromTargetingPack(Logger log, string targetFrameworkIdentifier, string targetFrameworkVersion, string frameworkReference, string [] targetingPackRoots)
        {
            var nugetFramework = new NuGetFramework(targetFrameworkIdentifier, Version.Parse(targetFrameworkVersion));

            foreach (var targetingPackRoot in targetingPackRoots)
            {
                var frameworkPackages = FrameworkPackages.LoadFrameworkPackagesFromPack(log, nugetFramework, frameworkReference, targetingPackRoot)
                    ?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (frameworkPackages != null)
                {
                    //  We found the framework packages in the targeting pack, so return them
                    return frameworkPackages;
                }
            }
            return null;
        }

        static void AddPackagesToPrune(Dictionary<string, NuGetVersion> packagesToPrune, IEnumerable<(string id, NuGetVersion version)> packagesToAdd, Logger log)
        {
            foreach (var package in packagesToAdd)
            {
                // There are some "inconsistent" versions in the FrameworkPackages data.  This happens because, for example, the ASP.NET Core shared framework for .NET 9 inherits
                // from the ASP.NET Core shared framework for .NET 8, but not from the base Microsoft.NETCore.App framework for .NET 9.  So for something like System.IO.Pipelines,
                // which was in ASP.NET in .NET 8 but moved to the base shared framework in .NET 9, we will see an 8.0 version from the ASP.NET shared framework and a 9.0 version
                // from the base shared framework.  As long as the base shared framework is always referenced together with the ASP.NET shared framework, this shouldn't be a
                // problem, and we can just pick the latest version of the package that we see.
                if (!packagesToPrune.TryGetValue(package.id, out NuGetVersion existingVersion) || package.version > existingVersion)
                {
                    packagesToPrune[package.id] = package.version;
                }
            }
        }
    }
}
