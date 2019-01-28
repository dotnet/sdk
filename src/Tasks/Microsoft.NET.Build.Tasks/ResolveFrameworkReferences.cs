using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// This class processes the FrameworkReference items.  It adds PackageReferences for the
    /// targeting packs which provide the reference assemblies, and creates RuntimeFramework
    /// items, which are written to the runtimeconfig file
    /// </summary>
    public class ResolveFrameworkReferences : TaskBase
    {
        public string TargetFrameworkIdentifier { get; set; }

        public string TargetFrameworkVersion { get; set; }

        public string TargetingPackRoot { get; set; }

        public string AppHostRuntimeIdentifier { get; set; }

        public ITaskItem[] PackAsToolShimAppHostRuntimeIdentifiers { get; set; }

        [Required]
        public string RuntimeGraphPath { get; set; }

        public bool SelfContained { get; set; }

        public string RuntimeIdentifier { get; set; }

        /// <summary>
        /// The file name of Apphost asset.
        /// </summary>
        [Required]
        public string DotNetAppHostExecutableNameWithoutExtension { get; set; }

        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] KnownFrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] PackagesToDownload { get; set; }

        [Output]
        public ITaskItem[] RuntimeFrameworks { get; set; }

        [Output]
        public ITaskItem[] TargetingPacks { get; set; }

        [Output]
        public ITaskItem[] RuntimePacks { get; set; }

        //  There should only be one AppHost item, but we use an item here so we can attach metadata to it
        //  (ie the apphost pack name and version, and the relative path to the apphost inside of it so
        //  we can resolve the full path later)
        [Output]
        public ITaskItem[] AppHost { get; set; }
        
        [Output]
        public ITaskItem[] PackAsToolShimAppHosts { get; set; }

        [Output]
        public string[] UnresolvedFrameworkReferences { get; set; }

        protected override void ExecuteCore()
        {
            var knownFrameworkReferences = KnownFrameworkReferences.Select(item => new KnownFrameworkReference(item))
                .Where(kfr => kfr.TargetFramework.Framework.Equals(TargetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) &&
                              NormalizeVersion(kfr.TargetFramework.Version) == NormalizeVersion(new Version(TargetFrameworkVersion)))
                .ToDictionary(kfr => kfr.Name);

            List<ITaskItem> packagesToDownload = new List<ITaskItem>();
            List<ITaskItem> runtimeFrameworks = new List<ITaskItem>();
            List<ITaskItem> targetingPacks = new List<ITaskItem>();
            List<ITaskItem> runtimePacks = new List<ITaskItem>();
            List<string> unresolvedFrameworkReferences = new List<string>();

            string appHostPackPattern = null;
            string appHostPackVersion = null;
            string appHostKnownRuntimeIdentifiers = null;

            foreach (var frameworkReference in FrameworkReferences)
            {
                KnownFrameworkReference knownFrameworkReference;
                if (knownFrameworkReferences.TryGetValue(frameworkReference.ItemSpec, out knownFrameworkReference))
                {
                    if (!string.IsNullOrEmpty(knownFrameworkReference.AppHostPackNamePattern))
                    {
                        if (appHostPackPattern == null)
                        {
                            appHostPackPattern = knownFrameworkReference.AppHostPackNamePattern;
                            appHostPackVersion = knownFrameworkReference.LatestRuntimeFrameworkVersion;
                            appHostKnownRuntimeIdentifiers = knownFrameworkReference.AppHostRuntimeIdentifiers;
                        }
                        else
                        {
                            throw new InvalidOperationException("Multiple FrameworkReferences defined an AppHostPack, which is not supposed to happen");
                        }
                    }

                    //  Get the path of the targeting pack in the targeting pack root (e.g. dotnet/ref)
                    TaskItem targetingPack = new TaskItem(knownFrameworkReference.Name);
                    targetingPack.SetMetadata(MetadataKeys.PackageName, knownFrameworkReference.TargetingPackName);
                    targetingPack.SetMetadata(MetadataKeys.PackageVersion, knownFrameworkReference.TargetingPackVersion);
                    targetingPack.SetMetadata(MetadataKeys.RelativePath, "");

                    string targetingPackPath = null;
                    if (!string.IsNullOrEmpty(TargetingPackRoot))
                    {
                        targetingPackPath = GetPackPath(knownFrameworkReference.TargetingPackName, knownFrameworkReference.TargetingPackVersion);
                    }
                    if (targetingPackPath != null && Directory.Exists(targetingPackPath))
                    {
                        targetingPack.SetMetadata(MetadataKeys.Path, targetingPackPath);
                        targetingPack.SetMetadata(MetadataKeys.PackageDirectory, targetingPackPath);
                    }
                    else
                    {
                        //  Download targeting pack
                        TaskItem packageToDownload = new TaskItem(knownFrameworkReference.TargetingPackName);
                        packageToDownload.SetMetadata(MetadataKeys.Version, knownFrameworkReference.TargetingPackVersion);

                        packagesToDownload.Add(packageToDownload);
                    }

                    targetingPacks.Add(targetingPack);

                    if (SelfContained &&
                        !string.IsNullOrEmpty(RuntimeIdentifier) &&
                        !string.IsNullOrEmpty(knownFrameworkReference.RuntimePackNamePatterns))
                    {
                        foreach (var runtimePackNamePattern in knownFrameworkReference.RuntimePackNamePatterns.Split(';'))
                        {
                            string runtimePackRuntimeIdentifier = new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath).GetBestRuntimeIdentifier(RuntimeIdentifier, knownFrameworkReference.RuntimePackRuntimeIdentifiers, out bool wasInGraph);

                            if (runtimePackRuntimeIdentifier == null)
                            {
                                if (wasInGraph)
                                {
                                    //  NETSDK1082: There was no runtime pack for {0} available for the specified RuntimeIdentifier '{1}'.
                                    Log.LogError(Strings.NoRuntimePackAvailable, knownFrameworkReference.Name, RuntimeIdentifier);
                                }
                                else
                                {
                                    //  NETSDK1083: The specified RuntimeIdentifier '{0}' is not recognized.
                                    Log.LogError(Strings.UnsupportedRuntimeIdentifier, RuntimeIdentifier);
                                }
                            }
                            else
                            {
                                string runtimePackName = runtimePackNamePattern.Replace("**RID**", runtimePackRuntimeIdentifier);

                                TaskItem runtimePackItem = new TaskItem(runtimePackName);
                                runtimePackItem.SetMetadata(MetadataKeys.PackageName, runtimePackName);
                                runtimePackItem.SetMetadata(MetadataKeys.PackageVersion, knownFrameworkReference.LatestRuntimeFrameworkVersion);
                                runtimePackItem.SetMetadata("FrameworkReference", knownFrameworkReference.Name);
                                runtimePackItem.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimePackRuntimeIdentifier);

                                runtimePacks.Add(runtimePackItem);

                                TaskItem packageToDownload = new TaskItem(runtimePackName);
                                packageToDownload.SetMetadata(MetadataKeys.Version, knownFrameworkReference.LatestRuntimeFrameworkVersion);

                                packagesToDownload.Add(packageToDownload);
                            }
                        }
                    }

                    TaskItem runtimeFramework = new TaskItem(knownFrameworkReference.RuntimeFrameworkName);

                    //  Use default (non roll-forward) version for now.  Eventually we'll need to add support for rolling
                    //  forward, and for publishing assets from a runtime pack for self-contained apps
                    runtimeFramework.SetMetadata(MetadataKeys.Version, knownFrameworkReference.DefaultRuntimeFrameworkVersion);

                    runtimeFrameworks.Add(runtimeFramework);
                }
                else
                {
                    unresolvedFrameworkReferences.Add(frameworkReference.ItemSpec);
                }
            }

            ApphostResolver apphostResolver
                = new ApphostResolver(
                    appHostPackPattern,
                    appHostKnownRuntimeIdentifiers,
                    appHostPackVersion,
                    TargetingPackRoot,
                    new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath),
                    DotNetAppHostExecutableNameWithoutExtension,
                    Log);

            var AppHostAndAdditionalPackageToDownload = apphostResolver.GetAppHostItem(AppHostRuntimeIdentifier, "AppHost");

            AppHost = AppHostAndAdditionalPackageToDownload.AppHost;
            packagesToDownload.AddRange(AppHostAndAdditionalPackageToDownload.AdditionalPackagesToDownload);

            if (PackAsToolShimAppHostRuntimeIdentifiers != null)
            {
                List<ITaskItem> packAsToolShimAppHostsList = new List<ITaskItem>();
                foreach (var packAsToolShimAppHostRuntimeIdentifier in PackAsToolShimAppHostRuntimeIdentifiers)
                {
                    var shimApphostAndPackage = apphostResolver.GetAppHostItem(packAsToolShimAppHostRuntimeIdentifier.ItemSpec, "PackAsToolShimAppHost");

                    var PackAsToolShimAppHosts = shimApphostAndPackage.AppHost;
                    packagesToDownload.AddRange(shimApphostAndPackage.AdditionalPackagesToDownload);

                    if (PackAsToolShimAppHosts != null)
                    {
                        packAsToolShimAppHostsList.AddRange(PackAsToolShimAppHosts);
                    }
                }
                PackAsToolShimAppHosts = packAsToolShimAppHostsList.ToArray();
            }

            if (packagesToDownload.Any())
            {
                PackagesToDownload = packagesToDownload.ToArray();
            }

            if (runtimeFrameworks.Any())
            {
                RuntimeFrameworks = runtimeFrameworks.ToArray();
            }

            if (targetingPacks.Any())
            {
                TargetingPacks = targetingPacks.ToArray();
            }

            if (runtimePacks.Any())
            {
                RuntimePacks = runtimePacks.ToArray();
            }

            if (unresolvedFrameworkReferences.Any())
            {
                UnresolvedFrameworkReferences = unresolvedFrameworkReferences.ToArray();
            }
        }

        private string GetPackPath(string name, string version)
        {
            return Path.Combine(TargetingPackRoot, name, version);
        }

        private static Version NormalizeVersion(Version version)
        {
            if (version.Revision == 0)
            {
                if (version.Build == 0)
                {
                    return new Version(version.Major, version.Minor);
                }
                else
                {
                    return new Version(version.Major, version.Minor, version.Build);
                }
            }

            return version;
        }

        private class KnownFrameworkReference
        {
            ITaskItem _item;
            public KnownFrameworkReference(ITaskItem item)
            {
                _item = item;
                TargetFramework = NuGetFramework.Parse(item.GetMetadata("TargetFramework"));
            }

            //  The name / itemspec of the FrameworkReference used in the project
            public string Name => _item.ItemSpec;

            //  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
            public string RuntimeFrameworkName => _item.GetMetadata("RuntimeFrameworkName");
            public string DefaultRuntimeFrameworkVersion => _item.GetMetadata("DefaultRuntimeFrameworkVersion");
            public string LatestRuntimeFrameworkVersion => _item.GetMetadata("LatestRuntimeFrameworkVersion");

            //  The ID of the targeting pack NuGet package to reference
            public string TargetingPackName => _item.GetMetadata("TargetingPackName");
            public string TargetingPackVersion => _item.GetMetadata("TargetingPackVersion");

            public string AppHostPackNamePattern => _item.GetMetadata("AppHostPackNamePattern");

            public string AppHostRuntimeIdentifiers => _item.GetMetadata("AppHostRuntimeIdentifiers");

            public string RuntimePackNamePatterns => _item.GetMetadata("RuntimePackNamePatterns");

            public string RuntimePackRuntimeIdentifiers => _item.GetMetadata("RuntimePackRuntimeIdentifiers");

            public NuGetFramework TargetFramework { get; }
        }
    }
}
