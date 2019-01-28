﻿using System;
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
            string appHostRuntimeIdentifiers = null;

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
                            appHostRuntimeIdentifiers = knownFrameworkReference.AppHostRuntimeIdentifiers;
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
                            string runtimePackRuntimeIdentifier = GetBestRuntimeIdentifier(
                                RuntimeIdentifier,
                                knownFrameworkReference.RuntimePackRuntimeIdentifiers,
                                new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath),
                                out bool wasInGraph);

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

            AppHost = GetAppHostItem(appHostPackPattern, appHostRuntimeIdentifiers, appHostPackVersion,
                packagesToDownload, AppHostRuntimeIdentifier, "AppHost");

            if (PackAsToolShimAppHostRuntimeIdentifiers != null)
            {
                List<ITaskItem> packAsToolShimAppHostsList = new List<ITaskItem>();
                foreach (var packAsToolShimAppHostRuntimeIdentifier in PackAsToolShimAppHostRuntimeIdentifiers)
                {
                    var PackAsToolShimAppHosts = GetAppHostItem(
                            appHostPackPattern,
                            appHostRuntimeIdentifiers,
                            appHostPackVersion,
                            packagesToDownload,
                            packAsToolShimAppHostRuntimeIdentifier.ItemSpec,
                            "PackAsToolShimAppHost");

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

        private ITaskItem[] GetAppHostItem(
            string appHostPackPattern, 
            string appHostRuntimeIdentifiers,
            string appHostPackVersion, List<ITaskItem> packagesToDownload, 
            string appHostRuntimeIdentifier, 
            string itemName)
        {
            if (!string.IsNullOrEmpty(appHostRuntimeIdentifier) && !string.IsNullOrEmpty(appHostPackPattern))
            {
                //  Choose AppHost RID as best match of the specified RID
                string bestAppHostRuntimeIdentifier =
                    GetBestRuntimeIdentifier(
                        appHostRuntimeIdentifier,
                        appHostRuntimeIdentifiers,
                        new RuntimeGraphCache(this).GetRuntimeGraph(RuntimeGraphPath),
                        out bool wasInGraph);

                if (bestAppHostRuntimeIdentifier == null)
                {
                    if (wasInGraph)
                    {
                        //  NETSDK1084: There was no app host for available for the specified RuntimeIdentifier '{0}'.
                        Log.LogError(Strings.NoAppHostAvailable, appHostRuntimeIdentifier);
                    }
                    else
                    {
                        //  NETSDK1083: The specified RuntimeIdentifier '{0}' is not recognized.
                        Log.LogError(Strings.UnsupportedRuntimeIdentifier, appHostRuntimeIdentifier);
                    }
                }
                else
                {
                    string appHostPackName = appHostPackPattern.Replace("**RID**", bestAppHostRuntimeIdentifier);

                    string appHostRelativePathInPackage = Path.Combine("runtimes", bestAppHostRuntimeIdentifier, "native",
                        DotNetAppHostExecutableNameWithoutExtension +
                        ExecutableExtension.ForRuntimeIdentifier(bestAppHostRuntimeIdentifier));


                    TaskItem appHostItem = new TaskItem(itemName);
                    string appHostPackPath = null;
                    if (!string.IsNullOrEmpty(TargetingPackRoot))
                    {
                        appHostPackPath = GetPackPath(appHostPackName, appHostPackVersion);
                    }

                    if (appHostPackPath != null && Directory.Exists(appHostPackPath))
                    {
                        //  Use AppHost from packs folder
                        appHostItem.SetMetadata(MetadataKeys.Path, Path.Combine(appHostPackPath, appHostRelativePathInPackage));
                    }
                    else
                    {
                        //  Download apphost pack
                        TaskItem packageToDownload = new TaskItem(appHostPackName);
                        packageToDownload.SetMetadata(MetadataKeys.Version, appHostPackVersion);
                        packagesToDownload.Add(packageToDownload);

                        appHostItem.SetMetadata(MetadataKeys.RuntimeIdentifier, appHostRuntimeIdentifier);
                        appHostItem.SetMetadata(MetadataKeys.PackageName, appHostPackName);
                        appHostItem.SetMetadata(MetadataKeys.PackageVersion, appHostPackVersion);
                        appHostItem.SetMetadata(MetadataKeys.RelativePath, appHostRelativePathInPackage);
                    }

                    return new ITaskItem[] {appHostItem};
                }
            }
            return null;
        }

        private string GetBestRuntimeIdentifier(string targetRuntimeIdentifier, string availableRuntimeIdentifiers,
            RuntimeGraph runtimeGraph, out bool wasInGraph)
        {
            if (targetRuntimeIdentifier == null || availableRuntimeIdentifiers == null)
            {
                wasInGraph = false;
                return null;
            }

            return NuGetUtils.GetBestMatchingRid(
                runtimeGraph,
                targetRuntimeIdentifier,
                availableRuntimeIdentifiers.Split(';'),
                out wasInGraph);
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
