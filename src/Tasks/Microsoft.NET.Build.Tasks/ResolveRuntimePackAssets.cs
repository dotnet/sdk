// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveRuntimePackAssets : TaskBase
    {
        public ITaskItem[] ResolvedRuntimePacks { get; set; }

        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] UnavailableRuntimePacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] SatelliteResourceLanguages { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] RuntimeFrameworks { get; set; }        

        public bool DesignTimeBuild { get; set; }

        public bool DisableTransitiveFrameworkReferenceDownloads { get; set; }

        [Output]
        public ITaskItem[] RuntimePackAssets { get; set; }

        protected override void ExecuteCore()
        {
            var runtimePackAssets = new List<ITaskItem>();

            // Find any RuntimeFrameworks that matches with FrameworkReferences, so that we can apply that RuntimeFrameworks profile to the corresponding RuntimePack.
            // This is done in 2 parts, First part (see comments for 2nd part further below), we match the RuntimeFramework with the FrameworkReference by using the following metadata.
            // RuntimeFrameworks.GetMetadata("FrameworkName")==FrameworkReferences.ItemSpec
            // For example, A WinForms app that uses useWindowsForms (and useWPF will be set to false) has the following values that will result in a match of the below RuntimeFramework.
            // FrameworkReferences with an ItemSpec "Microsoft.WindowsDesktop.App.WindowsForms" will match with 
            // RuntimeFramework with an ItemSpec => "Microsoft.WindowsDesktop.App", GetMetadata("FrameworkName") => "Microsoft.WindowsDesktop.App.WindowsForms", GetMetadata("Profile") => "WindowsForms"
            List<ITaskItem> matchingRuntimeFrameworks = RuntimeFrameworks != null ? FrameworkReferences
                    .SelectMany(fxReference => RuntimeFrameworks.Where(rtFx =>
                        fxReference.ItemSpec.Equals(rtFx.GetMetadata(MetadataKeys.FrameworkName), StringComparison.OrdinalIgnoreCase)))
                        .ToList() : null;

            HashSet<string> frameworkReferenceNames = new(FrameworkReferences.Select(item => item.ItemSpec), StringComparer.OrdinalIgnoreCase);

            foreach (var unavailableRuntimePack in UnavailableRuntimePacks)
            {
                if (frameworkReferenceNames.Contains(unavailableRuntimePack.ItemSpec))
                {
                    //  This is a runtime pack that should be used, but wasn't available for the specified RuntimeIdentifier
                    //  NETSDK1082: There was no runtime pack for {0} available for the specified RuntimeIdentifier '{1}'.
                    Log.LogError(Strings.NoRuntimePackAvailable, unavailableRuntimePack.ItemSpec,
                        unavailableRuntimePack.GetMetadata(MetadataKeys.RuntimeIdentifier));
                }
            }

            HashSet<string> processedRuntimePackRoots = new(StringComparer.OrdinalIgnoreCase);

            foreach (var runtimePack in ResolvedRuntimePacks)
            {
                if (!frameworkReferenceNames.Contains(runtimePack.GetMetadata(MetadataKeys.FrameworkName)))
                {
                    var additionalFrameworkReferences = runtimePack.GetMetadata(MetadataKeys.AdditionalFrameworkReferences);
                    if (additionalFrameworkReferences == null ||
                        !additionalFrameworkReferences.Split(';').Any(afr => frameworkReferenceNames.Contains(afr)))
                    {
                        //  This is a runtime pack for a shared framework that ultimately wasn't referenced, so don't include its assets
                        continue;
                    }
                }

                // For any RuntimeFrameworks that matches with FrameworkReferences, we can apply that RuntimeFrameworks profile to the corresponding RuntimePack.
                // This is done in 2 parts, second part (see comments for 1st part above), Matches the RuntimeFramework with the ResolvedRuntimePacks by comparing the following metadata.
                // RuntimeFrameworks.ItemSpec == ResolvedRuntimePacks.GetMetadata("FrameworkName")
                // For example, A WinForms app that uses useWindowsForms (and useWPF will be set to false) has the following values that will result in a match of the below RuntimeFramework
                // matchingRTReference.GetMetadata("Profile") will be "WindowsForms". 'Profile' will be an empty string if no matching RuntimeFramework is found
                HashSet<string> profiles = matchingRuntimeFrameworks?
                    .Where(matchingRTReference => runtimePack.GetMetadata("FrameworkName").Equals(matchingRTReference.ItemSpec))
                    .Select(matchingRTReference => matchingRTReference.GetMetadata("Profile")).ToHashSet() ?? [];

                // Special case the Windows SDK projections. Normally the Profile information flows through the RuntimeFramework items,
                // but those aren't created for RuntimePackAlwaysCopyLocal references. This logic could be revisited later to be generalized in some way.
                if (runtimePack.GetMetadata(MetadataKeys.FrameworkName) == "Microsoft.Windows.SDK.NET.Ref")
                {
                    if (FrameworkReferences?.Any(fxReference => fxReference.ItemSpec == "Microsoft.Windows.SDK.NET.Ref.Windows") == true)
                    {
                        profiles.Add("Windows");
                    }
                    
                    if (FrameworkReferences?.Any(fxReference => fxReference.ItemSpec == "Microsoft.Windows.SDK.NET.Ref.Xaml") == true)
                    {
                        profiles.Add("Xaml");
                    }
                }

                //  If we have a runtime framework with an empty profile, it means that we should use all of the contents of the runtime pack,
                //  so we can clear the profile list
                if (profiles.Contains(string.Empty))
                {
                    profiles.Clear();
                }

                string runtimePackRoot = runtimePack.GetMetadata(MetadataKeys.PackageDirectory);

                if (string.IsNullOrEmpty(runtimePackRoot) || !Directory.Exists(runtimePackRoot))
                {
                    if (!DesignTimeBuild)
                    {
                        //  Don't treat this as an error if we are doing a design-time build.  This is because the design-time
                        //  build needs to succeed in order to get the right information in order to run a restore to download
                        //  the runtime pack.

                        if (DisableTransitiveFrameworkReferenceDownloads)
                        {
                            Log.LogError(Strings.RuntimePackNotRestored_TransitiveDisabled, runtimePack.ItemSpec);
                        }
                        else
                        {
                            Log.LogError(Strings.RuntimePackNotDownloaded, runtimePack.ItemSpec,
                                runtimePack.GetMetadata(MetadataKeys.RuntimeIdentifier));
                        }
                    }
                    continue;
                }

                if (!processedRuntimePackRoots.Add(runtimePackRoot))
                {
                    //  We already added assets from this runtime pack (which can happen with FrameworkReferences to different
                    //  profiles of the same shared framework)
                    continue;
                }

                var runtimeListPath = Path.Combine(runtimePackRoot, "data", "RuntimeList.xml");

                if (File.Exists(runtimeListPath))
                {
                    var runtimePackAlwaysCopyLocal = runtimePack.HasMetadataValue(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");

                    AddRuntimePackAssetsFromManifest(runtimePackAssets, runtimePackRoot, runtimeListPath, runtimePack, runtimePackAlwaysCopyLocal, profiles);
                }
                else
                {
                    throw new BuildErrorException(string.Format(Strings.RuntimeListNotFound, runtimeListPath));
                }
            }

            RuntimePackAssets = runtimePackAssets.ToArray();
        }

        private void AddRuntimePackAssetsFromManifest(List<ITaskItem> runtimePackAssets, string runtimePackRoot,
            string runtimeListPath, ITaskItem runtimePack, bool runtimePackAlwaysCopyLocal, HashSet<string> profiles)
        {
            var assetSubPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            XDocument frameworkListDoc = XDocument.Load(runtimeListPath);
            // profile feature is only supported in net9.0 and later. We would ignore it for previous versions.
            bool profileSupported = false;
            string targetFrameworkVersion = frameworkListDoc.Root.Attribute("TargetFrameworkVersion")?.Value;
            if (!string.IsNullOrEmpty(targetFrameworkVersion))
            {
                string[] parts = targetFrameworkVersion.Split('.');
                if (parts.Length > 0 && int.TryParse(parts[0], out int versionNumber))
                {
                    // The Windows SDK projections use profiles and need to be supported on .NET 8 as well.
                    // No other packages are supported using profiles below .NET 9, so we can special case.
                    if (versionNumber >= 9 ||
                        (versionNumber >= 8 && frameworkListDoc.Root.Attribute("FrameworkName")?.Value == "Microsoft.Windows.SDK.NET.Ref"))
                    {
                        profileSupported = true;
                    }
                }
            }
            foreach (var fileElement in frameworkListDoc.Root.Elements("File"))
            {
                if (profileSupported && profiles.Count != 0)
                {
                    var profileAttributeValue = fileElement.Attribute("Profile")?.Value;

                    var assemblyProfiles = profileAttributeValue?.Split(';');
                    if (profileAttributeValue == null || !assemblyProfiles.Any(p => profiles.Contains(p)))
                    {
                        //  Assembly wasn't in the profile specified, so don't reference it
                        continue;
                    }
                }

                //  Call GetFullPath to normalize slashes
                string assetPath = Path.GetFullPath(Path.Combine(runtimePackRoot, fileElement.Attribute("Path").Value));

                string typeAttributeValue = fileElement.Attribute("Type").Value;
                string assetType;
                string culture = null;
                if (typeAttributeValue.Equals("Managed", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "runtime";
                }
                else if (typeAttributeValue.Equals("Native", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "native";
                }
                else if (typeAttributeValue.Equals("PgoData", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "pgodata";
                }
                else if (typeAttributeValue.Equals("Resources", StringComparison.OrdinalIgnoreCase))
                {
                    assetType = "resources";
                    culture = fileElement.Attribute("Culture")?.Value;
                    if (culture == null)
                    {
                        throw new BuildErrorException($"Culture not set in runtime manifest for {assetPath}");
                    }
                    if (SatelliteResourceLanguages.Length >= 1 &&
                        !SatelliteResourceLanguages.Any(lang => string.Equals(lang.ItemSpec, culture, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }
                else
                {
                    throw new BuildErrorException($"Unrecognized file type '{typeAttributeValue}' in {runtimeListPath}");
                }

                var assetItem = CreateAssetItem(assetPath, assetType, runtimePack, culture);

                // Ensure the asset item's destination sub-path is unique
                var assetSubPath = assetItem.GetMetadata(MetadataKeys.DestinationSubPath);
                if (!assetSubPaths.Add(assetSubPath))
                {
                    Log.LogError(Strings.DuplicateRuntimePackAsset, assetSubPath);
                    continue;
                }

                assetItem.SetMetadata("AssemblyVersion", fileElement.Attribute("AssemblyVersion")?.Value);
                assetItem.SetMetadata("FileVersion", fileElement.Attribute("FileVersion")?.Value);
                assetItem.SetMetadata("PublicKeyToken", fileElement.Attribute("PublicKeyToken")?.Value);
                assetItem.SetMetadata("DropFromSingleFile", fileElement.Attribute("DropFromSingleFile")?.Value);
                if (runtimePackAlwaysCopyLocal)
                {
                    assetItem.SetMetadata(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");
                }

                runtimePackAssets.Add(assetItem);
            }
        }

        private static TaskItem CreateAssetItem(string assetPath, string assetType, ITaskItem runtimePack, string culture)
        {
            string runtimeIdentifier = runtimePack.GetMetadata(MetadataKeys.RuntimeIdentifier);

            var assetItem = new TaskItem(assetPath);

            if (assetType != "pgodata")
                assetItem.SetMetadata(MetadataKeys.CopyLocal, "true");

            if (string.IsNullOrEmpty(culture))
            {
                assetItem.SetMetadata(MetadataKeys.DestinationSubPath, Path.GetFileName(assetPath));
            }
            else
            {
                assetItem.SetMetadata(MetadataKeys.DestinationSubDirectory, culture + Path.DirectorySeparatorChar);
                assetItem.SetMetadata(MetadataKeys.DestinationSubPath, Path.Combine(culture, Path.GetFileName(assetPath)));
                assetItem.SetMetadata(MetadataKeys.Culture, culture);
            }

            assetItem.SetMetadata(MetadataKeys.AssetType, assetType);
            assetItem.SetMetadata(MetadataKeys.NuGetPackageId, runtimePack.GetMetadata(MetadataKeys.NuGetPackageId));
            assetItem.SetMetadata(MetadataKeys.NuGetPackageVersion, runtimePack.GetMetadata(MetadataKeys.NuGetPackageVersion));
            assetItem.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);
            assetItem.SetMetadata(MetadataKeys.IsTrimmable, runtimePack.GetMetadata(MetadataKeys.IsTrimmable));

            return assetItem;
        }
    }
}
